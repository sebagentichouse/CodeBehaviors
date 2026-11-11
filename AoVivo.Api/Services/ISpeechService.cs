namespace AoVivo.Api.Services;

/// <summary>Speech-to-text and text-to-speech with the Azure OpenAI audio deployments (section "Speech").</summary>
public interface ISpeechService
{
    /// <summary>Transcribes what the user said.</summary>
    /// <param name="pcm">Raw PCM16 / 24 kHz / mono.</param>
    /// <returns>The recognized text; empty when nothing was recognized.</returns>
    /// <exception cref="SpeechServiceException">The transcription deployment failed.</exception>
    Task<string> TranscribeAsync(byte[] pcm, CancellationToken cancellationToken = default);

    /// <summary>Reads <paramref name="text"/> aloud. Long texts are synthesized in several requests and joined.</summary>
    /// <returns>Raw PCM16 / 24 kHz / mono.</returns>
    /// <exception cref="SpeechServiceException">The text-to-speech deployment failed.</exception>
    Task<byte[]> SynthesizeAsync(string text, CancellationToken cancellationToken = default);
}
