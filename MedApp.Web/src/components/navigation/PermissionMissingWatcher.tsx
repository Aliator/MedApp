import {useToast} from '@astryxdesign/core/Toast';
import {useQueryClient} from '@tanstack/react-query';
import {useEffect, useRef} from 'react';
import {onPermissionRefused} from '../../api/client';
import {whoamiQuery} from '../../auth/auth';
import {isPermission, needsPermissionText} from '../../auth/permissions';

/** The toast's words for a refusal: the API's own, or failing those, the app's copy of what the permission allows. */
export function permissionRefusedText(permission: string | undefined, message: string | undefined): string {
  if (message) return message;
  return `Your roles don't allow this.${isPermission(permission) ? ` ${needsPermissionText(permission)}` : ''}`;
}

/** Whether a dialog is open (closed Astryx dialogs stay in the page without `open`). */
const isDialogOpen = () => document.querySelector('dialog[open]') !== null;

/**
 * Runs `action` once no dialog is open: now, or as soon as the last one closes. Returns a function that cancels it.
 */
export function whenNoDialogIsOpen(action: () => void): () => void {
  if (!isDialogOpen()) {
    action();
    return () => {};
  }
  const observer = new MutationObserver(() => {
    if (isDialogOpen()) return;
    observer.disconnect();
    action();
  });
  observer.observe(document.body, {subtree: true, childList: true, attributes: true, attributeFilter: ['open']});
  return () => observer.disconnect();
}

/**
 * When the API refuses a request because the person's roles don't grant a permission (someone took it from one of
 * their roles since the page loaded, or the page offered something it shouldn't have), this says so in a toast,
 * naming what's missing, and reads whoami again, so everything that permission allows is hidden (`useCan`).
 *
 * It waits for any open dialog to close before reading whoami: the dialog may sit under something that's about to be
 * hidden, and what was typed in it must stay there to be copied out (decision 61 of `docs/centre-phase2-plan.md`).
 * The dialog shows the API's words too (`problemMessage`), as it shows any refusal. In the signed-in layout.
 */
export function PermissionMissingWatcher() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const toastRef = useRef(toast);
  useEffect(() => {
    toastRef.current = toast;
  });

  useEffect(() => {
    let cancelRefresh: (() => void) | null = null;
    const stop = onPermissionRefused(({permission, message}) => {
      toastRef.current({
        body: permissionRefusedText(permission, message),
        type: 'error',
        // One at a time for each permission, however many requests it refused.
        uniqueID: `permission-missing-${permission ?? 'unknown'}`,
        isAutoHide: true,
        autoHideDuration: 10000,
      });
      cancelRefresh?.();
      cancelRefresh = whenNoDialogIsOpen(() => {
        cancelRefresh = null;
        void queryClient.invalidateQueries({queryKey: whoamiQuery.queryKey});
      });
    });
    return () => {
      stop();
      cancelRefresh?.();
    };
  }, [queryClient]);

  return null;
}
