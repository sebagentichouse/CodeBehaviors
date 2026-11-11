// Audio format expected and returned by AoVivo.Api: PCM signed 16-bit little-endian, 24 kHz, mono.
export const SAMPLE_RATE = 24_000
export const CHANNELS = 1
export const BITS_PER_SAMPLE = 16
export const BYTES_PER_SECOND = SAMPLE_RATE * CHANNELS * (BITS_PER_SAMPLE / 8)
export const WAV_HEADER_SIZE = 44
/** The API rejects less than 100 ms of audio. */
export const MIN_SECONDS = 0.1

/** Averages all channels into one. */
export function mixToMono(channels: readonly Float32Array[]): Float32Array {
  if (channels.length === 0) return new Float32Array(0)
  if (channels.length === 1) return channels[0]
  const length = Math.min(...channels.map((c) => c.length))
  const mono = new Float32Array(length)
  for (const channel of channels) {
    for (let i = 0; i < length; i++) mono[i] += channel[i] / channels.length
  }
  return mono
}

/** Converts [-1, 1] float samples to 16-bit integers (clamping out-of-range values). */
export function floatTo16BitPcm(samples: Float32Array): Int16Array {
  const pcm = new Int16Array(samples.length)
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]))
    pcm[i] = s < 0 ? Math.round(s * 0x8000) : Math.round(s * 0x7fff)
  }
  return pcm
}

/** Wraps PCM16 mono samples in a WAV (RIFF) container. */
export function encodeWav(pcm: Int16Array, sampleRate = SAMPLE_RATE): Blob {
  const dataBytes = pcm.length * 2
  const buffer = new ArrayBuffer(WAV_HEADER_SIZE + dataBytes)
  const view = new DataView(buffer)
  const ascii = (offset: number, text: string) => {
    for (let i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i))
  }

  ascii(0, 'RIFF')
  view.setUint32(4, WAV_HEADER_SIZE - 8 + dataBytes, true)
  ascii(8, 'WAVE')
  ascii(12, 'fmt ')
  view.setUint32(16, 16, true) // fmt chunk size
  view.setUint16(20, 1, true) // PCM
  view.setUint16(22, CHANNELS, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * CHANNELS * 2, true) // byte rate
  view.setUint16(32, CHANNELS * 2, true) // block align
  view.setUint16(34, BITS_PER_SAMPLE, true)
  ascii(36, 'data')
  view.setUint32(40, dataBytes, true)

  for (let i = 0; i < pcm.length; i++) view.setInt16(WAV_HEADER_SIZE + i * 2, pcm[i], true)
  return new Blob([buffer], { type: 'audio/wav' })
}

/** Duration of a WAV produced by the API (44-byte header + PCM16 24 kHz mono). */
export function wavDurationSeconds(byteLength: number): number {
  return Math.max(0, byteLength - WAV_HEADER_SIZE) / BYTES_PER_SECOND
}
