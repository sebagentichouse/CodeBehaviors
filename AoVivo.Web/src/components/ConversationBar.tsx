interface Props {
  conversationId: string
  onConversationIdChange: (value: string) => void
  onNewConversation: () => void
  disabled: boolean
}

export function ConversationBar({ conversationId, onConversationIdChange, onNewConversation, disabled }: Props) {
  const copy = () => void navigator.clipboard?.writeText(conversationId)

  return (
    <div className="conversation-bar">
      <label htmlFor="conversation-id">Conversation</label>
      <input
        id="conversation-id"
        value={conversationId}
        onChange={(e) => onConversationIdChange(e.target.value)}
        placeholder="new – created by the first request"
        spellCheck={false}
        disabled={disabled}
      />
      <button type="button" onClick={copy} disabled={!conversationId} title="Copy the conversation id">
        Copy
      </button>
      <button type="button" onClick={onNewConversation} disabled={disabled}>
        New conversation
      </button>
    </div>
  )
}
