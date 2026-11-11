using System.ComponentModel.DataAnnotations;

namespace AoVivo.Api.Options;

/// <summary>
/// Azure OpenAI audio models that turn the user's speech into text and the agent's answer into speech
/// (section "Speech"). Both must be deployed in the Foundry resource.
/// </summary>
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>Azure OpenAI endpoint of the Foundry resource, e.g. https://&lt;resource&gt;.openai.azure.com/.</summary>
    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Deployment name of the speech-to-text model (e.g. gpt-4o-mini-transcribe, gpt-4o-transcribe, whisper).</summary>
    [Required]
    public string TranscriptionDeployment { get; set; } = "gpt-4o-mini-transcribe";

    /// <summary>
    /// Optional ISO-639-1 language of the user's speech ("en", "pt", ...). It improves accuracy and speed;
    /// leave empty to let the model detect it.
    /// </summary>
    public string? TranscriptionLanguage { get; set; }

    /// <summary>Deployment name of the text-to-speech model (e.g. gpt-4o-mini-tts, tts, tts-hd).</summary>
    [Required]
    public string SpeechDeployment { get; set; } = "gpt-4o-mini-tts";

    /// <summary>Voice of the spoken answers (alloy, ash, coral, echo, fable, nova, onyx, sage, shimmer, ...).</summary>
    [Required]
    public string Voice { get; set; } = "alloy";
}
