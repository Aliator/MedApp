import {createFileRoute} from '@tanstack/react-router';
import {notepadQuery} from '../api/notepad';
import {preload} from '../api/preload';
import {settingsQuery} from '../api/settings';
import {requireSignedIn} from '../auth/auth';
import {NotepadWindowPage} from '../pages/notepad/NotepadWindowPage';

/**
 * The notepad's own window, opened from the side nav's Notepad row. Outside the signed-in layout (no side nav, and no
 * notepad inside the notepad), but behind the same guard, so holds still apply. It renders with the account's settings
 * (its light or dark mode, colours and text size) and the notepad.
 */
export const Route = createFileRoute('/notepad')({
  beforeLoad: requireSignedIn,
  loader: ({context}) => preload(context.queryClient, settingsQuery, notepadQuery()),
  component: NotepadWindowPage,
});
