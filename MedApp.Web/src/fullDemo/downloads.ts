import type {BackendResponse} from './backend';
import {answerBytes, downloadName, headerOf, isApiUrl, apiPath} from './http';

/**
 * Downloads in the full demo: a link to the API (a file's Download, `/api/attachments/{id}/content`, which the browser
 * would fetch from the network) is answered by the backend instead, and its file saved under the name the API gives
 * (`Content-Disposition`), as the browser would save it. Always as a download: never opened in the page.
 */
export function installDownloads(answer: (path: string) => Promise<BackendResponse>, target: Document = document): () => void {
  const onClick = (event: MouseEvent) => {
    if (event.defaultPrevented || event.button !== 0) return;
    const link = (event.target as Element | null)?.closest?.('a[href]') as HTMLAnchorElement | null;
    if (!link || !isApiUrl(link.href)) return;
    event.preventDefault();
    void saveDownload(answer, apiPath(link.href), link.getAttribute('download') || null);
  };
  target.addEventListener('click', onClick, true);
  return () => target.removeEventListener('click', onClick, true);
}

/** Fetches one file from the backend and saves it. A refusal is logged (the page has nothing to show it in). */
export async function saveDownload(answer: (path: string) => Promise<BackendResponse>, path: string, fallbackName: string | null): Promise<void> {
  const response = await answer(path);
  if (response.status < 200 || response.status >= 300) {
    console.error(`The file couldn't be downloaded (${response.status}).`);
    return;
  }
  const blob = new Blob([answerBytes(response)], {type: headerOf(response.headers, 'Content-Type') ?? 'application/octet-stream'});
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = downloadName(response.headers) ?? fallbackName ?? 'download';
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
