import {answerBytes, answerText, apiPath, downloadName, encodeBody, headersObject, isApiUrl, problemAnswer, toResponse} from './http';

describe('full demo http', () => {
  it('knows the API’s requests: /api on this site only', () => {
    expect(isApiUrl('/api/patients/search?LastName=x')).toBe(true);
    expect(isApiUrl(`${window.location.origin}/api/auth/whoami`)).toBe(true);
    expect(isApiUrl('/MedApp/full/backend.js')).toBe(false);
    expect(isApiUrl('https://elsewhere.example/api/x')).toBe(false);
    expect(apiPath(`${window.location.origin}/api/patients/search?LastName=x`)).toBe('/api/patients/search?LastName=x');
  });

  it('passes the headers on with the page’s host', () => {
    const headers = headersObject(new Headers({'Content-Type': 'application/json'}));
    expect(headers).toEqual({Host: window.location.host, 'content-type': 'application/json'});
  });

  it('encodes a form as multipart, with its boundary, and a file with its type', async () => {
    const form = new FormData();
    form.append('note', 'synthetic');
    const encoded = await encodeBody(form);
    expect(encoded.contentType).toMatch(/^multipart\/form-data; boundary=/);
    expect(new TextDecoder().decode(encoded.bytes)).toContain('synthetic');

    const file = await encodeBody(new Blob(['abc'], {type: 'text/plain'}));
    expect(file.contentType).toBe('text/plain');
    expect([...file.bytes!]).toEqual([97, 98, 99]);
    expect(await encodeBody(null)).toEqual({bytes: undefined, contentType: null});
  });

  it('turns answers into responses: text, a file (base64), and none for a 204', async () => {
    expect(await toResponse({status: 200, headers: {'Content-Type': 'application/json'}, body: '{"a":1}'}).json()).toEqual({a: 1});
    const file = toResponse({status: 200, headers: {}, bodyBase64: btoa('%PDF-')});
    expect(await file.text()).toBe('%PDF-');
    expect(toResponse({status: 204, headers: {}, body: ''}).status).toBe(204);
    expect(answerText({status: 200, headers: {}, bodyBase64: btoa('hello')})).toBe('hello');
    expect([...answerBytes({status: 200, headers: {}, body: 'hi'})]).toEqual([104, 105]);
    expect(JSON.parse(problemAnswer(503, 'Not ready').body!)).toEqual({status: 503, title: 'Not ready'});
  });

  it('reads a download’s file name from Content-Disposition', () => {
    expect(downloadName({'Content-Disposition': 'attachment; filename=report.pdf; filename*=UTF-8\'\'gp%20report.pdf'})).toBe('gp report.pdf');
    expect(downloadName({'content-disposition': 'attachment; filename="note.txt"'})).toBe('note.txt');
    expect(downloadName({})).toBeNull();
  });
});
