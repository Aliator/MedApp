import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/** The signed-in user's notepad: its text (Markdown), when it was last saved, its version, and how long it's kept. */
export type Notepad = Schemas['NotepadResponse'];

/**
 * Your notepad (`GET api/me/notepad`), kept on the server with your account, never in the browser (it may hold patient
 * details). Empty, version 0, when you have none, or it was cleared after `keepDays` without a change. Read once as
 * the notepad opens; the pane keeps its own copy while you type. Signing out clears the query cache, so nothing of
 * it is left in this browser.
 */
export function notepadQuery() {
  return queryOptions({
    queryKey: ['notepad'] as const,
    queryFn: () => unwrap(api.GET('/api/me/notepad')),
    staleTime: Infinity,
  });
}

/**
 * Saves the whole notepad from the version last read or saved (`PUT api/me/notepad`). A 409 when it was saved
 * somewhere else meanwhile (another tab), or cleared after its days without a change; its message says which.
 */
export function useSaveNotepad() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (body: {text: string; expectedVersion: number}) => unwrap(api.PUT('/api/me/notepad', {body})),
    onSuccess: (notepad) => queryClient.setQueryData(notepadQuery().queryKey, notepad),
  });
}
