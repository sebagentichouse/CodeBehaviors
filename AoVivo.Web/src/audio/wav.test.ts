import { describe, expect, it } from 'vitest'
import { encodeWav, floatTo16BitPcm, mixToMono, SAMPLE_RATE, wavDurationSeconds } from './wav'

const text = (view: DataView, offset: number) =>
  String.fromCharCode(...Array.from({ length: 4 }, (_, i) => view.getUint8(offset + i)))

describe('encodeWav', () => {
  it('writes a PCM16 / 24 kHz / mono header followed by the samples', async () => {
    const pcm = new Int16Array([0, 1, -1, 32767, -32768])
    const blob = encodeWav(pcm)
    const view = new DataView(await blob.arrayBuffer())

    expect(blob.type).toBe('audio/wav')
    expect(view.byteLength).toBe(44 + pcm.length * 2)
    expect(text(view, 0)).toBe('RIFF')
    expect(view.getUint32(4, true)).toBe(36 + pcm.length * 2)
    expect(text(view, 8)).toBe('WAVE')
    expect(text(view, 12)).toBe('fmt ')
    expect(view.getUint32(16, true)).toBe(16)
    expect(view.getUint16(20, true)).toBe(1) // PCM
    expect(view.getUint16(22, true)).toBe(1) // mono
    expect(view.getUint32(24, true)).toBe(SAMPLE_RATE)
    expect(view.getUint32(28, true)).toBe(SAMPLE_RATE * 2)
    expect(view.getUint16(32, true)).toBe(2)
    expect(view.getUint16(34, true)).toBe(16)
    expect(text(view, 36)).toBe('data')
    expect(view.getUint32(40, true)).toBe(pcm.length * 2)
    expect(Array.from(pcm, (_, i) => view.getInt16(44 + i * 2, true))).toEqual([0, 1, -1, 32767, -32768])
  })
})

describe('floatTo16BitPcm', () => {
  it('scales and clamps samples', () => {
    expect(Array.from(floatTo16BitPcm(new Float32Array([0, 1, -1, 0.5, 2, -2])))).toEqual([
      0, 32767, -32768, 16384, 32767, -32768,
    ])
  })
})

describe('mixToMono', () => {
  it('returns a single channel untouched', () => {
    const channel = new Float32Array([0.1, 0.2])
    expect(mixToMono([channel])).toBe(channel)
  })

  it('averages channels, truncating to the shortest', () => {
    const mono = mixToMono([new Float32Array([1, 0, 0.5, 9]), new Float32Array([0, 0, -0.5])])
    expect(Array.from(mono)).toEqual([0.5, 0, 0])
  })

  it('handles no channels', () => {
    expect(mixToMono([])).toHaveLength(0)
  })
})

describe('wavDurationSeconds', () => {
  it('computes the duration of a 24 kHz PCM16 WAV', () => {
    expect(wavDurationSeconds(44 + 48_000)).toBe(1)
    expect(wavDurationSeconds(10)).toBe(0)
  })
})
