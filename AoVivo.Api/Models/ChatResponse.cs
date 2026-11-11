namespace AoVivo.Api.Models;

/// <param name="ConversationId">Send it back in the next request to keep the conversation going.</param>
/// <param name="Reply">The agent's answer (or greeting).</param>
public sealed record ChatResponse(string ConversationId, string Reply);
