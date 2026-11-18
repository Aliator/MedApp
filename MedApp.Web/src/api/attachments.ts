import {keepPreviousData, queryOptions, useMutation, useQueryClient, type QueryClient} from '@tanstack/react-query';
import {formatFileSize} from '../utils/fileSize';
import {api, unwrap, type Schemas} from './client';
import {allMessages, ApiError, parseValidationErrors, problemTitle} from './errors';
import {pathwayKeys} from './pathways';

export type Attachment = Schemas['AttachmentResponse'];
export type AttachmentList = Schemas['AttachmentListResponse'];

/**
 * What files are attached to: a patient, one of their pathways, or a stage of one. A patient's list holds
 * everything attached to them anywhere; a pathway's only the files on the pathway itself; a stage's only its own.
 */
export type AttachmentTarget = {patientId: string} | {pathwayId: string} | {stageId: string};

/** What a file is attached to, as the API names it (`attachedTo`). */
export type AttachedTo = 'Patient' | 'Pathway' | 'Stage';

/** The `pathwayId` (all zeros) of a timeline or audit entry about a patient's own file, which has no pathway. */
export const NO_PATHWAY = '00000000-0000-0000-0000-000000000000';

/** What a target is, as the API names an attachment's `attachedTo`. */
export const attachedToOf = (target: AttachmentTarget): AttachedTo =>
  'patientId' in target ? 'Patient' : 'pathwayId' in target ? 'Pathway' : 'Stage';

/** A target as one string, e.g. "Stage:<id>", to tell apart uploads and settings held for several targets. */
export const attachmentTargetKey = (target: AttachmentTarget) => `${attachedToOf(target)}:${Object.values(target)[0]}`;

/** The most files a list returns, the newest (the API's `AttachmentLimits.MaxListed`); `totalCount` says how many in all. */
export const MAX_LISTED_ATTACHMENTS = 200;

/** What a file must be, until a list has said (the API's own limits come with every list). */
export interface AttachmentLimits {
  maxFileBytes: number;
  /** Lower case, with the dot: ".pdf". */
  allowedExtensions: string[];
}

export const DEFAULT_ATTACHMENT_LIMITS: AttachmentLimits = {
  maxFileBytes: 20 * 1024 * 1024,
  allowedExtensions: ['.pdf', '.png', '.jpg', '.jpeg', '.heic', '.heif', '.docx', '.txt'],
};

/** The limits a list came with, or the defaults before it has loaded. */
export function attachmentLimits(list: AttachmentList | undefined): AttachmentLimits {
  const extensions = list?.allowedExtensions?.map((extension) => extension.toLowerCase()) ?? [];
  return {
    maxFileBytes: list?.maxFileBytes || DEFAULT_ATTACHMENT_LIMITS.maxFileBytes,
    allowedExtensions: extensions.length > 0 ? extensions : DEFAULT_ATTACHMENT_LIMITS.allowedExtensions,
  };
}

/** The file types that can be attached, in words: "PDF, PNG, JPG or TXT". */
export function allowedTypesText(limits: AttachmentLimits): string {
  const names = limits.allowedExtensions.map((extension) => extension.replace(/^\./, '').toUpperCase());
  return names.length > 1 ? `${names.slice(0, -1).join(', ')} or ${names[names.length - 1]}` : (names[0] ?? '');
}

/** A file name's extension, lower case with its dot (".pdf"), or "" when it has none. */
export function fileExtension(name: string): string {
  const dot = name.lastIndexOf('.');
  return dot > 0 && dot < name.length - 1 ? name.slice(dot).toLowerCase() : '';
}

/**
 * Why a file can't be attached, checked in the browser before it's sent, or null if it can be sent. A browser often
 * can't read the server's 413 once it closes the connection on a body that's too large, so size and type are checked
 * here first, with messages that name the file. The server checks again, and its word is final.
 */
export function checkAttachmentFile(file: {name: string; size: number}, limits: AttachmentLimits): string | null {
  const extension = fileExtension(file.name);
  const allowed = allowedTypesText(limits);
  if (!extension) return `${file.name}: files without a type at the end of their name (such as .pdf) can't be attached. Attach ${allowed}.`;
  if (!limits.allowedExtensions.includes(extension))
    return `${file.name}: ${extension.slice(1).toUpperCase()} files can't be attached. Attach ${allowed}.`;
  if (file.size > limits.maxFileBytes)
    return `${file.name} is ${formatFileSize(file.size)}; the most is ${formatFileSize(limits.maxFileBytes)}.`;
  return null;
}

/** The query string that names a target, as the API takes it: `patientId=…`, `pathwayId=…` or `stageId=…`. */
function targetParams(target: AttachmentTarget): Record<string, string> {
  if ('patientId' in target) return {patientId: target.patientId};
  if ('pathwayId' in target) return {pathwayId: target.pathwayId};
  return {stageId: target.stageId};
}

/** Every attachment list is keyed under ['attachments'], so an upload or delete refreshes them all. */
export const attachmentKeys = {all: ['attachments'] as const};

/**
 * The files attached to a target (`GET api/attachments`), newest first, at most 200, with the limits on new ones.
 * `includeDeleted` (admins only) adds deleted files, marked as such. Switching it keeps the list on screen until the
 * other loads.
 */
export function attachmentsQuery(target: AttachmentTarget, includeDeleted = false) {
  const params = targetParams(target);
  return queryOptions({
    queryKey: ['attachments', {...params, ...(includeDeleted ? {includeDeleted: true} : {})}],
    queryFn: () =>
      unwrap(
        api.GET('/api/attachments', {
          params: {
            query: {
              PatientId: params.patientId,
              PathwayId: params.pathwayId,
              StageId: params.stageId,
              ...(includeDeleted ? {IncludeDeleted: true} : {}),
            },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

/** Where a file is downloaded from: always as a download (the API sends `Content-Disposition: attachment`). */
export const attachmentContentUrl = (id: string) => `/api/attachments/${id}/content`;

/** A small re-encoded image of a picture (only when `hasThumbnail`), safe to show in an `<img>`. */
export const attachmentThumbnailUrl = (id: string) => `/api/attachments/${id}/thumbnail`;

/** After a file is attached or deleted: every list of files, and the timelines (which show both). */
export async function refreshAfterAttachmentChange(queryClient: QueryClient) {
  await Promise.all([
    queryClient.invalidateQueries({queryKey: attachmentKeys.all}),
    queryClient.invalidateQueries({queryKey: pathwayKeys.timelines}),
  ]);
}

/** Deletes a file (`DELETE api/attachments/{id}`): hidden at once, removed for good after the retention period. */
export function useDeleteAttachment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE('/api/attachments/{id}', {params: {path: {id}}})),
    onSuccess: () => refreshAfterAttachmentChange(queryClient),
  });
}

/** What a failed upload says, when the server didn't say (or the browser couldn't read what it said). */
const UPLOAD_MESSAGES: Record<number, string> = {
  0: "The file couldn't be sent. Check the connection, and that the file isn't too large, then try again.",
  403: "You can't attach files here.",
  404: 'What the file was being attached to no longer exists.',
  409: "Files can't be attached here now.",
  413: 'The file is too large to attach.',
  415: "Files of this type can't be attached.",
  422: "The file failed its malware scan, so it wasn't attached.",
  503: "File storage isn't set up, so files can't be attached.",
};
const UPLOAD_FALLBACK = "The file couldn't be attached. Please try again.";

function parseJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

/** The message for a failed upload: the server's own (its ProblemDetails title, or a 400's messages), or ours. */
function uploadErrorMessage(status: number, body: unknown): string {
  if (status === 400) {
    const errors = parseValidationErrors(body);
    const messages = allMessages(errors);
    if (messages.length > 0) return messages.join(' ');
  }
  return problemTitle(body) ?? UPLOAD_MESSAGES[status] ?? UPLOAD_FALLBACK;
}

/** The error an upload rejects with when it's cancelled (its signal aborted). */
export const uploadCancelled = () => new DOMException('The upload was cancelled.', 'AbortError');

export const isUploadCancelled = (error: unknown) => error instanceof DOMException && error.name === 'AbortError';

export interface UploadAttachmentOptions {
  target: AttachmentTarget;
  file: File;
  /** How much has been sent, from 0 to 1, as it goes. */
  onProgress?: (fraction: number) => void;
  /** Cancels the upload (it rejects with an AbortError: `isUploadCancelled`). */
  signal?: AbortSignal;
}

/**
 * Attaches a file (`POST api/attachments?patientId=…`), resolving with the new attachment (201). The body is the
 * file's bytes; its name goes in `X-File-Name`, URL-encoded, because a header can't hold other characters. Sent with
 * XMLHttpRequest rather than fetch, which can't report an upload's progress. Same origin, with the session cookie.
 * Rejects with an `ApiError` whose `title` is the message to show (the server's, when it gave one), or an
 * AbortError when cancelled. Browser only.
 */
export function uploadAttachment({target, file, onProgress, signal}: UploadAttachmentOptions): Promise<Attachment> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(uploadCancelled());
      return;
    }
    const xhr = new XMLHttpRequest();
    const abort = () => xhr.abort();
    const settle = (finish: () => void) => {
      signal?.removeEventListener('abort', abort);
      finish();
    };

    xhr.open('POST', `/api/attachments?${new URLSearchParams(targetParams(target)).toString()}`);
    xhr.withCredentials = true;
    xhr.setRequestHeader('Content-Type', 'application/octet-stream');
    xhr.setRequestHeader('X-File-Name', encodeURIComponent(file.name));
    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable && event.total > 0) onProgress?.(event.loaded / event.total);
    };
    xhr.onload = () =>
      settle(() => {
        const body = parseJson(xhr.responseText);
        if (xhr.status >= 200 && xhr.status < 300) {
          onProgress?.(1);
          resolve(body as Attachment);
        } else {
          reject(new ApiError(xhr.status, undefined, uploadErrorMessage(xhr.status, body)));
        }
      });
    xhr.onerror = () => settle(() => reject(new ApiError(0, undefined, UPLOAD_MESSAGES[0])));
    xhr.onabort = () => settle(() => reject(uploadCancelled()));
    signal?.addEventListener('abort', abort, {once: true});
    xhr.send(file);
  });
}

/** What an upload's error says (an ApiError's message to show), whatever it is. */
export const uploadErrorText = (error: unknown) => (error instanceof ApiError && error.title) || UPLOAD_FALLBACK;

/** What kind of file it is, for its icon when it has no thumbnail. */
export type AttachmentKind = 'pdf' | 'image' | 'word' | 'text' | 'other';

export function attachmentKind(attachment: Pick<Attachment, 'contentType' | 'fileName'>): AttachmentKind {
  const type = (attachment.contentType ?? '').toLowerCase();
  const extension = fileExtension(attachment.fileName ?? '');
  if (type === 'application/pdf' || extension === '.pdf') return 'pdf';
  if (type.startsWith('image/')) return 'image';
  if (type.includes('wordprocessingml') || extension === '.docx') return 'word';
  if (type.startsWith('text/')) return 'text';
  return 'other';
}
