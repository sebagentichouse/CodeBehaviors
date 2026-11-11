// Client for AoVivo.Api (see AoVivo.Api/Controllers/ChatController.cs).
// URLs are relative: in development Vite proxies /api to the API (vite.config.ts).

export interface ChatReply {
  conversationId: string
  reply: string
}

export interface AudioReply {
  conversationId: string
  /** The agent's spoken answer as audio/wav (PCM16, 24 kHz, mono). */
  audio: Blob
  /** What the agent said (its answer as text). */
  transcript: string | null
}

/** An error response from the API (ASP.NET Core ProblemDetails) or a network failure. */
export class ApiError extends Error {
  readonly status: number
  readonly title: string
  readonly detail?: string

  constructor(status: number, title: string, detail?: string) {
    super(detail ? `${title}: ${detail}` : title)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
  }
}

/** Body of POST /api/chat/audio?format=json (AoVivo.Api Models/AudioChatJsonResponse.cs). */
interface AudioChatJson {
  conversationId?: string
  transcript?: string | null
  /** The spoken answer as a WAV file, base64. */
  wav?: string
}

/**
 * POST /api/chat. A null/blank message returns the agent's greeting.
 * Pass the returned conversationId to continue the conversation.
 */
export async function sendText(
  message: string | null,
  conversationId: string | null,
  signal?: AbortSignal,
): Promise<ChatReply> {
  const response = await request('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ message, conversationId }),
    signal,
  })
  return (await response.json()) as ChatReply
}

/**
 * POST /api/chat/audio. A null audio returns the agent's spoken greeting.
 * The audio must be WAV PCM16 / 24 kHz / mono (see audio/convert.ts). The API transcribes it, sends the text to
 * the agent (same conversation as the text turns) and reads the answer aloud.
 * Uses format=json: long answers (e.g. with web search) do not fit in a response header through the dev proxy.
 */
export async function sendAudio(
  wav: Blob | null,
  conversationId: string | null,
  signal?: AbortSignal,
): Promise<AudioReply> {
  const query = new URLSearchParams({ format: 'json' })
  if (conversationId) query.set('conversationId', conversationId)

  const response = await request(`/api/chat/audio?${query}`, {
    method: 'POST',
    headers: wav ? { 'Content-Type': 'audio/wav', Accept: 'application/json' } : { Accept: 'application/json' },
    body: wav ?? undefined,
    signal,
  })

  let body: AudioChatJson
  try {
    body = (await response.json()) as AudioChatJson
  } catch {
    throw new ApiError(response.status, 'Invalid response', 'The audio answer is not valid JSON.')
  }
  if (!body.conversationId) {
    throw new ApiError(response.status, 'Invalid response', 'The conversationId is missing.')
  }
  return {
    conversationId: body.conversationId,
    audio: base64ToBlob(body.wav ?? '', 'audio/wav'),
    transcript: body.transcript || null,
  }
}

async function request(url: string, init: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, 'API unreachable', 'Is AoVivo.Api running? (dotnet run --project AoVivo.Api)')
  }
  if (!response.ok) throw await toApiError(response)
  return response
}

async function toApiError(response: Response): Promise<ApiError> {
  const text = await response.text().catch(() => '')
  try {
    const problem = JSON.parse(text) as { title?: string; detail?: string; errors?: Record<string, string[]> }
    const validation = problem.errors ? Object.values(problem.errors).flat().join(' ') : undefined
    return new ApiError(response.status, problem.title ?? `HTTP ${response.status}`, problem.detail ?? validation)
  } catch {
    // Not JSON. An empty 5xx comes from the dev proxy when the API is down (Vite answers 502);
    // the API's own errors always carry a ProblemDetails body.
    const proxyDown = response.status >= 500 && text.trim() === ''
    return new ApiError(
      response.status,
      proxyDown ? 'API unreachable' : `HTTP ${response.status} ${response.statusText}`.trim(),
      proxyDown ? 'Is AoVivo.Api running? (dotnet run --project AoVivo.Api)' : text || undefined,
    )
  }
}

function base64ToBlob(base64: string, type: string): Blob {
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i)
  return new Blob([bytes], { type })
}
