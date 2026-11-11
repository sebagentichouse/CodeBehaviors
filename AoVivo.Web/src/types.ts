export type Sender = 'user' | 'agent' | 'system' | 'error'

export interface ChatMessage {
  id: number
  from: Sender
  text?: string
  /** Object URL of a WAV clip (user recording or agent answer). */
  audioUrl?: string
  /** Small print under the bubble: duration, response time, ... */
  meta?: string
}

export interface PendingRequest {
  label: string
  startedAt: number
}
