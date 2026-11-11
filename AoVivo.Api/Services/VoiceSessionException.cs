namespace AoVivo.Api.Services;

/// <summary>The VoiceLive test session reported an error or ended before the model answered.</summary>
public sealed class VoiceSessionException(string message) : Exception(message);
