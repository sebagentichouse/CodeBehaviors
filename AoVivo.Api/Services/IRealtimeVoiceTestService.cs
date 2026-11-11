using AoVivo.Api.Models;

namespace AoVivo.Api.Services;

/// <summary>
/// TEST ONLY. Talks to a VoiceLive realtime model directly, without the Foundry agent:
/// no agent instructions, no tools (no web search) and no conversation history. Used by
/// POST /api/chat/audio/realtime to check audio quality and latency of a realtime model.
/// </summary>
public interface IRealtimeVoiceTestService
{
    /// <param name="audio">What the user says as raw PCM16 / 24 kHz / mono, or null/empty for a spoken greeting.</param>
    /// <returns>The model's spoken answer as raw PCM16 / 24 kHz / mono, with its transcript.</returns>
    Task<RealtimeTestResponse> ConverseAsync(byte[]? audio, CancellationToken cancellationToken = default);
}
