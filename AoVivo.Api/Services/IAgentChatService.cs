using AoVivo.Api.Models;

namespace AoVivo.Api.Services;

/// <summary>
/// Talks to the Foundry agent. Text and voice turns go to the same agent (same instructions and tools,
/// e.g. web search) and can be mixed in the same Foundry conversation.
/// </summary>
public interface IAgentChatService
{
    /// <summary>
    /// Talks to the agent by text. Replaces the console app's <c>VoiceLive.RunAsync()</c>.
    /// </summary>
    /// <param name="message">Null or blank: returns the agent's greeting. Otherwise: the agent's answer to the message.</param>
    /// <param name="conversationId">Existing Foundry conversation to continue; null or blank creates a new conversation.</param>
    Task<ChatResponse> ConverseAsync(string? message, string? conversationId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Talks to the agent by voice: the audio is transcribed, sent to the agent exactly like a text message
    /// (<see cref="ConverseAsync"/>), and the agent's answer is converted to speech.
    /// </summary>
    /// <param name="audio">
    /// What the user says, as raw PCM16 / 24 kHz / mono (at least 100 ms).
    /// Null or empty: returns the agent's spoken greeting.
    /// </param>
    /// <param name="conversationId">Existing Foundry conversation to continue; null or blank creates a new conversation.</param>
    /// <returns>The agent's spoken answer as raw PCM16 / 24 kHz / mono, with its text.</returns>
    /// <exception cref="SpeechNotRecognizedException">No speech was recognized in <paramref name="audio"/>.</exception>
    /// <exception cref="SpeechServiceException">The speech-to-text or text-to-speech deployment failed.</exception>
    Task<AudioChatResponse> ConverseAudioAsync(byte[]? audio, string? conversationId = null, CancellationToken cancellationToken = default);
}
