using AoVivo.Api.Controllers;
using AoVivo.Api.Options;
using AoVivo.Api.Services;
using Azure.AI.OpenAI;
using Azure.AI.Projects;
using Azure.AI.VoiceLive;
using Azure.Identity;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Configuration: appsettings.json + user secrets (Development) + environment variables.
// Validated at startup so a missing setting fails fast instead of on the first request.
builder.Services.AddOptions<AgentOptions>()
    .Bind(builder.Configuration.GetSection(AgentOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SpeechOptions>()
    .Bind(builder.Configuration.GetSection(SpeechOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<VoiceLiveOptions>()
    .Bind(builder.Configuration.GetSection(VoiceLiveOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AzureAdOptions>()
    .Bind(builder.Configuration.GetSection(AzureAdOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// One credential (the service principal) for every Azure client of the app.
builder.Services.AddSingleton(sp =>
{
    var ad = sp.GetRequiredService<IOptions<AzureAdOptions>>().Value;
    return new ClientSecretCredential(ad.TenantId, ad.ClientId, ad.ClientSecret);
});

// Foundry project: the agent, for text and voice turns (same as CredentialToken + RunAsync).
builder.Services.AddSingleton(sp =>
{
    var agent = sp.GetRequiredService<IOptions<AgentOptions>>().Value;
    return new AIProjectClient(
        endpoint: new Uri(agent.ProjectEndpoint),
        tokenProvider: sp.GetRequiredService<ClientSecretCredential>());
});

// Azure OpenAI audio deployments: speech-to-text before the agent, text-to-speech after it.
builder.Services.AddSingleton(sp =>
{
    var speech = sp.GetRequiredService<IOptions<SpeechOptions>>().Value;
    return new AzureOpenAIClient(new Uri(speech.Endpoint), sp.GetRequiredService<ClientSecretCredential>());
});

// VoiceLive: ONLY for the test endpoint POST /api/chat/audio/realtime (realtime model, no agent).
builder.Services.AddSingleton(sp =>
{
    var voice = sp.GetRequiredService<IOptions<VoiceLiveOptions>>().Value;
    return new VoiceLiveClient(new Uri(voice.Endpoint), sp.GetRequiredService<ClientSecretCredential>());
});

builder.Services.AddSingleton<ISpeechService, SpeechService>();
builder.Services.AddSingleton<IAgentChatService, AgentChatService>();
builder.Services.AddSingleton<IRealtimeVoiceTestService, RealtimeVoiceTestService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend",
        policy =>
        {
            policy
                .WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()
                // Lets a browser that calls the API directly (not through the Vite proxy) read the custom headers.
                .WithExposedHeaders(ApiHeaders.All);
        });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("Frontend");

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
