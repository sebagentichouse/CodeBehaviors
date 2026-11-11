namespace AoVivo.Api.Models;

/// <summary>Answer of the realtime model test endpoint (no agent, no conversation).</summary>
/// <param name="Model">The VoiceLive realtime model that answered.</param>
/// <param name="Audio">The model's spoken answer: raw PCM16, 24 kHz, mono.</param>
/// <param name="Transcript">Text of what the model said, when the service provides it.</param>
public sealed record RealtimeTestResponse(string Model, byte[] Audio, string? Transcript);
