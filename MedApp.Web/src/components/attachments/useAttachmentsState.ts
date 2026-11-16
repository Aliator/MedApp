import {useQueryClient} from '@tanstack/react-query';
import {useCallback, useEffect, useMemo, useRef, useState} from 'react';
import {
  attachmentTargetKey,
  checkAttachmentFile,
  isUploadCancelled,
  refreshAfterAttachmentChange,
  uploadAttachment,
  uploadErrorText,
  type AttachmentLimits,
  type AttachmentTarget,
} from '../../api/attachments';
import {useAnnounce} from '../../app/announcements';

/** A file being attached: waiting its turn, on its way, or refused (by the browser's checks or the server). */
export interface AttachmentUpload {
  id: string;
  /** Which target it's for (`attachmentTargetKey`). */
  targetKey: string;
  fileName: string;
  state: 'waiting' | 'uploading' | 'failed';
  /** How much has been sent, from 0 to 1. */
  progress: number;
  /** Why it failed, naming the file; empty while it hasn't. */
  error: string;
}

/**
 * The files being attached, and whether admins are seeing deleted files, for every target on a page. A page holds
 * it (`useAttachmentsState`) and passes it to each `Attachments`, so uploads carry on, and Show deleted stays on, when
 * the page changes layout (a narrow window, a half view) and its sections are drawn again.
 */
export interface AttachmentsState {
  uploads: readonly AttachmentUpload[];
  /** Checks the files, then sends those that pass, one after another; refused ones show why. */
  add: (target: AttachmentTarget, files: readonly File[], limits: AttachmentLimits) => void;
  /** Stops a file being sent, or takes it out of the queue. */
  cancel: (upload: AttachmentUpload) => void;
  /** Clears the failed files shown for a target. */
  dismissFailed: (targetKey: string) => void;
  isShowingDeleted: (targetKey: string) => boolean;
  setShowingDeleted: (targetKey: string, isShowing: boolean) => void;
}

interface Queued {
  id: string;
  target: AttachmentTarget;
  file: File;
}

/**
 * Sends files one at a time, each with its progress, and announces each result to screen readers ("scan.pdf
 * attached"). Leaving the page cancels what's still going.
 */
export function useAttachmentsState(): AttachmentsState {
  const queryClient = useQueryClient();
  const announce = useAnnounce();
  const [uploads, setUploads] = useState<AttachmentUpload[]>([]);
  const [showingDeleted, setShowingDeletedState] = useState<Record<string, boolean>>({});
  const queue = useRef<Queued[]>([]);
  const current = useRef<{id: string; controller: AbortController} | null>(null);
  const isActive = useRef(true);
  const nextId = useRef(0);

  useEffect(() => {
    isActive.current = true;
    return () => {
      isActive.current = false;
      queue.current = [];
      current.current?.controller.abort();
    };
  }, []);

  const update = useCallback(
    (id: string, change: Partial<AttachmentUpload>) =>
      setUploads((list) => list.map((upload) => (upload.id === id ? {...upload, ...change} : upload))),
    [],
  );
  const remove = useCallback((id: string) => setUploads((list) => list.filter((upload) => upload.id !== id)), []);

  /** Sends the queue, a file at a time, until it's empty (or the page is left). */
  const run = useCallback(async () => {
    if (current.current) return;
    for (let next = queue.current.shift(); next && isActive.current; next = queue.current.shift()) {
      const {id, target, file} = next;
      const controller = new AbortController();
      current.current = {id, controller};
      update(id, {state: 'uploading'});
      try {
        await uploadAttachment({target, file, signal: controller.signal, onProgress: (progress) => update(id, {progress})});
        // The row goes once the list has the file, so it never disappears from both.
        await refreshAfterAttachmentChange(queryClient);
        remove(id);
        announce(`${file.name} attached`);
      } catch (error) {
        if (isUploadCancelled(error)) {
          remove(id);
          if (isActive.current) announce(`${file.name} not attached: cancelled`);
        } else {
          const message = `${file.name}: ${uploadErrorText(error)}`;
          update(id, {state: 'failed', error: message});
          announce(message);
        }
      } finally {
        current.current = null;
      }
    }
  }, [announce, queryClient, remove, update]);

  const add = useCallback(
    (target: AttachmentTarget, files: readonly File[], limits: AttachmentLimits) => {
      if (files.length === 0) return;
      const targetKey = attachmentTargetKey(target);
      const rows = files.map((file): AttachmentUpload => {
        const problem = checkAttachmentFile(file, limits);
        const id = `upload-${++nextId.current}`;
        if (!problem) queue.current.push({id, target, file});
        return {id, targetKey, fileName: file.name, state: problem ? 'failed' : 'waiting', progress: 0, error: problem ?? ''};
      });
      setUploads((list) => [...list, ...rows]);
      const refused = rows.filter((row) => row.error).map((row) => row.error);
      const sending = rows.length - refused.length;
      announce(
        [...refused, sending > 0 ? `Attaching ${sending} ${sending === 1 ? 'file' : 'files'}` : null].filter(Boolean).join('. '),
      );
      void run();
    },
    [announce, run],
  );

  const cancel = useCallback(
    ({id, fileName}: AttachmentUpload) => {
      // The one being sent stops, and the loop says so; one waiting just leaves the queue.
      if (current.current?.id === id) {
        current.current.controller.abort();
        return;
      }
      queue.current = queue.current.filter((queued) => queued.id !== id);
      remove(id);
      announce(`${fileName} not attached: cancelled`);
    },
    [announce, remove],
  );

  const dismissFailed = useCallback(
    (targetKey: string) => setUploads((list) => list.filter((upload) => upload.targetKey !== targetKey || upload.state !== 'failed')),
    [],
  );

  const isShowingDeleted = useCallback((targetKey: string) => showingDeleted[targetKey] ?? false, [showingDeleted]);
  const setShowingDeleted = useCallback(
    (targetKey: string, isShowing: boolean) => setShowingDeletedState((current) => ({...current, [targetKey]: isShowing})),
    [],
  );

  return useMemo(
    () => ({uploads, add, cancel, dismissFailed, isShowingDeleted, setShowingDeleted}),
    [uploads, add, cancel, dismissFailed, isShowingDeleted, setShowingDeleted],
  );
}
