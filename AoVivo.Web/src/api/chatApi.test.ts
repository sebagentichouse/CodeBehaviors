import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, sendAudio, sendText } from './chatApi'

const fetchMock = vi.fn<typeof fetch>()

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
})

const json = (body: unknown, status = 200, contentType = 'application/json') =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': contentType } })

describe('sendText', () => {
  it('posts the message and conversation id and returns the reply', async () => {
    fetchMock.mockResolvedValue(json({ conversationId: 'conv_1', reply: 'Hello!' }))

    const reply = await sendText('Hi', 'conv_1')

    expect(reply).toEqual({ conversationId: 'conv_1', reply: 'Hello!' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/chat')
    expect(init?.method).toBe('POST')
    expect(JSON.parse(init?.body as string)).toEqual({ message: 'Hi', conversationId: 'conv_1' })
  })

  it('sends nulls to ask for the greeting in a new conversation', async () => {
    fetchMock.mockResolvedValue(json({ conversationId: 'conv_new', reply: 'Welcome' }))
    await sendText(null, null)
    expect(JSON.parse(fetchMock.mock.calls[0][1]?.body as string)).toEqual({ message: null, conversationId: null })
  })

  it('turns ProblemDetails into an ApiError', async () => {
    fetchMock.mockResolvedValue(
      json({ title: 'Conversation not found', detail: "Conversation 'x' does not exist.", status: 404 }, 404, 'application/problem+json'),
    )

    const error = await sendText('Hi', 'x').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 404, title: 'Conversation not found', detail: "Conversation 'x' does not exist." })
  })

  it('includes validation errors', async () => {
    fetchMock.mockResolvedValue(json({ title: 'One or more validation errors occurred.', errors: { message: ['Too long.'] } }, 400))
    await expect(sendText('Hi', null)).rejects.toMatchObject({ status: 400, detail: 'Too long.' })
  })

  it('reports an unreachable API (network error or empty proxy 500)', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await expect(sendText('Hi', null)).rejects.toMatchObject({ status: 0, title: 'API unreachable' })

    fetchMock.mockResolvedValueOnce(new Response('', { status: 502, headers: { 'Content-Type': 'text/plain' } }))
    await expect(sendText('Hi', null)).rejects.toMatchObject({ status: 502, title: 'API unreachable' })
  })

  it('keeps the API\'s own 502 ProblemDetails distinct from an unreachable API', async () => {
    fetchMock.mockResolvedValue(json({ title: 'Agent service error', detail: 'Service unavailable' }, 502, 'application/problem+json'))
    await expect(sendText('Hi', null)).rejects.toMatchObject({ status: 502, title: 'Agent service error' })
  })

  it('shows non-JSON error bodies as text', async () => {
    fetchMock.mockResolvedValue(new Response('Request body too large.', { status: 413, statusText: 'Payload Too Large' }))
    await expect(sendText('Hi', null)).rejects.toMatchObject({ status: 413, detail: 'Request body too large.' })
  })
})

describe('sendAudio', () => {
  /** A WAV of `bytes` zero bytes, base64 (as in the API's JSON answer). */
  const base64Of = (bytes: number) => btoa(String.fromCharCode(...new Uint8Array(bytes)))

  it('posts the WAV with the conversation id and reads the JSON answer', async () => {
    fetchMock.mockResolvedValue(
      json({ conversationId: 'conv_1', transcript: 'Olá! Como posso ajudar?', wav: base64Of(44 + 4800) }),
    )
    const wav = new Blob([new Uint8Array(100)], { type: 'audio/wav' })

    const reply = await sendAudio(wav, 'conv 1/x')

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/chat/audio?format=json&conversationId=conv+1%2Fx')
    expect(init?.body).toBe(wav)
    expect(init?.headers).toEqual({ 'Content-Type': 'audio/wav', Accept: 'application/json' })
    expect(reply.conversationId).toBe('conv_1')
    expect(reply.transcript).toBe('Olá! Como posso ajudar?')
    expect(reply.audio.size).toBe(44 + 4800)
    expect(reply.audio.type).toBe('audio/wav')
  })

  it('sends an empty body to ask for the spoken greeting', async () => {
    fetchMock.mockResolvedValue(json({ conversationId: 'conv_new', transcript: '', wav: base64Of(44) }))

    const reply = await sendAudio(null, null)

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/chat/audio?format=json')
    expect(init?.body).toBeUndefined()
    expect(init?.headers).toEqual({ Accept: 'application/json' })
    expect(reply.transcript).toBeNull()
  })

  it('fails when the conversation id is missing', async () => {
    fetchMock.mockResolvedValue(json({ transcript: 'Hi', wav: base64Of(44) }))
    await expect(sendAudio(null, null)).rejects.toMatchObject({ title: 'Invalid response' })
  })

  it('fails when the answer is not JSON', async () => {
    fetchMock.mockResolvedValue(new Response(new Uint8Array(44), { status: 200, headers: { 'Content-Type': 'audio/wav' } }))
    await expect(sendAudio(null, null)).rejects.toMatchObject({ title: 'Invalid response' })
  })

  it('reports gateway errors', async () => {
    fetchMock.mockResolvedValue(json({ title: 'Timeout', detail: 'No answer within 60 seconds.' }, 504))
    await expect(sendAudio(null, null)).rejects.toMatchObject({ status: 504, title: 'Timeout' })
  })

  it('reports audio without speech', async () => {
    fetchMock.mockResolvedValue(
      json({ title: 'No speech recognized', detail: 'No speech was recognized in the audio.' }, 422, 'application/problem+json'),
    )
    await expect(sendAudio(new Blob([new Uint8Array(10)]), 'conv_1')).rejects.toMatchObject({
      status: 422,
      title: 'No speech recognized',
    })
  })
})
