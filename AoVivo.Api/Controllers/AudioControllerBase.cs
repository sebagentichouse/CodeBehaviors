using System.ClientModel;
using System.Net.WebSockets;
using AoVivo.Api.Audio;
using AoVivo.Api.Services;
using Azure;
using Azure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AoVivo.Api.Controllers;

/// <summary>
/// Shared by the chat and the test controllers: reads audio request bodies, writes audio responses and
/// turns failures of the Azure services into ProblemDetails responses.
/// </summary>
public abstract class AudioControllerBase : ControllerBase
{
    /// <summary>24 MB: about 8.7 minutes of PCM16 24 kHz mono, under the 25 MB limit of the transcription API.</summary>
    public const long MaxAudioRequestBytes = 24 * 1024 * 1024;

    protected enum AudioFormat { Wav, Pcm, Json }

    /// <summary>Parses the <c>format</c> query parameter (wav by default; json only where supported).</summary>
    protected static bool TryParseFormat(string? format, bool allowJson, out AudioFormat result)
    {
        switch (format?.Trim().ToLowerInvariant())
        {
            case null or "" or "wav":
                result = AudioFormat.Wav;
                return true;
            case "pcm":
                result = AudioFormat.Pcm;
                return true;
            case "json" when allowJson:
                result = AudioFormat.Json;
                return true;
            default:
                result = default;
                return false;
        }
    }

    protected ObjectResult InvalidFormat(string detail) =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid format", detail: detail);

    /// <summary>
    /// Reads the request body: WAV or raw PCM (both PCM16, 24 kHz, mono). An empty body gives
    /// <c>Pcm = null</c> (greeting). <c>Problem</c> is set when the audio cannot be used.
    /// </summary>
    protected async Task<(byte[]? Pcm, ObjectResult? Problem)> ReadAudioAsync(CancellationToken cancellationToken)
    {
        byte[] input;
        try
        {
            using var body = new MemoryStream();
            await Request.Body.CopyToAsync(body, cancellationToken);
            input = body.ToArray();
        }
        catch (BadHttpRequestException ex) // e.g. larger than MaxAudioRequestBytes
        {
            return (null, Problem(statusCode: ex.StatusCode, title: "Invalid request body", detail: ex.Message));
        }

        if (input.Length == 0)
            return (null, null);

        byte[] pcm;
        try
        {
            pcm = PcmAudio.IsWav(input) ? PcmAudio.FromWav(input) : input;
        }
        catch (InvalidDataException ex)
        {
            return (null, Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid audio", detail: ex.Message));
        }

        if (PcmAudio.Validate(pcm) is { } problem)
            return (null, Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid audio", detail: problem));

        return (pcm, null);
    }

    /// <summary>Returns PCM16 / 24 kHz / mono samples as a WAV file or as raw PCM.</summary>
    protected FileContentResult AudioFile(byte[] pcm, AudioFormat format) =>
        format == AudioFormat.Pcm
            ? File(pcm, "application/octet-stream")
            : File(PcmAudio.ToWav(pcm), "audio/wav");

    /// <summary>Null for a null or blank id (= start a new conversation), otherwise the trimmed id.</summary>
    protected static string? NormalizeConversationId(string? conversationId) =>
        string.IsNullOrWhiteSpace(conversationId) ? null : conversationId.Trim();

    protected static bool IsServiceError(Exception ex) =>
        ex is ClientResultException or RequestFailedException or AuthenticationFailedException
            or SpeechServiceException or SpeechNotRecognizedException
            or VoiceSessionException or WebSocketException or TimeoutException;

    /// <summary>Turns a failure of the agent / Azure services into a ProblemDetails response.</summary>
    /// <param name="conversationId">The conversation the caller asked for (null when it asked for a new one).</param>
    /// <param name="defaultTitle">Title of unexpected service errors.</param>
    protected ObjectResult ServiceError(Exception ex, string? conversationId, ILogger logger, string defaultTitle = "Agent service error")
    {
        switch (ex)
        {
            case SpeechNotRecognizedException:
                logger.LogInformation("No speech recognized in the user's audio");
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "No speech recognized", detail: ex.Message);

            // Checked before the 404 cases below: a missing audio deployment is not a missing conversation.
            case SpeechServiceException:
                logger.LogError(ex, "Speech service error");
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Speech service error", detail: ex.Message);

            case ClientResultException { Status: StatusCodes.Status404NotFound } when conversationId is not null:
            case RequestFailedException { Status: StatusCodes.Status404NotFound } when conversationId is not null:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Conversation not found",
                    detail: $"Conversation '{conversationId}' does not exist. Omit conversationId to start a new one.");

            case ClientResultException { Status: StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden }:
            case RequestFailedException { Status: StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden }:
                logger.LogError(ex, "Azure denied access to the service principal");
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Access denied by Azure",
                    detail: $"Check the roles of the service principal. {ex.Message}");

            case AuthenticationFailedException:
                logger.LogError(ex, "Could not authenticate against Azure");
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Azure authentication failed",
                    detail: "Check the AzureAd settings (TenantId, ClientId, ClientSecret).");

            case TimeoutException:
                logger.LogWarning(ex, "Service timed out");
                return Problem(statusCode: StatusCodes.Status504GatewayTimeout, title: "Timeout", detail: ex.Message);

            default:
                logger.LogError(ex, "{Title}", defaultTitle);
                return Problem(statusCode: StatusCodes.Status502BadGateway, title: defaultTitle, detail: ex.Message);
        }
    }
}
