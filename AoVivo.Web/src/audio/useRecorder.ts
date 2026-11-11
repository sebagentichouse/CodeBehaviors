import { useCallback, useEffect, useRef, useState } from 'react'

export type RecorderState = 'idle' | 'recording'

/** Records the microphone with MediaRecorder. stop() resolves with the recorded clip (webm/ogg/mp4 depending on the browser). */
export function useRecorder() {
  const [state, setState] = useState<RecorderState>('idle')
  const [seconds, setSeconds] = useState(0)
  const recorder = useRef<MediaRecorder | null>(null)
  const chunks = useRef<Blob[]>([])
  const timer = useRef<number | undefined>(undefined)

  const release = useCallback(() => {
    window.clearInterval(timer.current)
    recorder.current?.stream.getTracks().forEach((t) => t.stop())
    recorder.current = null
    setState('idle')
  }, [])

  const start = useCallback(async () => {
    if (!navigator.mediaDevices?.getUserMedia || typeof MediaRecorder === 'undefined') {
      throw new Error('This browser cannot record audio (it needs https or localhost, and MediaRecorder support).')
    }
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: { channelCount: 1, echoCancellation: true, noiseSuppression: true },
    })
    chunks.current = []
    const media = new MediaRecorder(stream)
    media.ondataavailable = (e) => {
      if (e.data.size > 0) chunks.current.push(e.data)
    }
    media.start()
    recorder.current = media

    const startedAt = Date.now()
    setSeconds(0)
    timer.current = window.setInterval(() => setSeconds((Date.now() - startedAt) / 1000), 200)
    setState('recording')
  }, [])

  const stop = useCallback(
    () =>
      new Promise<Blob>((resolve, reject) => {
        const media = recorder.current
        if (!media) return reject(new Error('Not recording.'))
        media.onstop = () => {
          resolve(new Blob(chunks.current, { type: media.mimeType }))
          release()
        }
        media.onerror = () => {
          reject(new Error('Recording failed.'))
          release()
        }
        media.stop()
      }),
    [release],
  )

  const cancel = useCallback(() => {
    if (recorder.current) {
      recorder.current.onstop = null
      recorder.current.stop()
    }
    release()
  }, [release])

  // Stop the microphone if the component goes away mid-recording.
  useEffect(() => () => cancel(), [cancel])

  return { state, seconds, start, stop, cancel }
}
