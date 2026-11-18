import {Center} from '@astryxdesign/core/Center';
import {Spinner} from '@astryxdesign/core/Spinner';
import {VStack} from '@astryxdesign/core/Stack';
import {useNavigate} from '@tanstack/react-router';
import {useContext, useEffect, useRef} from 'react';
import {NotepadProvider, storeNotepadOpen, useNotepad} from '../../components/notepad/NotepadContext';
import {NotepadPane} from '../../components/notepad/NotepadPane';
import {InHalfPaneContext, useHalfView} from '../../components/pages/halfView';

/** Its half's height (the half paints the page's colour). */
const HALF = {height: '100%'} as const;

/**
 * The notepad's page (`/notepad`), shown in a half view beside a page (the side nav's Notepad row's menu): the notepad
 * filling its half. Close saves first, then closes the half (it stays open if the save fails). Add Note to Pathway
 * adds to the pathway page beside it (the signed-in layout's), after the same confirmation as the docked notepad.
 *
 * It isn't a page of its own: opened on its own (the address typed, or a window kept from when the notepad had one),
 * it goes to the start page with the notepad docked there.
 */
export function NotepadPage() {
  return useContext(InHalfPaneContext) ? <NotepadInHalf /> : <ToDockedNotepad />;
}

const noop = () => {};

function NotepadInHalf() {
  const half = useHalfView();
  // This page is inside the signed-in layout, whose notepad this is.
  const docked = useNotepad();

  // As it opens, the docked notepad (or the sheet) closes, saving first, so it isn't open twice side by side.
  const closedDocked = useRef(false);
  useEffect(() => {
    if (closedDocked.current || !docked?.isOpen) return;
    closedDocked.current = true;
    docked.requestClose();
  }, [docked]);

  // The layout already has the main landmark and Ctrl+S; the notepad is a region within its half.
  return (
    <NotepadProvider onCloseHalf={half.onClose ?? noop}>
      <VStack gap={0} hAlign="stretch" style={HALF}>
        <NotepadPane />
      </VStack>
    </NotepadProvider>
  );
}

/**
 * `/notepad` on its own: to the start page, with the notepad docked there (the signed-in layout reads that it's open as
 * it mounts). In an effect, as the server can't know this browser's storage; its render is just a spinner.
 */
function ToDockedNotepad() {
  const navigate = useNavigate();
  const isSent = useRef(false);

  useEffect(() => {
    if (isSent.current) return;
    isSent.current = true;
    storeNotepadOpen(true);
    void navigate({to: '/', replace: true});
  }, [navigate]);

  return (
    <Center axis="both" padding={10}>
      <Spinner size="lg" label="Opening the notepad..." />
    </Center>
  );
}
