/**
 * A stand-in for the browser's `BroadcastChannel`, in this test's memory only (Node's own reaches other test workers
 * in the same process). As in a browser, a message goes to every other open channel of the same name, never back to
 * the one that sent it, a moment later, as a copy. `setup.ts` puts it in place for every test, and closes them all
 * after each.
 */
export class FakeBroadcastChannel extends EventTarget {
  static readonly open = new Set<FakeBroadcastChannel>();

  onmessage: ((event: MessageEvent) => void) | null = null;

  constructor(readonly name: string) {
    super();
    FakeBroadcastChannel.open.add(this);
  }

  postMessage(data: unknown) {
    if (!FakeBroadcastChannel.open.has(this)) throw new DOMException('The channel is closed.', 'InvalidStateError');
    const copy = JSON.parse(JSON.stringify(data)) as unknown;
    for (const other of FakeBroadcastChannel.open) {
      if (other === this || other.name !== this.name) continue;
      queueMicrotask(() => {
        if (!FakeBroadcastChannel.open.has(other)) return;
        const event = new MessageEvent('message', {data: copy});
        other.onmessage?.(event);
        other.dispatchEvent(event);
      });
    }
  }

  close() {
    FakeBroadcastChannel.open.delete(this);
  }

  /** Closes every channel (after each test). */
  static reset() {
    FakeBroadcastChannel.open.clear();
  }
}

/**
 * Another of MedApp's windows on this channel, for a test: what it has been sent, and a way to send as it would.
 */
export function otherWindow(name = 'medapp.notepad') {
  const channel = new FakeBroadcastChannel(name);
  const received: unknown[] = [];
  channel.onmessage = (event) => received.push(event.data);
  return {received, post: (message: unknown) => channel.postMessage(message), close: () => channel.close()};
}
