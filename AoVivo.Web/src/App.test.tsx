import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import App from './App'
import { ApiError, sendAudio, sendText } from './api/chatApi'
import { toAgentWav } from './audio/convert'

vi.mock('./api/chatApi', async (importOriginal) => ({
  ...(await importOriginal<typeof import('./api/chatApi')>()),
  sendText: vi.fn(),
  sendAudio: vi.fn(),
}))
vi.mock('./audio/convert', () => ({ toAgentWav: vi.fn() }))

const textMock = vi.mocked(sendText)
const audioMock = vi.mocked(sendAudio)
const convertMock = vi.mocked(toAgentWav)

const conversationInput = () => screen.getByLabelText('Conversation') as HTMLInputElement
const messages = (from: string) => screen.queryAllByTestId(`message-${from}`)

beforeEach(() => {
  textMock.mockReset()
  audioMock.mockReset()
  convertMock.mockReset()
})

describe('App', () => {
  it('gets the text greeting and keeps the conversation for the next message', async () => {
    const user = userEvent.setup()
    textMock
      .mockResolvedValueOnce({ conversationId: 'conv_1', reply: 'Hi! How can I help?' })
      .mockResolvedValueOnce({ conversationId: 'conv_1', reply: 'It is sunny.' })
    render(<App />)

    await user.click(screen.getByRole('button', { name: /text greeting/i }))
    expect(await screen.findByText('Hi! How can I help?')).toBeInTheDocument()
    expect(textMock).toHaveBeenLastCalledWith(null, null)
    expect(conversationInput().value).toBe('conv_1')

    await user.type(screen.getByLabelText('Message'), 'How is the weather?{Enter}')
    expect(await screen.findByText('It is sunny.')).toBeInTheDocument()
    expect(textMock).toHaveBeenLastCalledWith('How is the weather?', 'conv_1')
    expect(messages('user').map((m) => m.textContent)).toEqual(['YouHow is the weather?'])
    expect(screen.getByLabelText('Message')).toHaveValue('')
  })

  it('plays the spoken greeting with its transcript', async () => {
    const user = userEvent.setup()
    audioMock.mockResolvedValue({
      conversationId: 'conv_v',
      audio: new Blob([new Uint8Array(44 + 96_000)], { type: 'audio/wav' }),
      transcript: 'Olá, bem-vindo!',
    })
    render(<App />)

    await user.click(screen.getByRole('button', { name: /spoken greeting/i }))

    const agent = await screen.findByText('Olá, bem-vindo!')
    const bubble = agent.closest('.message') as HTMLElement
    expect(within(bubble).getByLabelText('Agent audio')).toHaveAttribute('src', expect.stringMatching(/^blob:/))
    expect(within(bubble).getByText(/🔊 2\.0 s · answered in/)).toBeInTheDocument()
    expect(audioMock).toHaveBeenCalledWith(null, null)
    expect(conversationInput().value).toBe('conv_v')
  })

  it('converts an uploaded audio file and sends it in the current conversation', async () => {
    const user = userEvent.setup()
    const wav = new Blob([new Uint8Array(10)], { type: 'audio/wav' })
    convertMock.mockResolvedValue({ wav, seconds: 1.5 })
    audioMock.mockResolvedValue({ conversationId: 'conv_9', audio: new Blob([new Uint8Array(44)]), transcript: 'Sure.' })
    render(<App />)

    await user.type(conversationInput(), 'conv_9')
    await user.upload(screen.getByTestId('audio-file'), new File(['x'], 'question.mp3', { type: 'audio/mpeg' }))

    expect(await screen.findByText('Sure.')).toBeInTheDocument()
    expect(audioMock).toHaveBeenCalledWith(wav, 'conv_9')
    expect(screen.getByText('🎤 question.mp3 · 1.5 s')).toBeInTheDocument()
  })

  it('rejects audio shorter than 100 ms without calling the API', async () => {
    const user = userEvent.setup()
    convertMock.mockResolvedValue({ wav: new Blob(), seconds: 0.05 })
    render(<App />)

    await user.upload(screen.getByTestId('audio-file'), new File(['x'], 'click.wav', { type: 'audio/wav' }))

    expect(await screen.findByText(/too short/)).toBeInTheDocument()
    expect(audioMock).not.toHaveBeenCalled()
  })

  it('shows API errors and keeps the conversation usable', async () => {
    const user = userEvent.setup()
    textMock.mockRejectedValueOnce(new ApiError(404, 'Conversation not found', "Conversation 'bad' does not exist."))
    render(<App />)

    await user.type(conversationInput(), 'bad')
    await user.type(screen.getByLabelText('Message'), 'Hello{Enter}')

    const error = await screen.findByText("Conversation not found: Conversation 'bad' does not exist.")
    expect(error.closest('.message')).toHaveTextContent('HTTP 404')
    expect(screen.getByLabelText('Message')).toBeEnabled()
  })

  it('disables input while waiting for the agent', async () => {
    const user = userEvent.setup()
    let answer!: (value: { conversationId: string; reply: string }) => void
    textMock.mockReturnValue(new Promise((resolve) => (answer = resolve)))
    render(<App />)

    await user.type(screen.getByLabelText('Message'), 'Hello{Enter}')

    expect(screen.getByRole('status')).toHaveTextContent('Thinking')
    expect(screen.getByLabelText('Message')).toBeDisabled()
    expect(screen.getByRole('button', { name: /talk/i })).toBeDisabled()

    answer({ conversationId: 'c', reply: 'Hi' })
    await waitFor(() => expect(screen.getByLabelText('Message')).toBeEnabled())
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('starts a new conversation', async () => {
    const user = userEvent.setup()
    textMock.mockResolvedValue({ conversationId: 'conv_1', reply: 'Hi!' })
    render(<App />)

    await user.click(screen.getByRole('button', { name: /text greeting/i }))
    await screen.findByText('Hi!')
    await user.click(screen.getByRole('button', { name: /new conversation/i }))

    expect(conversationInput().value).toBe('')
    expect(screen.queryByText('Hi!')).not.toBeInTheDocument()
    expect(screen.getByText(/Start with a greeting/)).toBeInTheDocument()
  })

  it('explains when the microphone is unavailable', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.click(screen.getByRole('button', { name: /talk/i }))

    expect(await screen.findByText(/cannot record audio/)).toBeInTheDocument()
  })
})
