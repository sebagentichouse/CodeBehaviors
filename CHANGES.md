# AoVivoChat – voice through the agent (option B)

## What changed in behavior

| | Before | Now |
|---|---|---|
| `POST /api/chat/audio` | Went to the `gpt-realtime` model (both voice routes had `useRealtimeModel: true`); the agent was never called | Speech → text (Azure OpenAI transcription) → **chat-agent** via the same Responses call as text → text → speech (Azure OpenAI TTS) |
| Web search, agent instructions | Not available by voice | Available by voice (the agent gets the transcribed text exactly like a typed message) |
| Conversation | Voice turns were not stored; made-up `rt_<guid>` ids caused a 404 when text followed a voice-first conversation | Text and voice share the same Foundry conversation |
| `POST /api/chat/audio/realtime` | Same as `/audio` | **TEST ONLY**: realtime model, no agent, no conversation; Development environment only; says so in Swagger, in the `X-Voice-Target` header and in the model's instructions |
| Long answers in the UI | Answer text in an `X-Agent-Transcript` header (the Vite proxy fails above 16 KB of headers) | The React app uses `format=json`: `{ conversationId, transcript, wav }` |

## Setup before running

1. **Deploy two audio models** in `presente-resource` (Foundry portal → *Models + endpoints* → *Deploy model*):
   - speech-to-text: `gpt-4o-mini-transcribe` (or `gpt-4o-transcribe` / `whisper`)
   - text-to-speech: `gpt-4o-mini-tts` (or `tts` / `tts-hd`)

   If your region does not offer them, deploy the alternatives and put the **deployment names** in
   `appsettings.json` → `Speech:TranscriptionDeployment` / `Speech:SpeechDeployment`.
2. **Endpoint**: `Speech:Endpoint` is `https://presente-resource.openai.azure.com/` (the
   `https://presente-resource.cognitiveservices.azure.com/` form also works).
3. **Permissions**: the service principal's *Foundry User* role should be enough. If a voice turn returns
   *"Speech service error … (HTTP 401/403)"*, add **Cognitive Services OpenAI User** on the resource.
4. Optional: `Speech:TranscriptionLanguage` = `"pt"` or `"en"` if you always speak the same language.
5. The client secret is still read from user secrets (`AzureAd:ClientSecret`); nothing changed there.

## Files – AoVivo.Api

| File | Change |
|---|---|
| `Program.cs` | Removed `var segredo = ad.ClientSecret;`. Registers `AzureOpenAIClient` (same service principal), `ISpeechService`, `IRealtimeVoiceTestService`. CORS exposes all custom headers from `ApiHeaders`. |
| `appsettings.json` | New `Speech` section; `VoiceLive:RealtimeModel`; removed `Agent:ProjectName` (only VoiceLive agent mode used it); fixed the trailing comma in `AzureAd`. |
| `appsettings.Development.json` | `AoVivo.Api.Services` logs at `Debug` (user transcripts, VoiceLive event sequence). |
| `Options/SpeechOptions.cs` | **New.** Endpoint, deployments, language, voice. |
| `Options/VoiceLiveOptions.cs` | Now only for the test endpoint; new `RealtimeInstructions` (tells the model it has no tools, so it stops offering to "check the latest news"). |
| `Options/AgentOptions.cs` | Removed `ProjectName`. |
| `Services/AgentChatService.cs` | `ConverseAudioAsync` = transcribe → `ConverseAsync` → synthesize. VoiceLive code moved out. Blank conversation ids are treated as "new". |
| `Services/IAgentChatService.cs` | Only the agent methods (text and voice). |
| `Services/SpeechService.cs`, `ISpeechService.cs` | **New.** Azure OpenAI transcription and TTS (PCM 24 kHz, the same format as before). Long answers are synthesized in pieces and joined. Failures become `SpeechServiceException`. |
| `Services/RealtimeVoiceTestService.cs`, `IRealtimeVoiceTestService.cs` | **New.** The former realtime-model code, test only, no conversation id, and it logs every VoiceLive event plus all fields of a VoiceLive error. |
| `Services/SpeechServiceException.cs`, `SpeechNotRecognizedException.cs`, `VoiceSessionException.cs` | **New.** Replace `AgentVoiceException.cs` (deleted). |
| `Audio/SpeechText.cs` | **New.** Removes markdown, links, URLs and web-search citations before text-to-speech; splits long text at sentence boundaries. The UI still shows the original text. |
| `Controllers/ChatController.cs` | `/audio` really goes to the agent; new `format=json`; 422 when no speech is recognized. |
| `Controllers/RealtimeTestController.cs` | **New.** `POST /api/chat/audio/realtime`, labeled *TEST ONLY* in Swagger, 404 outside Development, headers `X-Model-Transcript` and `X-Voice-Target: realtime-model=…; agent=none`. |
| `Controllers/AudioControllerBase.cs` | **New.** Shared body reading, audio responses and error mapping. A missing audio deployment is no longer reported as "Conversation not found"; 401/403 → "Access denied by Azure"; too-large body → 413. |
| `Controllers/ApiHeaders.cs` | **New.** Header names in one place. |
| `Models/AudioChatJsonResponse.cs`, `RealtimeTestResponse.cs` | **New.** |
| `Models/AudioChatResponse.cs` | Doc fixed (conversation id is a real Foundry id again). |
| `AoVivo.Api.http` | Voice requests explained; `format=json`; realtime test requests in their own section. |

Request limit lowered from 25 MB to 24 MB: the WAV sent to the transcription API must stay under its 25 MB limit.

## Files – AoVivo.Web

| File | Change |
|---|---|
| `src/api/chatApi.ts` | `sendAudio` calls `/api/chat/audio?format=json` and decodes the base64 WAV. `AudioReply` is unchanged, so `App.tsx` did not change. |
| `src/api/chatApi.test.ts` | `sendAudio` tests for the JSON answer, plus the 422 "No speech recognized" case. |
| `README.md` | Describes the voice flow, `format=json` and the test endpoint. |

## Suggested tests

1. Text greeting, then spoken greeting in the same conversation: both should sound like the same agent.
2. By voice: *"What is the latest news about …?"*: a current answer (web search). The text shows the
   citations; the audio does not read the URLs.
3. Type *"My name is Ana"*, then ask by voice *"What is my name?"*: the agent remembers (shared conversation).
4. **New conversation**, start with voice, then type: no more 404.
5. Record silence: *"No speech recognized"* (HTTP 422).
6. `AoVivo.Api.http` requests 7 and 8: the realtime test endpoint; check the `X-Voice-Target` header.
7. `npm test` and `npm run typecheck` in `AoVivo.Web`.
