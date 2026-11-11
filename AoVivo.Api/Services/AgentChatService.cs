using AoVivo.Api.Audio;
using AoVivo.Api.Models;
using AoVivo.Api.Options;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Responses;

namespace AoVivo.Api.Services;

/// <summary>
/// Web version of <c>AoVivo.VoiceLive.RunAsync()</c>: same Foundry agent calls, but
/// it takes the user's input and keeps the conversation between calls.
/// <para>
/// Both text and voice go through the agent's Responses endpoint with the service principal (the path that
/// works). For voice, <see cref="ISpeechService"/> only converts audio to text before the call and text to
/// audio after it, so the agent's instructions, tools (web search) and conversation history always apply.
/// </para>
/// Registered as a singleton: the SDK clients are thread-safe and meant to be reused.
/// </summary>
public sealed class AgentChatService : IAgentChatService
{
    private readonly AIProjectClient _projectClient;
    private readonly ProjectConversationsClient _conversationClient;
    private readonly ISpeechService _speech;
    private readonly AgentOptions _agent;
    private readonly ILogger<AgentChatService> _logger;

    public AgentChatService(
        AIProjectClient projectClient,
        ISpeechService speech,
        IOptions<AgentOptions> agentOptions,
        ILogger<AgentChatService> logger)
    {
        _projectClient = projectClient;
        _speech = speech;
        _agent = agentOptions.Value;
        _logger = logger;

        // Same as RunAsync: reuse the agent-scoped SDK pipeline for conversations.
        ProjectResponsesClient agentClient =
            _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(_agent.Name);

        _conversationClient = new ProjectConversationsClient(
            agentClient.Pipeline,
            new OpenAIClientOptions { Endpoint = agentClient.Endpoint });
    }

    public async Task<ChatResponse> ConverseAsync(string? message, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        bool isGreeting = string.IsNullOrWhiteSpace(message);
        string prompt = isGreeting ? _agent.GreetingPrompt : message!;

        string id = await EnsureConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);

        ProjectResponsesClient responseClient =
            _projectClient.ProjectOpenAIClient.GetProjectResponsesClientForAgentEndpoint(
                _agent.Name, defaultConversationId: id);

        var result = await responseClient
            .CreateResponseAsync(prompt, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        ResponseResult response = result.Value;

        _logger.LogInformation("Agent {Agent} answered in conversation {ConversationId} ({Kind})",
            _agent.Name, id, isGreeting ? "greeting" : "message");

        return new ChatResponse(id, response.GetOutputText() ?? string.Empty);
    }

    public async Task<AudioChatResponse> ConverseAudioAsync(byte[]? audio, string? conversationId = null, CancellationToken cancellationToken = default)
    {
        bool isGreeting = audio is null || audio.Length == 0;

        // 1) Speech -> text. No audio means "greeting": ConverseAsync sends the same GreetingPrompt as the text version.
        string? message = null;
        if (!isGreeting)
        {
            if (PcmAudio.Validate(audio!) is { } problem)
                throw new ArgumentException(problem, nameof(audio));

            message = await _speech.TranscribeAsync(audio!, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(message))
                throw new SpeechNotRecognizedException();
        }

        // 2) The agent answers exactly as for a typed message, in the same Foundry conversation.
        ChatResponse reply = await ConverseAsync(message, conversationId, cancellationToken).ConfigureAwait(false);

        // 3) Text -> speech, without the markdown, links and citations that only make sense on screen.
        string spoken = SpeechText.ForSpeech(reply.Reply);
        byte[] pcm = spoken.Length == 0
            ? Array.Empty<byte>()
            : await _speech.SynthesizeAsync(spoken, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent {Agent} answered by voice in conversation {ConversationId} ({Kind}, {Seconds:0.0}s of audio)",
            _agent.Name, reply.ConversationId, isGreeting ? "greeting" : "audio", pcm.Length / (double)PcmAudio.BytesPerSecond);

        return new AudioChatResponse(reply.ConversationId, pcm, reply.Reply);
    }

    /// <summary>Returns <paramref name="conversationId"/> (trimmed), or creates a new conversation when it is null or blank.</summary>
    private async Task<string> EnsureConversationAsync(string? conversationId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
            return conversationId.Trim();

        var conversation = await _conversationClient
            .CreateProjectConversationAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Created conversation {ConversationId} with agent {Agent}", conversation.Value.Id, _agent.Name);
        return conversation.Value.Id;
    }
}
