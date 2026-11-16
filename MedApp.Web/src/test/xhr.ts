/**
 * A stand-in XMLHttpRequest for uploads (`uploadAttachment`): nothing is sent, and the test answers each request
 * itself, with progress, a response, a network failure or nothing (to cancel it). `stubXhr()` installs it (the
 * test setup's `vi.unstubAllGlobals` removes it) and returns the requests made, in order.
 */
export class FakeXhr {
  method = '';
  url = '';
  headers: Record<string, string> = {};
  withCredentials = false;
  body: unknown = undefined;
  status = 0;
  responseText = '';
  isAborted = false;
  upload: {onprogress: ((event: Pick<ProgressEvent, 'lengthComputable' | 'loaded' | 'total'>) => void) | null} = {onprogress: null};
  onload: (() => void) | null = null;
  onerror: (() => void) | null = null;
  onabort: (() => void) | null = null;

  constructor(private readonly sent: FakeXhr[]) {}

  open(method: string, url: string) {
    this.method = method;
    this.url = url;
  }

  setRequestHeader(name: string, value: string) {
    this.headers[name] = value;
  }

  send(body: unknown) {
    this.body = body;
    this.sent.push(this);
  }

  abort() {
    this.isAborted = true;
    this.onabort?.();
  }

  /** Reports this much of the body sent. */
  progress(loaded: number, total: number, lengthComputable = true) {
    this.upload.onprogress?.({lengthComputable, loaded, total});
  }

  /** Answers with this status and body (JSON unless a string). */
  respond(status: number, body: unknown = '') {
    this.status = status;
    this.responseText = typeof body === 'string' ? body : JSON.stringify(body);
    this.onload?.();
  }

  /** Fails as a dropped connection does (no status). */
  fail() {
    this.onerror?.();
  }
}

export function stubXhr(): FakeXhr[] {
  const sent: FakeXhr[] = [];
  vi.stubGlobal(
    'XMLHttpRequest',
    class extends FakeXhr {
      constructor() {
        super(sent);
      }
    },
  );
  return sent;
}
