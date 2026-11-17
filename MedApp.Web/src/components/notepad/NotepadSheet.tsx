import {Drawer} from '@astryxdesign/core/Drawer';
import {useNotepad, useNotepadIsSheet} from './NotepadContext';
import {NotepadPane} from './NotepadPane';

/**
 * The notepad on a narrow window or a phone: a full-screen sheet over the page (an Astryx `Drawer`, the full width).
 * Escape closes it, after saving, as Close does. On a wider window `AppLayout` docks it beside the page instead.
 */
export function NotepadSheet() {
  const notepad = useNotepad();
  const isSheet = useNotepadIsSheet();
  const isOpen = (notepad?.isOpen ?? false) && isSheet;

  return (
    <Drawer
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) notepad?.requestClose();
      }}
      label="Notepad"
      width="100%"
      experimental={{isFullWidthOnMobile: true}}
      purpose="form"
      padding={0}>
      {isOpen && <NotepadPane />}
    </Drawer>
  );
}
