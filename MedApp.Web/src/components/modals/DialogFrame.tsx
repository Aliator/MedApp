import {Dialog, DialogHeader} from '@astryxdesign/core/Dialog';
import {Layout, LayoutContent, LayoutFooter} from '@astryxdesign/core/Layout';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {useContext, useState, type ReactNode} from 'react';
import {DialogRegionContext, OverWholeWindowContext, useDialogRegion} from '../pages/halfView';
import {RegionDialog} from './RegionDialog';

export interface DialogFrameProps {
  isOpen: boolean;
  title: string;
  /** Shown at the start of the header, before the centred title (e.g. ConfirmDialog's icon). */
  icon?: ReactNode;
  /** Called when the dialog is dismissed (close button, Escape, or backdrop where allowed). */
  onClose: () => void;
  /** Buttons at the end of the footer. */
  footer?: ReactNode;
  /** Something at the start of the footer, before its buttons, e.g. a summary of what the buttons will do. */
  footerStart?: ReactNode;
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
 *
 * Where it sits: opened from a page with something beside it (a half of a half view, or the page beside the docked
 * notepad), it covers only that page's region (`RegionDialog`), so the rest stays usable, to copy from; what's in it
 * then lays out by that region. Otherwise (a page on its own, a phone) it's Astryx's modal dialog over the whole
 * window, laid out by the window. Which it is is kept while it's open, so the layout changing beside it (the notepad
 * closed) never rebuilds it, losing what was typed.
 */
export function DialogFrame({
  isOpen,
  title,
  icon,
  onClose,
  footer,
  footerStart,
  width = 440,
  purpose = 'form',
  children,
}: DialogFrameProps) {
  const handleOpenChange = (open: boolean) => {
    if (!open) onClose();
  };

  // Over its own region while something is beside the page; decided as it opens, and kept until it closes.
  const region = useContext(DialogRegionContext);
  const isOverRegionNow = useDialogRegion() !== null;
  const [isOverRegionWhileOpen, setIsOverRegionWhileOpen] = useState<boolean | null>(isOpen ? isOverRegionNow : null);
  if (isOpen && isOverRegionWhileOpen === null) setIsOverRegionWhileOpen(isOverRegionNow);
  if (!isOpen && isOverRegionWhileOpen !== null) setIsOverRegionWhileOpen(null);
  const isOverRegion = (isOverRegionWhileOpen ?? isOverRegionNow) && region?.element != null && region.host != null;

  const frame = (
    // Over the whole window, what's in it lays out by the window; over a region, by the region, as the page does.
    <OverWholeWindowContext.Provider value={!isOverRegion}>
      <Layout
        height="fill"
        padding={4}
        defaultHasDividers
        header={
          <DialogHeader
            title={title}
            startContent={icon}
            onOpenChange={purpose === 'required' ? undefined : handleOpenChange}
          />
        }
        content={<LayoutContent>{children}</LayoutContent>}
        footer={
          footer ? (
            <LayoutFooter>
              <HStack gap={2} vAlign="center" hAlign="end" wrap={footerStart ? 'wrap' : 'nowrap'}>
                {footerStart && <StackItem size="fill">{footerStart}</StackItem>}
                {/* One group, so on a narrow window the buttons move under the summary together, not one at a time. */}
                <HStack gap={2} vAlign="center" hAlign="end" wrap="wrap">
                  {footer}
                </HStack>
              </HStack>
            </LayoutFooter>
          ) : undefined
        }
      />
    </OverWholeWindowContext.Provider>
  );

  return isOverRegion && region ? (
    <RegionDialog isOpen={isOpen} onOpenChange={handleOpenChange} title={title} width={width} purpose={purpose} region={region}>
      {frame}
    </RegionDialog>
  ) : (
    <Dialog isOpen={isOpen} onOpenChange={handleOpenChange} width={width} purpose={purpose}>
      {frame}
    </Dialog>
  );
}
