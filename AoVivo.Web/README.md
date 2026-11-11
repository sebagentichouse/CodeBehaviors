# AoVivo.Web – React test client for AoVivo.Api

A small chat UI to try every endpoint of `AoVivo.Api`:

| UI action | API call |
|---|---|
| **👋 Text greeting** | `POST /api/chat` with `message: null` |
| Type a message + **Send** | `POST /api/chat` with `message` |
| **🔊 Spoken greeting** | `POST /api/chat/audio?format=json` with an empty body |
| **🎤 Talk** (click to start, click ■ to send) | `POST /api/chat/audio?format=json` with the recording |
| **📁 Send audio file** | `POST /api/chat/audio?format=json` with the file |

All of them talk to the same Foundry agent and share the **conversation id** shown at the top. For voice, the API
transcribes the audio, sends the text to the agent exactly like a typed message (so the agent's instructions and
tools, such as web search, apply) and reads the answer aloud. The conversation id is filled in by the first answer,
and you can edit it or paste an id to continue an existing conversation. **New conversation** clears it. The
agent's spoken answers play automatically and show their text.

Voice calls use `format=json` (`{ conversationId, transcript, wav }`, the WAV in base64) instead of the
`X-Conversation-Id` / `X-Agent-Transcript` headers: answers with web search can be long, and the Vite dev proxy
rejects responses with more than 16 KB of headers.

The API's test endpoint `POST /api/chat/audio/realtime` (realtime model, **no agent**) is intentionally not in
this UI; use `AoVivo.Api/AoVivo.Api.http` to try it.

Recordings and files are converted in the browser to what the API expects (WAV PCM16, 24 kHz, mono), so any
format the browser can decode works (webm, mp3, m4a, wav at any rate, stereo, ...).

## Run

Requires Node.js 20.19+ (or 22.12+).

```bash
# terminal 1 – the API (http profile, port 5080)
dotnet run --project AoVivo.Api --launch-profile http

# terminal 2 – this app
cd AoVivo.Web
npm install
npm run dev        # http://localhost:5173
```

**Visual Studio:** the solution includes `AoVivo.Web.esproj`. Right-click the solution → *Configure Startup
Projects…* → *Multiple startup projects* → start **AoVivo.Api** (profile `http`) and **AoVivo.Web**.

### How it reaches the API

The app calls relative URLs (`/api/...`), and the Vite dev server proxies them to `http://localhost:5080`.
The API also allows CORS from `http://localhost:5173` (and exposes its custom headers) for browsers that call it
directly. To target another address (for example the `https` profile), set `API_TARGET`:

```bash
API_TARGET=https://localhost:7080 npm run dev     # or put API_TARGET=... in .env.local
```

The microphone needs a secure context: `http://localhost` works, but other hosts need https.

## Scripts

| Script | |
|---|---|
| `npm run dev` | dev server with the API proxy |
| `npm test` | unit tests (Vitest + Testing Library) |
| `npm run typecheck` | TypeScript check |
| `npm run build` / `npm run preview` | production build in `dist/` / serve it (also proxied) |

## Code map

- `src/api/chatApi.ts` – typed client for the text and voice endpoints; turns ProblemDetails into `ApiError`
- `src/audio/wav.ts` – WAV encoding, mono mixdown, float → PCM16 (pure, unit tested)
- `src/audio/convert.ts` – decode any audio in the browser and resample it to 24 kHz mono
- `src/audio/useRecorder.ts` – microphone recording hook (MediaRecorder)
- `src/App.tsx` – conversation state; `src/components/` – header bar, message list, composer
