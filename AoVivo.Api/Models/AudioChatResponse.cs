namespace AoVivo.Api.Models;

/// <summary>A voice turn with the agent.</summary>
/// <param name="ConversationId">Foundry conversation id. Send it back to continue the conversation (text and voice turns can be mixed).</param>
/// <param name="Audio">The agent's spoken answer (or greeting): raw PCM16, 24 kHz, mono.</param>
/// <param name="Transcript">The agent's answer as text (exactly what was converted to speech, before markdown cleanup).</param>
public sealed record AudioChatResponse(string ConversationId, byte[] Audio, string Transcript);
