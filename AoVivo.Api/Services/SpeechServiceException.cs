namespace AoVivo.Api.Services;

/// <summary>
/// A call to an Azure OpenAI audio deployment (speech-to-text or text-to-speech) failed.
/// Kept apart from agent errors so that, for example, a missing deployment (HTTP 404) is not
/// reported as "Conversation not found".
/// </summary>
public sealed class SpeechServiceException(string message, Exception innerException)
    : Exception(message, innerException);
