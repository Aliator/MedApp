/**
 * An open dialog: one over the whole window (Astryx keeps a closed one in the page, without `open`), or one over its
 * own half of a half view or beside the docked notepad (`RegionDialog`, marked while open).
 */
export const OPEN_DIALOG = 'dialog[open], [data-open-dialog]';

/** The attributes that say a dialog is open, for watching them change (`MutationObserver`). */
export const OPEN_DIALOG_ATTRIBUTES = ['open', 'data-open-dialog'];

/** Whether any dialog is open. */
export const isDialogOpen = (document: Document = globalThis.document) => document.querySelector(OPEN_DIALOG) !== null;
