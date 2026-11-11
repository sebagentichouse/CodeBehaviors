namespace AoVivo.Api.Services;

/// <summary>The user's audio was transcribed to nothing (silence, noise, or too quiet).</summary>
public sealed class SpeechNotRecognizedException()
    : Exception("No speech was recognized in the audio. Speak closer to the microphone or send a longer recording.");
