import type {BackendResponse} from './backend';
import {answerBytes, answerText, headerOf, isApiUrl} from './http';

/** Answers one request: its method, URL, the headers set, and the body sent. */
export type XhrAnswer = (method: string, url: string, headers: Record<string, string>, body: unknown) => Promise<BackendResponse>;

/** XMLHttpRequest's state, which a request for the API reads from its answer. */
const STATE = ['readyState', 'status', 'statusText', 'responseURL', 'responseText', 'response'] as const;
type StateName = (typeof STATE)[number];

/**
 * XMLHttpRequest for the full demo: a request for the API (`/api/...` on this site: the app's uploads, `sendFile`) is
 * answered by the backend in the page; anything else is a real XMLHttpRequest. Progress is simple: the whole body is
 * reported sent once it has gone to the backend. A file's answer (`bodyBase64`) comes back as a Blob or bytes, by the
 * `responseType` asked for.
 */
export function backendXhrClass(Real: typeof XMLHttpRequest, answer: XhrAnswer): typeof XMLHttpRequest {
  class BackendXhr extends Real {
    #request: {method: string; url: string; headers: Record<string, string>} | null = null;
    #answer: BackendResponse | null = null;
    #state = 0;
    #aborted = false;

    override open(method: string, url: string | URL, ...rest: unknown[]) {
      if (isApiUrl(url)) {
        this.#request = {method: method.toUpperCase(), url: url.toString(), headers: {}};
        this.#answer = null;
        this.#aborted = false;
        this.#change(1);
        return;
      }
      this.#request = null;
      (super.open as (...args: unknown[]) => void)(method, url, ...rest);
    }

    override setRequestHeader(name: string, value: string) {
      if (this.#request) this.#request.headers[name] = value;
      else super.setRequestHeader(name, value);
    }

    override send(body?: Document | XMLHttpRequestBodyInit | null) {
      const request = this.#request;
      if (!request) return super.send(body);
      const size = body instanceof Blob ? body.size : typeof body === 'string' ? body.length : 0;
      this.#fire(this, 'loadstart');
      answer(request.method, request.url, request.headers, body).then(
        (result) => {
          if (this.#aborted) return;
          this.#fire(this.upload, 'progress', size, size);
          this.#fire(this.upload, 'load', size, size);
          this.#answer = result;
          this.#change(4);
          this.#fire(this, 'load');
          this.#fire(this, 'loadend');
        },
        () => {
          if (this.#aborted) return;
          this.#change(4);
          this.#fire(this, 'error');
          this.#fire(this, 'loadend');
        },
      );
    }

    override abort() {
      if (!this.#request) return super.abort();
      if (this.#aborted || this.#answer) return;
      this.#aborted = true;
      this.#change(4);
      this.#fire(this, 'abort');
      this.#fire(this, 'loadend');
    }

    override getResponseHeader(name: string) {
      if (!this.#request) return super.getResponseHeader(name);
      return headerOf(this.#answer?.headers ?? {}, name);
    }

    override getAllResponseHeaders() {
      if (!this.#request) return super.getAllResponseHeaders();
      return Object.entries(this.#answer?.headers ?? {})
        .map(([name, value]) => `${name.toLowerCase()}: ${value}\r\n`)
        .join('');
    }

    /** Whether this request is for the API (answered by the backend). */
    isForBackend() {
      return this.#request !== null;
    }

    /** XMLHttpRequest's state for a request for the API. */
    stateOf(name: StateName): unknown {
      switch (name) {
        case 'readyState':
          return this.#state;
        case 'status':
          return this.#answer?.status ?? 0;
        case 'statusText':
          return '';
        case 'responseURL':
          return new URL(this.#request!.url, window.location.href).href;
        case 'responseText':
          return this.#answer ? answerText(this.#answer) : '';
        case 'response':
          return this.#response();
      }
    }

    #response(): unknown {
      if (!this.#answer) return this.responseType === '' || this.responseType === 'text' ? '' : null;
      switch (this.responseType) {
        case 'json':
          try {
            return JSON.parse(answerText(this.#answer));
          } catch {
            return null;
          }
        case 'blob':
          return new Blob([answerBytes(this.#answer)], {type: this.getResponseHeader('Content-Type') ?? ''});
        case 'arraybuffer':
          return answerBytes(this.#answer).buffer;
        default:
          return answerText(this.#answer);
      }
    }

    #change(state: number) {
      this.#state = state;
      this.#fire(this, 'readystatechange');
    }

    #fire(target: EventTarget, type: string, loaded = 0, total = 0) {
      const event = type === 'readystatechange' ? new Event(type) : new ProgressEvent(type, {lengthComputable: total > 0, loaded, total});
      target.dispatchEvent(event);
    }
  }

  // Accessors on the prototype, as the DOM's own are (so not class members over XMLHttpRequest's properties): a request
  // for the API reads its answer, any other XMLHttpRequest's own.
  for (const name of STATE) {
    Object.defineProperty(BackendXhr.prototype, name, {
      configurable: true,
      get(this: BackendXhr) {
        return this.isForBackend() ? this.stateOf(name) : Reflect.get(Real.prototype, name, this);
      },
    });
  }
  return BackendXhr;
}

/** Puts the backend's XMLHttpRequest in place (`installFullDemo`). */
export function installXhr(answer: XhrAnswer): void {
  window.XMLHttpRequest = backendXhrClass(window.XMLHttpRequest, answer);
}
