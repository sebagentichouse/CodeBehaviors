namespace AoVivo.Api.Models;

/// <summary>
/// Body of POST /api/chat/audio?format=json. Used by the React app: long answers (for example with web search)
/// do not fit in a response header, and the Vite dev proxy rejects responses with more than 16 KB of headers.
/// </summary>
/// <param name="ConversationId">Foundry conversation id. Send it back to continue the conversation.</param>
/// <param name="Transcript">The agent's answer as text.</param>
/// <param name="Wav">The agent's spoken answer as a WAV file (PCM16, 24 kHz, mono), base64 in JSON.</param>
public sealed record AudioChatJsonResponse(string ConversationId, string Transcript, byte[] Wav);
