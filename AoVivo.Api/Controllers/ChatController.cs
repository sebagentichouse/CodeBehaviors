using AoVivo.Api.Audio;
using AoVivo.Api.Models;
using AoVivo.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AoVivo.Api.Controllers;

/// <summary>
/// Text and voice turns with the Foundry agent. Both use the same agent (instructions, tools such as web search)
/// and the same Foundry conversation, so they can be mixed.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Tags("Chat with the agent")]
public sealed class ChatController : AudioControllerBase
{
    private readonly IAgentChatService _chat;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IAgentChatService chat, ILogger<ChatController> logger)
    {
        _chat = chat;
        _logger = logger;
    }

    /// <summary>
    /// Talks to the agent by text.
    /// No body, or a null/blank "message": returns the agent's greeting.
    /// A "message": returns the agent's answer.
    /// Send back the returned "conversationId" to continue the same conversation.
    /// </summary>
    [HttpPost]
    [Produces("application/json")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChatRequest? request,
        CancellationToken cancellationToken)
    {
        string? conversationId = NormalizeConversationId(request?.ConversationId);
        try
        {
            return Ok(await _chat.ConverseAsync(request?.Message, conversationId, cancellationToken));
        }
        catch (Exception ex) when (IsServiceError(ex))
        {
            return ServiceError(ex, conversationId, _logger);
        }
    }

    /// <summary>
    /// Talks to the agent by voice: the audio is transcribed, answered by the agent like a typed message,
    /// and the answer is read aloud.
    /// Body: what the user says, as a WAV file or raw PCM (both PCM16, 24 kHz, mono).
    /// Empty body: returns the agent's spoken greeting.
    /// Response, by <c>format</c>:
    /// <c>wav</c> (default) or <c>pcm</c>: the spoken answer, with the conversation id in the X-Conversation-Id
    /// header and the answer's text (URL-encoded) in X-Agent-Transcript;
    /// <c>json</c>: { conversationId, transcript, wav (base64) }, for browsers and long answers.
    /// Pass the conversation id as <c>?conversationId=</c> to continue the conversation (text and audio turns can be mixed).
    /// </summary>
    [HttpPost("audio")]
    [RequestSizeLimit(MaxAudioRequestBytes)]
    [ProducesResponseType<AudioChatJsonResponse>(StatusCodes.Status200OK, "application/json", "audio/wav", "application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> PostAudio(
        [FromQuery] string? conversationId,
        [FromQuery] string format = "wav",
        CancellationToken cancellationToken = default)
    {
        if (!TryParseFormat(format, allowJson: true, out AudioFormat audioFormat))
            return InvalidFormat("Use format=wav (default), format=pcm or format=json.");

        var (pcm, invalidAudio) = await ReadAudioAsync(cancellationToken);
        if (invalidAudio is not null)
            return invalidAudio;

        string? id = NormalizeConversationId(conversationId);
        try
        {
            AudioChatResponse reply = await _chat.ConverseAudioAsync(pcm, id, cancellationToken);

            if (audioFormat == AudioFormat.Json)
                return Ok(new AudioChatJsonResponse(reply.ConversationId, reply.Transcript, PcmAudio.ToWav(reply.Audio)));

            Response.Headers[ApiHeaders.ConversationId] = reply.ConversationId;
            if (!string.IsNullOrEmpty(reply.Transcript))
                Response.Headers[ApiHeaders.AgentTranscript] = Uri.EscapeDataString(reply.Transcript); // headers must be ASCII

            return AudioFile(reply.Audio, audioFormat);
        }
        catch (Exception ex) when (IsServiceError(ex))
        {
            return ServiceError(ex, id, _logger);
        }
    }
}
