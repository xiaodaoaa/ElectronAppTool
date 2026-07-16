import * as net from 'net'
import type { IStompSocket } from '@stomp/stompjs'

export function createTCPSocket(host: string, port: number): IStompSocket {
  const socket = new net.Socket()
  let url = `tcp://${host}:${port}`

  const stompSocket: IStompSocket = {
    url,
    binaryType: 'arraybuffer',

    get readyState(): number {
      if (socket.connecting) return 0
      if (socket.readyState === 'open') return 1
      if (socket.destroyed) return 3
      return 2
    },

    onclose: null,
    onerror: null,
    onmessage: null,
    onopen: null,

    close(): void {
      socket.destroy()
    },

    send(data: string | ArrayBufferLike | Blob | ArrayBufferView): void {
      if (typeof data === 'string') {
        socket.write(data)
      } else if (data instanceof ArrayBuffer) {
        socket.write(Buffer.from(data))
      } else if (ArrayBuffer.isView(data)) {
        socket.write(Buffer.from(data.buffer, data.byteOffset, data.byteLength))
      }
    }
  }

  socket.on('connect', () => {
    url = `tcp://${host}:${port}`
    stompSocket.url = url
    if (stompSocket.onopen) {
      stompSocket.onopen()
    }
  })

  socket.on('data', (data: Buffer) => {
    if (stompSocket.onmessage) {
      stompSocket.onmessage({ data: data.toString('utf8') })
    }
  })

  socket.on('close', () => {
    if (stompSocket.onclose) {
      stompSocket.onclose()
    }
  })

  socket.on('error', (err: Error) => {
    if (stompSocket.onerror) {
      stompSocket.onerror(err)
    }
  })

  socket.connect(port, host)

  return stompSocket
}