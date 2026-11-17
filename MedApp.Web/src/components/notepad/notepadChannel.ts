import type {OpenProcedure} from './NotepadContext';

/**
 * The channel MedApp's windows keep their notepads in step on (a `BroadcastChannel`): only between MedApp's own windows
 * in this browser (the same origin), carrying what's already in their memory, and storing nothing.
 */
export const NOTEPAD_CHANNEL = 'medapp.notepad';

export type NotepadMessage =
  /** A window saved the notepad: its version now. */
  | {type: 'saved'; version: number}
  /** A window's procedure pages open now, for the notepad's own window to add notes to (none as the window goes). */
  | {type: 'procedures'; windowId: string; procedures: OpenProcedure[]}
  /** The notepad's own window, starting, asks the others for their procedure pages. */
  | {type: 'ask-procedures'}
  /** A note was added to this procedure from a notepad, so pages showing it read it afresh. */
  | {type: 'note-added'; procedureId: string};

/**
 * Opens the channel, passing each message from another window to `onMessage`. Null where the browser has no
 * `BroadcastChannel` (and on the server): the notepad then just works in its own window. Call it in an effect, never
 * while rendering, and close it in the effect's clean-up.
 */
export function openNotepadChannel(onMessage: (message: NotepadMessage) => void): BroadcastChannel | null {
  if (typeof BroadcastChannel === 'undefined') return null;
  const channel = new BroadcastChannel(NOTEPAD_CHANNEL);
  channel.onmessage = (event: MessageEvent<NotepadMessage>) => {
    if (event.data && typeof event.data === 'object' && 'type' in event.data) onMessage(event.data);
  };
  return channel;
}

/** Posts a message, if the channel is open (posting on one just closed throws). */
export function postNotepadMessage(channel: BroadcastChannel | null, message: NotepadMessage) {
  try {
    channel?.postMessage(message);
  } catch {
    // Closed meanwhile: there's nobody left to tell from here.
  }
}

/** A new ID for this window, so the notepad's own window can keep each window's procedure pages apart. */
export function newWindowId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`;
}
