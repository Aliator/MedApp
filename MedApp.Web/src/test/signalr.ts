/**
 * A stand-in for `@microsoft/signalr` in tests (`setup.ts` swaps it in): connections "start" without a server, and
 * `liveHub.push` delivers a message to every connection listening for it, as the API's hub would.
 */
type Handler = (...args: unknown[]) => void;

const connections = new Set<FakeHubConnection>();

class FakeHubConnection {
  readonly handlers = new Map<string, Handler[]>();
  readonly url: string;
  isStarted = false;

  constructor(url: string) {
    this.url = url;
  }

  on(method: string, handler: Handler) {
    this.handlers.set(method, [...(this.handlers.get(method) ?? []), handler]);
  }

  onreconnected() {}

  start() {
    this.isStarted = true;
    connections.add(this);
    return Promise.resolve();
  }

  stop() {
    this.isStarted = false;
    connections.delete(this);
    return Promise.resolve();
  }
}

export class HubConnectionBuilder {
  private url = '';

  withUrl(url: string) {
    this.url = url;
    return this;
  }

  withAutomaticReconnect() {
    return this;
  }

  configureLogging() {
    return this;
  }

  build() {
    return new FakeHubConnection(this.url);
  }
}

export const LogLevel = {Warning: 3};

export const liveHub = {
  /** The connections open now. */
  get connections() {
    return [...connections];
  },
  /** Delivers a message to every open connection listening for it. */
  push(method: string, ...args: unknown[]) {
    for (const connection of connections) for (const handler of connection.handlers.get(method) ?? []) handler(...args);
  },
  reset() {
    connections.clear();
  },
};
