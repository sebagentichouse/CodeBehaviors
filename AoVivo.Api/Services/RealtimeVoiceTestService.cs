using System.Text.Json;
using AoVivo.Api.Audio;
using AoVivo.Api.Models;
using AoVivo.Api.Options;
using Azure.AI.VoiceLive;
using Microsoft.Extensions.Options;

namespace AoVivo.Api.Services;

/// <summary>
/// TEST ONLY: one VoiceLive session per request with a realtime model (<see cref="VoiceLiveOptions.RealtimeModel"/>).
/// The Foundry agent is NOT involved, so there is no web search and nothing is stored in a Foundry conversation.
/// Registered as a singleton: the VoiceLive client is thread-safe and meant to be reused.
/// </summary>
public sealed class RealtimeVoiceTestService : IRealtimeVoiceTestService
{
    /// <summary>Audio is streamed to VoiceLive in 1-second messages.</summary>
    private const int AudioChunkBytes = PcmAudio.BytesPerSecond;

    private readonly VoiceLiveClient _voiceClient;
    private readonly VoiceLiveOptions _voice;
    private readonly AgentOptions _agent;
    private readonly ILogger<RealtimeVoiceTestService> _logger;

    public RealtimeVoiceTestService(
        VoiceLiveClient voiceClient,
        IOptions<VoiceLiveOptions> voiceOptions,
        IOptions<AgentOptions> agentOptions,
        ILogger<RealtimeVoiceTestService> logger)
    {
        _voiceClient = voiceClient;
        _voice = voiceOptions.Value;
        _agent = agentOptions.Value;
        _logger = logger;
    }

    public async Task<RealtimeTestResponse> ConverseAsync(byte[]? audio, CancellationToken cancellationToken = default)
    {
        bool isGreeting = audio is null || audio.Length == 0;
        if (!isGreeting && PcmAudio.Validate(audio!) is { } problem)
            throw new ArgumentException(problem, nameof(audio));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_voice.ResponseTimeoutSeconds));
        CancellationToken token = timeout.Token;

        try
        {
            using VoiceLiveSession session = await _voiceClient
                .StartSessionAsync(SessionTarget.FromModel(_voice.RealtimeModel), token)
                .ConfigureAwait(false);

            // PCM16 in and out, no server voice-activity detection (the caller sends a complete utterance, so we
            // commit it and ask for exactly one response), and instructions that say the model has no tools.
            await SendCommandAsync(session, new
            {
                type = "session.update",
                session = new
                {
                    instructions = _voice.RealtimeInstructions,
                    input_audio_format = "pcm16",
                    output_audio_format = "pcm16",
                    turn_detection = (object?)null
                }
            }, token).ConfigureAwait(false);

            using var output = new MemoryStream();
            string? transcript = null;
            bool inputSent = false;

            await foreach (SessionUpdate update in session.GetUpdatesAsync(token).ConfigureAwait(false))
            {
                switch (update)
                {
                    // Send the user's turn only once the session configuration is applied.
                    case SessionUpdateSessionUpdated when !inputSent:
                        inputSent = true;
                        await SendUserTurnAsync(session, audio, token).ConfigureAwait(false);
                        break;

                    case SessionUpdateResponseAudioDelta audioDelta when audioDelta.Delta is not null:
                        output.Write(audioDelta.Delta.ToArray());
                        break;

                    case SessionUpdateResponseAudioTranscriptDone transcriptDone:
                        transcript = transcriptDone.Transcript;
                        break;

                    case SessionUpdateError error:
                        _logger.LogError("VoiceLive session error (model {Model}): {Error}", _voice.RealtimeModel, Describe(error));
                        throw new VoiceSessionException($"VoiceLive error: {error.Error?.Message ?? "unknown error"}");

                    case SessionUpdateResponseDone when inputSent:
                        _logger.LogInformation(
                            "TEST realtime model {Model} answered ({Kind}, {Seconds:0.0}s of audio): {Transcript}",
                            _voice.RealtimeModel, isGreeting ? "greeting" : "audio",
                            output.Length / (double)PcmAudio.BytesPerSecond, transcript);
                        return new RealtimeTestResponse(_voice.RealtimeModel, output.ToArray(), transcript);

                    default:
                        // The sequence of events is the first thing to look at when a session misbehaves.
                        _logger.LogDebug("VoiceLive event: {Event}", update.GetType().Name);
                        break;
                }
            }

            throw new VoiceSessionException("The VoiceLive session closed before the model finished answering.");
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The realtime model did not answer within {_voice.ResponseTimeoutSeconds} seconds.");
        }
    }

    /// <summary>Sends the user's audio (or the greeting prompt when there is none) and requests the answer.</summary>
    private async Task SendUserTurnAsync(VoiceLiveSession session, byte[]? audio, CancellationToken cancellationToken)
    {
        if (audio is null || audio.Length == 0)
        {
            // Same greeting prompt as the agent endpoints, sent as a user text message.
            await SendCommandAsync(session, new
            {
                type = "conversation.item.create",
                item = new
                {
                    type = "message",
                    role = "user",
                    content = new[] { new { type = "input_text", text = _agent.GreetingPrompt } }
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            for (int offset = 0; offset < audio.Length; offset += AudioChunkBytes)
            {
                byte[] chunk = audio.AsSpan(offset, Math.Min(AudioChunkBytes, audio.Length - offset)).ToArray();
                await session.SendInputAudioAsync(chunk, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            await SendCommandAsync(session, new { type = "input_audio_buffer.commit" }, cancellationToken).ConfigureAwait(false);
        }

        await SendCommandAsync(session, new { type = "response.create" }, cancellationToken).ConfigureAwait(false);
    }

    private static Task SendCommandAsync(VoiceLiveSession session, object command, CancellationToken cancellationToken) =>
        session.SendCommandAsync(BinaryData.FromObjectAsJson(command), cancellationToken);

    /// <summary>All the fields of a VoiceLive error (code, type, param, event id...), not only the message.</summary>
    private static string Describe(SessionUpdateError error)
    {
        if (error.Error is null)
            return "(no error details)";
        try
        {
            return JsonSerializer.Serialize(error.Error, error.Error.GetType());
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
        {
            return error.Error.Message ?? "(no message)";
        }
    }
}
