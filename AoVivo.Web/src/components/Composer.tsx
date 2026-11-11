import { useRef, useState, type FormEvent } from 'react'
import type { RecorderState } from '../audio/useRecorder'

interface Props {
  busy: boolean
  recorderState: RecorderState
  recordingSeconds: number
  onSendText: (text: string) => void
  onGreeting: (mode: 'text' | 'voice') => void
  onToggleRecording: () => void
  onCancelRecording: () => void
  onUploadAudio: (file: File) => void
}

export function Composer({
  busy,
  recorderState,
  recordingSeconds,
  onSendText,
  onGreeting,
  onToggleRecording,
  onCancelRecording,
  onUploadAudio,
}: Props) {
  const [text, setText] = useState('')
  const fileInput = useRef<HTMLInputElement>(null)
  const recording = recorderState === 'recording'

  const submit = (e: FormEvent) => {
    e.preventDefault()
    const message = text.trim()
    if (!message || busy) return
    onSendText(message)
    setText('')
  }

  return (
    <div className="composer">
      <div className="quick-actions">
        <button type="button" onClick={() => onGreeting('text')} disabled={busy || recording}>
          👋 Text greeting
        </button>
        <button type="button" onClick={() => onGreeting('voice')} disabled={busy || recording}>
          🔊 Spoken greeting
        </button>
        <button type="button" onClick={() => fileInput.current?.click()} disabled={busy || recording}>
          📁 Send audio file
        </button>
        <input
          ref={fileInput}
          type="file"
          accept="audio/*"
          hidden
          data-testid="audio-file"
          onChange={(e) => {
            const file = e.target.files?.[0]
            e.target.value = '' // allow picking the same file again
            if (file) onUploadAudio(file)
          }}
        />
      </div>

      <form className="input-row" onSubmit={submit}>
        <input
          aria-label="Message"
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder={recording ? 'Recording… click ■ to send' : 'Type a message and press Enter'}
          disabled={busy || recording}
          autoFocus
        />
        <button type="submit" className="primary" disabled={busy || recording || !text.trim()}>
          Send
        </button>
        <button
          type="button"
          className={`mic ${recording ? 'recording' : ''}`}
          onClick={onToggleRecording}
          disabled={busy}
          aria-pressed={recording}
          title={recording ? 'Stop and send the recording' : 'Record a voice message'}
        >
          {recording ? `■ ${recordingSeconds.toFixed(1)} s` : '🎤 Talk'}
        </button>
        {recording && (
          <button type="button" onClick={onCancelRecording} title="Discard the recording">
            Cancel
          </button>
        )}
      </form>
    </div>
  )
}
