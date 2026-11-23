import {uploadAttachment, uploadCancelled} from '../api/attachments';
import {answer} from '../test/fullDemo';
import type {BackendResponse} from './backend';
import {backendXhrClass, installXhr} from './xhr';

describe('the full demo’s XMLHttpRequest', () => {
  it('sends an upload to the backend, with its headers and file, and reports progress and the answer', async () => {
    const answerFn = vi.fn(async (): Promise<BackendResponse> => answer(201, {id: 'file-1', fileName: 'note.txt'}));
    const Xhr = backendXhrClass(XMLHttpRequest, answerFn);
    const xhr = new Xhr();
    const progress: number[] = [];
    const states: number[] = [];
    xhr.upload.onprogress = (event) => progress.push(event.loaded / event.total);
    xhr.onreadystatechange = () => states.push(xhr.readyState);
    const loaded = new Promise<void>((resolve) => (xhr.onload = () => resolve()));

    xhr.open('POST', '/api/attachments?patientId=p1');
    xhr.setRequestHeader('X-File-Name', 'note.txt');
    const file = new File(['synthetic'], 'note.txt', {type: 'text/plain'});
    xhr.send(file);
    await loaded;

    expect(answerFn).toHaveBeenCalledWith('POST', '/api/attachments?patientId=p1', {'X-File-Name': 'note.txt'}, file);
    expect(progress).toEqual([1]);
    expect(states).toEqual([1, 4]);
    expect(xhr.status).toBe(201);
    expect(JSON.parse(xhr.responseText)).toEqual({id: 'file-1', fileName: 'note.txt'});
    expect(xhr.getResponseHeader('content-type')).toBe('application/json');
    expect(xhr.getAllResponseHeaders()).toBe('content-type: application/json\r\n');
  });

  it('gives a file’s answer as a Blob or bytes, by responseType', async () => {
    const Xhr = backendXhrClass(XMLHttpRequest, async () => ({status: 200, headers: {'Content-Type': 'application/pdf'}, bodyBase64: btoa('%PDF-')}));
    const xhr = new Xhr();
    xhr.open('GET', '/api/attachments/1/content');
    xhr.responseType = 'blob';
    const loaded = new Promise<void>((resolve) => (xhr.onload = () => resolve()));
    xhr.send();
    await loaded;
    const blob = xhr.response as Blob;
    expect(blob.type).toBe('application/pdf');
    expect(await blob.text()).toBe('%PDF-');
  });

  it('cancels, and fails as a dropped connection when the backend can’t answer', async () => {
    let finish: (value: BackendResponse) => void = () => {};
    const Xhr = backendXhrClass(XMLHttpRequest, () => new Promise((resolve) => (finish = resolve)));
    const xhr = new Xhr();
    const aborted = vi.fn();
    xhr.onabort = aborted;
    xhr.open('POST', '/api/attachments');
    xhr.send('x');
    xhr.abort();
    finish(answer(201));
    await Promise.resolve();
    expect(aborted).toHaveBeenCalledTimes(1);
    expect(xhr.status).toBe(0);

    const Failing = backendXhrClass(XMLHttpRequest, () => Promise.reject(new Error('down')));
    const failing = new Failing();
    const failed = new Promise<void>((resolve) => (failing.onerror = () => resolve()));
    failing.open('POST', '/api/attachments');
    failing.send('x');
    await failed;
    expect(failing.status).toBe(0);
  });

  it('leaves any other request to the real XMLHttpRequest', () => {
    const opened: unknown[] = [];
    class Real extends XMLHttpRequest {
      override open(...args: unknown[]) {
        opened.push(args);
      }
      override send() {
        opened.push('sent');
      }
    }
    const answerFn = vi.fn();
    const Xhr = backendXhrClass(Real, answerFn);
    const xhr = new Xhr();
    xhr.open('GET', 'https://elsewhere.example/data.json');
    xhr.send();
    expect(opened).toEqual([['GET', 'https://elsewhere.example/data.json'], 'sent']);
    expect(answerFn).not.toHaveBeenCalled();
    expect(xhr.readyState).toBe(0);
  });

  it('carries the app’s own uploads (uploadAttachment) to the backend as a file', async () => {
    const original = window.XMLHttpRequest;
    const received: {headers: Record<string, string>; bytes?: Uint8Array}[] = [];
    const answerFn = vi.fn(async (method: string, url: string, headers: Record<string, string>, body: unknown) => {
      received.push({headers, bytes: new Uint8Array(await (body as Blob).arrayBuffer())});
      void method;
      void url;
      return answer(201, {id: 'a1', fileName: 'synthetic.txt'});
    });
    installXhr(answerFn);
    try {
      const attached = await uploadAttachment({target: {patientId: 'p1'}, file: new File(['abc'], 'synthetic.txt')});
      expect(attached).toEqual({id: 'a1', fileName: 'synthetic.txt'});
      expect(received[0].headers).toMatchObject({'Content-Type': 'application/octet-stream', 'X-File-Name': 'synthetic.txt'});
      expect([...received[0].bytes!]).toEqual([97, 98, 99]);
      const controller = new AbortController();
      controller.abort();
      await expect(uploadAttachment({target: {patientId: 'p1'}, file: new File(['x'], 'x.txt'), signal: controller.signal})).rejects.toEqual(uploadCancelled());
    } finally {
      window.XMLHttpRequest = original;
    }
  });
});
