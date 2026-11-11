using System.ComponentModel.DataAnnotations;

namespace AoVivo.Api.Options;

/// <summary>Service principal used to call the agent (section "AzureAd").</summary>
public sealed class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    [Required]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Keep it out of appsettings.json: use user secrets or environment variables.</summary>
    [Required(ErrorMessage = "AzureAd:ClientSecret is missing. Set it with 'dotnet user-secrets' or the AzureAd__ClientSecret environment variable.")]
    public string ClientSecret { get; set; } = string.Empty;
}
