import {VStack} from '@astryxdesign/core/Stack';
import {useContext, useEffect, useRef} from 'react';
import {usePageTitle} from '../../app/announcements';
import {SaveShortcutProvider} from '../../components/forms/SaveShortcut';
import {NotepadProvider, useNotepad} from '../../components/notepad/NotepadContext';
import {NotepadPane} from '../../components/notepad/NotepadPane';
import {InHalfPaneContext, useHalfView} from '../../components/pages/halfView';

/** The window's whole height, on the page's colour (there's no AppShell to paint it). */
const WINDOW = {height: '100dvh', backgroundColor: 'var(--color-background-body)'} as const;
/** In a half view: its half's height (the half paints the page's colour). */
const HALF = {height: '100%'} as const;

/**
 * The notepad on a page of its own (`/notepad`), filling it: in a small window of its own (opened from the side nav's
 * Notepad row, with no side nav), or in a half view beside a page (the row's menu). Close saves first, then closes the
 * window, or the half (it stays open if the save fails); the window's browser asks before it's closed with something
 * unsaved. Add Note to Procedure adds to the procedure pages open in MedApp's windows (this one's too, in a half view),
 * after the same confirmation, and saves made here or there keep them in step.
 */
export function NotepadWindowPage() {
  const isInHalf = useContext(InHalfPaneContext);
  const half = useHalfView();
  // In a half view, this page is inside the signed-in layout, whose notepad this is (none in the window).
  const docked = useNotepad();
  usePageTitle('Notepad', !isInHalf);

  // Opened in a half view: the docked notepad (or the sheet) closes, saving first, so it isn't open twice side by side.
  const closedDocked = useRef(false);
  useEffect(() => {
    if (!isInHalf || closedDocked.current || !docked?.isOpen) return;
    closedDocked.current = true;
    docked.requestClose();
  }, [isInHalf, docked]);

  if (isInHalf) {
    // The layout already has the main landmark and Ctrl+S; the notepad is a region within its half.
    return (
      <NotepadProvider isWindow onClose={half.onClose}>
        <VStack gap={0} hAlign="stretch" style={HALF}>
          <NotepadPane />
        </VStack>
      </NotepadProvider>
    );
  }
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
