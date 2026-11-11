import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach, vi } from 'vitest'

afterEach(() => cleanup())

// jsdom has no object URLs nor media playback.
if (!URL.createObjectURL) {
  let counter = 0
  URL.createObjectURL = vi.fn(() => `blob:test/${++counter}`)
  URL.revokeObjectURL = vi.fn()
}
HTMLMediaElement.prototype.play = () => Promise.resolve()
HTMLMediaElement.prototype.pause = () => {}
