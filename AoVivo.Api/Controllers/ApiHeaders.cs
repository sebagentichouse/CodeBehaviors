namespace AoVivo.Api.Controllers;

/// <summary>Custom response headers of the audio endpoints (also exposed through CORS in Program.cs).</summary>
public static class ApiHeaders
{
    /// <summary>Foundry conversation id of a voice turn with the agent (wav/pcm formats).</summary>
    public const string ConversationId = "X-Conversation-Id";

    /// <summary>The agent's answer as text, URL-encoded (wav/pcm formats).</summary>
    public const string AgentTranscript = "X-Agent-Transcript";

    /// <summary>TEST endpoint only: what the realtime model said, URL-encoded.</summary>
    public const string ModelTranscript = "X-Model-Transcript";

    /// <summary>TEST endpoint only: states that the answer came from a realtime model, not from the agent.</summary>
    public const string VoiceTarget = "X-Voice-Target";

    public static readonly string[] All = [ConversationId, AgentTranscript, ModelTranscript, VoiceTarget];
}
