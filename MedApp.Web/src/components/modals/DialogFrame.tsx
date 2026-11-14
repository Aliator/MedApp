import {Dialog, DialogHeader} from '@astryxdesign/core/Dialog';
import {Layout, LayoutContent, LayoutFooter} from '@astryxdesign/core/Layout';
import {HStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';

export interface DialogFrameProps {
  isOpen: boolean;
  title: string;
  /** Called when the dialog is dismissed (close button, Escape, or backdrop where allowed). */
  onClose: () => void;
  /** Buttons at the end of the footer. */
  footer?: ReactNode;
  width?: number;
  /**
   * 'form' (default) keeps a stray backdrop click from losing what was typed; 'info' allows it;
   * 'required' also blocks Escape and hides the close button, e.g. while a request is running.
   */
  purpose?: 'form' | 'info' | 'required';
  children: ReactNode;
}

/**
 * The frame every app dialog shares (replaces Blazor's Modal): a titled header with a close button, scrolling
 * content, and a footer of buttons. Laid out as Astryx's dialog templates are.
 */
export function DialogFrame({isOpen, title, onClose, footer, width = 440, purpose = 'form', children}: DialogFrameProps) {
  const handleOpenChange = (open: boolean) => {
    if (!open) onClose();
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={handleOpenChange} width={width} purpose={purpose}>
      <Layout
        height="fill"
        padding={4}
        defaultHasDividers
        header={<DialogHeader title={title} onOpenChange={purpose === 'required' ? undefined : handleOpenChange} />}
        content={<LayoutContent>{children}</LayoutContent>}
        footer={
          footer ? (
            <LayoutFooter>
              <HStack gap={2} vAlign="center" hAlign="end">
                {footer}
              </HStack>
            </LayoutFooter>
          ) : undefined
        }
      />
    </Dialog>
  );
}
