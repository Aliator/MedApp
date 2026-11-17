import {VStack} from '@astryxdesign/core/Stack';
import {usePageTitle} from '../../app/announcements';
import {SaveShortcutProvider} from '../../components/forms/SaveShortcut';
import {NotepadProvider} from '../../components/notepad/NotepadContext';
import {NotepadPane} from '../../components/notepad/NotepadPane';

/** The window's whole height, on the page's colour (there's no AppShell to paint it). */
const WINDOW = {height: '100dvh', backgroundColor: 'var(--color-background-body)'} as const;

/**
 * The notepad in a small window of its own (`/notepad`, opened from the side nav's Notepad row): the notepad, filling
 * the window, with no side nav. Close saves first, then closes the window (it stays open if the save fails), and the
 * browser asks before the window is closed with something unsaved. Add Note to Procedure adds to the procedure pages
 * open in MedApp's other windows, after the same confirmation, and saves made here or there keep both in step.
 */
export function NotepadWindowPage() {
  usePageTitle('Notepad', true);
  return (
    <NotepadProvider isWindow>
      {/* Ctrl+S saves it, as in the signed-in layout. */}
      <SaveShortcutProvider>
        <VStack gap={0} hAlign="stretch" role="main" style={WINDOW}>
          <NotepadPane />
        </VStack>
      </SaveShortcutProvider>
    </NotepadProvider>
  );
}
