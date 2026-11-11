import { useEffect, useRef, useState } from 'react'
import type { ChatMessage, PendingRequest } from '../types'

interface Props {
  messages: ChatMessage[]
  pending: PendingRequest | null
}

const AUTHOR: Record<ChatMessage['from'], string> = {
  user: 'You',
  agent: 'Agent',
  system: '',
  error: 'Error',
}

export function MessageList({ messages, pending }: Props) {
  const end = useRef<HTMLDivElement>(null)

  useEffect(() => {
    end.current?.scrollIntoView?.({ behavior: 'smooth', block: 'end' })
  }, [messages, pending])

  return (
    <div className="messages" role="log" aria-live="polite" aria-label="Messages">
      {messages.length === 0 && !pending && (
        <div className="empty">
          <p>Start with a greeting, type a message, or record your voice.</p>
          <p className="hint">Text and voice turns share the same conversation.</p>
        </div>
      )}

      {messages.map((m) => (
        <div key={m.id} className={`message ${m.from}`} data-testid={`message-${m.from}`}>
          {AUTHOR[m.from] && <div className="author">{AUTHOR[m.from]}</div>}
          {m.text && <div className="text">{m.text}</div>}
          {m.audioUrl && (
            // Agent answers play as soon as they arrive; user recordings only on demand.
            <audio controls src={m.audioUrl} autoPlay={m.from === 'agent'} aria-label={`${AUTHOR[m.from]} audio`} />
          )}
          {m.meta && <div className="meta">{m.meta}</div>}
        </div>
      ))}

      {pending && <Pending request={pending} />}
      <div ref={end} />
    </div>
  )
}

function Pending({ request }: { request: PendingRequest }) {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 250)
    return () => window.clearInterval(timer)
  }, [])

  return (
    <div className="message agent pending" role="status">
      <div className="author">Agent</div>
      <div className="text">
        <span className="dots" aria-hidden="true">
          <i />
          <i />
          <i />
        </span>
        {request.label} · {Math.max(0, (now - request.startedAt) / 1000).toFixed(0)} s
      </div>
    </div>
  )
}
