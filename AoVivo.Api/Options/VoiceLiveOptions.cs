using System.ComponentModel.DataAnnotations;

namespace AoVivo.Api.Options;

/// <summary>
/// Settings of the VoiceLive realtime model used ONLY by the test endpoint POST /api/chat/audio/realtime
/// (section "VoiceLive"). That endpoint does not use the Foundry agent: no agent instructions, no tools
/// (no web search) and no conversation history.
/// </summary>
public sealed class VoiceLiveOptions
{
    public const string SectionName = "VoiceLive";

    /// <summary>Foundry resource endpoint, e.g. https://&lt;resource&gt;.services.ai.azure.com/.</summary>
    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Realtime model of the test sessions.</summary>
    [Required]
    public string RealtimeModel { get; set; } = "gpt-realtime";

    /// <summary>
    /// Instructions of the test sessions. The model has no tools, so it is told not to offer what it cannot do
    /// (otherwise it offers to "check the latest news" and then cannot).
    /// </summary>
    [Required]
    public string RealtimeInstructions { get; set; } =
        "You are a voice assistant used to test audio. You have no tools and no internet access, so never offer " +
        "to search, browse or check the latest news. When a question needs current information, say that you " +
        "cannot access it. Keep your answers short.";

    /// <summary>Maximum time to wait for the model's spoken answer.</summary>
    [Range(5, 600)]
    public int ResponseTimeoutSeconds { get; set; } = 60;
}
