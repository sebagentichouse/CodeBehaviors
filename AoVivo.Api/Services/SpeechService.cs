using System.ClientModel;
using AoVivo.Api.Audio;
using AoVivo.Api.Options;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Audio;

namespace AoVivo.Api.Services;

/// <summary>
/// Speech-to-text and text-to-speech with Azure OpenAI audio deployments, authenticated with the same
/// service principal as the agent. Registered as a singleton: the SDK clients are thread-safe.
/// </summary>
public sealed class SpeechService : ISpeechService
{
    /// <summary>
    /// Characters per text-to-speech request. The models accept ~4096 characters (tts) or ~2000 tokens
    /// (gpt-4o-mini-tts); 3000 keeps a margin for both.
    /// </summary>
    private const int MaxCharsPerSpeechRequest = 3000;

    private readonly AudioClient _transcriber;
    private readonly AudioClient _synthesizer;
    private readonly SpeechOptions _options;
    private readonly ILogger<SpeechService> _logger;

    public SpeechService(AzureOpenAIClient client, IOptions<SpeechOptions> options, ILogger<SpeechService> logger)
    {
        _options = options.Value;
        _transcriber = client.GetAudioClient(_options.TranscriptionDeployment);
        _synthesizer = client.GetAudioClient(_options.SpeechDeployment);
        _logger = logger;
    }

    public async Task<string> TranscribeAsync(byte[] pcm, CancellationToken cancellationToken = default)
    {
        // The transcription API needs a file format: wrap the samples in a WAV header.
        using var wav = new MemoryStream(PcmAudio.ToWav(pcm), writable: false);

        var options = new AudioTranscriptionOptions();
        if (!string.IsNullOrWhiteSpace(_options.TranscriptionLanguage))
            options.Language = _options.TranscriptionLanguage;

        try
        {
            ClientResult<AudioTranscription> result = await _transcriber
                .TranscribeAudioAsync(wav, "speech.wav", options, cancellationToken)
                .ConfigureAwait(false);

            string text = result.Value.Text?.Trim() ?? string.Empty;
            _logger.LogDebug("Transcribed {Seconds:0.0}s of audio: {Text}", pcm.Length / (double)PcmAudio.BytesPerSecond, text);
            return text;
        }
        catch (ClientResultException ex)
        {
            throw new SpeechServiceException(
                $"Speech-to-text deployment '{_options.TranscriptionDeployment}' failed (HTTP {ex.Status}): {ex.Message}", ex);
        }
    }

    public async Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        var voice = new GeneratedSpeechVoice(_options.Voice);
        // "pcm" = raw PCM16, 24 kHz, mono: exactly the format of PcmAudio, so the pieces can simply be joined.
        var options = new SpeechGenerationOptions { ResponseFormat = GeneratedSpeechFormat.Pcm };

        using var output = new MemoryStream();
        try
        {
            foreach (string piece in SpeechText.Split(text, MaxCharsPerSpeechRequest))
            {
                ClientResult<BinaryData> audio = await _synthesizer
                    .GenerateSpeechAsync(piece, voice, options, cancellationToken)
                    .ConfigureAwait(false);
                output.Write(audio.Value.ToMemory().Span);
            }
        }
        catch (ClientResultException ex)
        {
            throw new SpeechServiceException(
                $"Text-to-speech deployment '{_options.SpeechDeployment}' failed (HTTP {ex.Status}): {ex.Message}", ex);
        }

        return output.ToArray();
    }
}
