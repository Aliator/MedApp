import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../api/preload';
import {settingsQuery} from '../api/settings';
import {requireSignedIn} from '../auth/auth';
import {NotepadPage} from '../pages/notepad/NotepadPage';

/**
 * The notepad's page, which a half view shows beside a page (the side nav's Notepad row's menu). Outside the signed-in
 * layout (no side nav, and no notepad inside the notepad: in a half view it's inside the main page's), but behind the
 * same guard, so holds still apply. Opened on its own, it goes to the start page with the notepad docked, so the
 * notepad isn't read for it here (the half view's notepad reads it as it opens).
 */
export const Route = createFileRoute('/notepad')({
  beforeLoad: requireSignedIn,
  loader: ({context}) => preload(context.queryClient, settingsQuery),
  component: NotepadPage,
});
