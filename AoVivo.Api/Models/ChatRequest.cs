namespace AoVivo.Api.Models;

/// <param name="Message">What the user asks. Null or blank returns the agent's greeting.</param>
/// <param name="ConversationId">Id returned by a previous call, to continue that conversation. Null starts a new one.</param>
public sealed record ChatRequest(string? Message, string? ConversationId);
