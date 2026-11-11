import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError, sendAudio, sendText } from './api/chatApi'
import { toAgentWav } from './audio/convert'
import { useRecorder } from './audio/useRecorder'
import { MIN_SECONDS, wavDurationSeconds } from './audio/wav'
import { Composer } from './components/Composer'
import { ConversationBar } from './components/ConversationBar'
import { MessageList } from './components/MessageList'
import type { ChatMessage, PendingRequest } from './types'
import './App.css'

type NewMessage = Omit<ChatMessage, 'id'>

/** What an API call produced: the conversation to continue and the agent's bubble. */
interface Exchange {
  conversationId: string
  agent: NewMessage
}

const seconds = (value: number) => `${value.toFixed(1)} s`

export default function App() {
  const [conversationId, setConversationId] = useState('')
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [pending, setPending] = useState<PendingRequest | null>(null)
  const recorder = useRecorder()
  const nextId = useRef(1)
  const audioUrls = useRef<string[]>([])

  const add = useCallback((message: NewMessage) => {
    setMessages((list) => [...list, { ...message, id: nextId.current++ }])
  }, [])

  const audioUrl = (blob: Blob) => {
    const url = URL.createObjectURL(blob)
    audioUrls.current.push(url)
    return url
  }

  const revokeAudio = () => {
    audioUrls.current.forEach((url) => URL.revokeObjectURL(url))
    audioUrls.current = []
  }
  useEffect(() => revokeAudio, [])

  const showError = (error: unknown) =>
    add({
      from: 'error',
      text: error instanceof Error ? error.message : String(error),
      meta: error instanceof ApiError && error.status ? `HTTP ${error.status}` : undefined,
    })

  /** Runs one request/answer turn against the API and records it in the conversation. */
  const exchange = async (label: string, call: (id: string | null) => Promise<Exchange>) => {
    const startedAt = Date.now()
    setPending({ label, startedAt })
    try {
      const result = await call(conversationId.trim() || null)
      setConversationId(result.conversationId)
      const elapsed = seconds((Date.now() - startedAt) / 1000)
      add({ ...result.agent, meta: [result.agent.meta, `answered in ${elapsed}`].filter(Boolean).join(' · ') })
    } catch (error) {
      showError(error)
    } finally {
      setPending(null)
    }
  }

  const textTurn = (message: string | null) =>
    exchange('Thinking', async (id) => {
      const reply = await sendText(message, id)
      return { conversationId: reply.conversationId, agent: { from: 'agent', text: reply.reply } }
    })

  const voiceTurn = (wav: Blob | null) =>
    exchange('Listening and answering', async (id) => {
      const reply = await sendAudio(wav, id)
      return {
        conversationId: reply.conversationId,
        agent: {
          from: 'agent',
          text: reply.transcript ?? undefined,
          audioUrl: audioUrl(reply.audio),
          meta: `🔊 ${seconds(wavDurationSeconds(reply.audio.size))}`,
        },
      }
    })

  const sendClip = async (clip: Blob, source: string) => {
    let converted
    try {
      converted = await toAgentWav(clip)
    } catch {
      showError(new Error(`Could not read the audio (${source}). Try another file or browser.`))
      return
    }
    if (converted.seconds < MIN_SECONDS) {
      showError(new Error('The audio is too short (less than 100 ms).'))
      return
    }
    add({ from: 'user', audioUrl: audioUrl(converted.wav), meta: `🎤 ${source} · ${seconds(converted.seconds)}` })
    await voiceTurn(converted.wav)
  }

  const handlers = {
    sendText: (text: string) => {
      add({ from: 'user', text })
      void textTurn(text)
    },

    greeting: (mode: 'text' | 'voice') => {
      add({ from: 'system', text: mode === 'text' ? 'Greeting requested (text)' : 'Greeting requested (voice)' })
      void (mode === 'text' ? textTurn(null) : voiceTurn(null))
    },

    toggleRecording: async () => {
      try {
        if (recorder.state === 'recording') await sendClip(await recorder.stop(), 'recording')
        else await recorder.start()
      } catch (error) {
        showError(
          error instanceof DOMException && error.name === 'NotAllowedError'
            ? new Error('Microphone access was denied. Allow it in the browser to record.')
            : error,
        )
      }
    },

    uploadAudio: (file: File) => void sendClip(file, file.name),

    newConversation: () => {
      recorder.cancel()
      revokeAudio()
      setMessages([])
      setConversationId('')
    },
  }

  const busy = pending !== null

  return (
    <div className="app">
      <header>
        <h1>
          AoVivo Chat <span>API tester</span>
        </h1>
        <ConversationBar
          conversationId={conversationId}
          onConversationIdChange={setConversationId}
          onNewConversation={handlers.newConversation}
          disabled={busy}
        />
      </header>

      <main>
        <MessageList messages={messages} pending={pending} />
      </main>

      <footer>
        <Composer
          busy={busy}
          recorderState={recorder.state}
          recordingSeconds={recorder.seconds}
          onSendText={handlers.sendText}
          onGreeting={handlers.greeting}
          onToggleRecording={() => void handlers.toggleRecording()}
          onCancelRecording={recorder.cancel}
          onUploadAudio={handlers.uploadAudio}
        />
      </footer>
    </div>
  )
}
