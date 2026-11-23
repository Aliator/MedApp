import {installDownloads} from './downloads';

describe('the full demo’s downloads', () => {
  it('answers a download link to the API from the backend and saves the file under the API’s name', async () => {
    const answer = vi.fn(async () => ({
      status: 200,
      headers: {'Content-Type': 'application/pdf', 'Content-Disposition': 'attachment; filename="gp-report.pdf"'},
      bodyBase64: btoa('%PDF-'),
    }));
    const saved: {name: string; href: string}[] = [];
    // The saved file's link, clicked by the code under test (the link to the API is dispatched by hand below).
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      saved.push({name: this.download, href: this.href});
    });
    vi.stubGlobal('URL', Object.assign(URL, {createObjectURL: vi.fn(() => 'blob:file'), revokeObjectURL: vi.fn()}));
    const stop = installDownloads(answer);

    const link = document.createElement('a');
    link.href = '/api/attachments/a1/content';
    link.setAttribute('download', '');
    link.textContent = 'Download';
    document.body.append(link);
    const event = new MouseEvent('click', {bubbles: true, cancelable: true, button: 0});
    link.dispatchEvent(event);
    await vi.waitFor(() => expect(saved).toHaveLength(1));

    expect(event.defaultPrevented).toBe(true);
    expect(answer).toHaveBeenCalledWith('/api/attachments/a1/content');
    expect(saved[0]).toEqual({name: 'gp-report.pdf', href: 'blob:file'});
    stop();
    link.remove();
  });

  it('leaves other links alone, and logs a refused file', async () => {
    const answer = vi.fn(async () => ({status: 404, headers: {}, body: ''}));
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const stop = installDownloads(answer);
    const other = document.createElement('a');
    other.href = '/MedApp/full/patients';
    document.body.append(other);
    // jsdom can't navigate: the page's own handler would take it from here.
    other.addEventListener('click', (event) => event.preventDefault());
    other.dispatchEvent(new MouseEvent('click', {bubbles: true, cancelable: true, button: 0}));
    expect(answer).not.toHaveBeenCalled();

    const link = document.createElement('a');
    link.href = '/api/attachments/gone/content';
    document.body.append(link);
    link.dispatchEvent(new MouseEvent('click', {bubbles: true, cancelable: true, button: 0}));
    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("The file couldn't be downloaded (404)."));
    stop();
    other.remove();
    link.remove();
  });
});
