using AoVivo.Api.Models;
using AoVivo.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AoVivo.Api.Controllers;

/// <summary>
/// TEST ONLY – talks to a VoiceLive realtime model directly. The Foundry agent is NOT used: no agent
/// instructions, no tools (no web search), no conversation history, nothing stored in a Foundry conversation.
/// Useful to compare audio quality and latency. Available only in the Development environment.
/// </summary>
[ApiController]
[Route("api/chat/audio/realtime")]
[Tags("TEST ONLY - realtime model, no agent")]
public sealed class RealtimeTestController : AudioControllerBase
{
    private readonly IRealtimeVoiceTestService _realtime;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<RealtimeTestController> _logger;

    public RealtimeTestController(
        IRealtimeVoiceTestService realtime,
        IWebHostEnvironment environment,
        ILogger<RealtimeTestController> logger)
    {
        _realtime = realtime;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// TEST ONLY – the realtime model answers, not the agent (no web search, no conversation).
    /// Body: WAV or raw PCM (PCM16, 24 kHz, mono); empty body: spoken greeting.
    /// Response: the spoken answer (audio/wav, or raw PCM with <c>?format=pcm</c>), the model's words (URL-encoded)
    /// in X-Model-Transcript, and X-Voice-Target stating that no agent was involved.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxAudioRequestBytes)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> Post(
        [FromQuery] string format = "wav",
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        if (!TryParseFormat(format, allowJson: false, out AudioFormat audioFormat))
            return InvalidFormat("Use format=wav (default) or format=pcm.");

        var (pcm, invalidAudio) = await ReadAudioAsync(cancellationToken);
        if (invalidAudio is not null)
            return invalidAudio;

        try
        {
            RealtimeTestResponse reply = await _realtime.ConverseAsync(pcm, cancellationToken);

            Response.Headers[ApiHeaders.VoiceTarget] = $"realtime-model={reply.Model}; agent=none";
            if (!string.IsNullOrEmpty(reply.Transcript))
                Response.Headers[ApiHeaders.ModelTranscript] = Uri.EscapeDataString(reply.Transcript); // headers must be ASCII

            return AudioFile(reply.Audio, audioFormat);
        }
        catch (Exception ex) when (IsServiceError(ex))
        {
            return ServiceError(ex, conversationId: null, _logger, defaultTitle: "Realtime model (test) error");
        }
    }
}
