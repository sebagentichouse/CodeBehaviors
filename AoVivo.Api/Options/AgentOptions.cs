using System.ComponentModel.DataAnnotations;

namespace AoVivo.Api.Options;

/// <summary>Settings of the Azure AI Foundry agent (section "Agent").</summary>
public sealed class AgentOptions
{
    public const string SectionName = "Agent";

    /// <summary>Foundry project endpoint, e.g. https://&lt;resource&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    [Required, Url]
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>Name of the agent in the Foundry project. Text and voice turns both go to this agent.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Message sent to the agent when the caller sends no message (or no audio), to get its greeting.</summary>
    [Required]
    public string GreetingPrompt { get; set; } = "Hi chat-agent";
}
