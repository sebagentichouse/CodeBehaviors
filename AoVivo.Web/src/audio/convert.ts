import { encodeWav, floatTo16BitPcm, mixToMono, SAMPLE_RATE } from './wav'

export interface AgentAudio {
  /** PCM16 / 24 kHz / mono WAV, ready for POST /api/chat/audio. */
  wav: Blob
  seconds: number
}

/**
 * Converts any audio the browser can decode (a microphone recording, an mp3/m4a/wav file, ...)
 * into the WAV format AoVivo.Api expects.
 */
export async function toAgentWav(input: Blob): Promise<AgentAudio> {
  const context = new OfflineAudioContext(1, 1, SAMPLE_RATE)
  // decodeAudioData resamples to the context's rate (24 kHz).
  let decoded = await context.decodeAudioData(await input.arrayBuffer())
  if (decoded.sampleRate !== SAMPLE_RATE) decoded = await resample(decoded)

  const channels = Array.from({ length: decoded.numberOfChannels }, (_, i) => decoded.getChannelData(i))
  const pcm = floatTo16BitPcm(mixToMono(channels))
  return { wav: encodeWav(pcm), seconds: pcm.length / SAMPLE_RATE }
}

/** Fallback for browsers that do not resample in decodeAudioData. */
async function resample(buffer: AudioBuffer): Promise<AudioBuffer> {
  const offline = new OfflineAudioContext(1, Math.max(1, Math.ceil(buffer.duration * SAMPLE_RATE)), SAMPLE_RATE)
  const source = offline.createBufferSource()
  source.buffer = buffer
  source.connect(offline.destination)
  source.start()
  return offline.startRendering()
}
