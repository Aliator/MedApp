import {Center} from '@astryxdesign/core/Center';
import {Dialog, type DialogPurpose} from '@astryxdesign/core/Dialog';
import {useFocusTrap} from '@astryxdesign/core/hooks';
import {useLayerDismissal} from '@astryxdesign/core/Layer';
import {useEffect, useLayoutEffect, useRef, type ReactNode, type SyntheticEvent} from 'react';
import {createPortal} from 'react-dom';
import type {DialogRegion} from '../pages/halfView';

export interface RegionDialogProps {
  isOpen: boolean;
  /** Called with false when the dialog asks to close (Escape, or a click on its backdrop where `purpose` allows it). */
  onOpenChange: (isOpen: boolean) => void;
  /** The dialog's name for screen readers: its title. */
  title: string;
  width: number;
  purpose: DialogPurpose;
  /** The region it covers, and where it's rendered. */
  region: DialogRegion;
  children: ReactNode;
}

/**
 * Above the page and its sticky header (10); below the app's own overlays (toasts, popovers and menus are in the
 * browser's top layer, above everything). Each dialog opened takes the next number, so one opened from another is
 * above it.
 */
const BASE_Z_INDEX = 400;
let opened = 0;

/** The region's area on screen, or the main area's when it has no box of its own (the page with nothing beside it). */
function areaOf(element: HTMLElement): DOMRect {
  const own = element.getBoundingClientRect();
  if (own.width > 0 && own.height > 0) return own;
  return element.closest('[role="main"]')?.getBoundingClientRect() ?? own;
}

/** How many dialogs cover each region: it stays inert until the last one closes. */
const covering = new WeakMap<HTMLElement, number>();

function cover(element: HTMLElement) {
  covering.set(element, (covering.get(element) ?? 0) + 1);
  element.setAttribute('inert', '');
}

function uncover(element: HTMLElement) {
  const count = (covering.get(element) ?? 1) - 1;
  if (count > 0) {
    covering.set(element, count);
    return;
  }
  covering.delete(element);
  element.removeAttribute('inert');
}

/**
 * The field the dialog asks to start on (`data-autofocus`, as Astryx's Dialog does), else its title, else the dialog.
 */
function focusStart(surface: HTMLElement) {
  const target =
    surface.querySelector<HTMLElement>('[data-autofocus]:not([data-autofocus="dialog-title"])') ??
    surface.querySelector<HTMLElement>('h2[tabindex="-1"]') ??
    surface;
  target.focus();
}

/**
 * Presses inside the dialog don't reach handlers around where it's written (a clickable row or card), as with Astryx's
 * Dialog: stopped in React's tree only, so the page's own listeners (a menu's outside press) still hear them.
 */
const stopInReactTree = (event: SyntheticEvent) => {
  event.isPropagationStopped = () => true;
};
const PRESS_BOUNDARY = Object.fromEntries(
  [
    'onClick',
    'onAuxClick',
    'onDoubleClick',
    'onContextMenu',
    'onMouseDown',
    'onMouseUp',
    'onPointerDown',
    'onPointerUp',
    'onTouchStart',
    'onTouchEnd',
  ].map((name) => [name, stopInReactTree]),
);

/** Over the region, which it's laid over by its measured area (set in `useLayoutEffect`). */
const OVERLAY = {
  position: 'fixed',
  inset: 0,
  backgroundColor: 'var(--color-overlay)',
  overscrollBehavior: 'contain',
} as const;

/**
 * A dialog over one region of the window only (a half of a half view, or the page beside the docked notepad), so what's
 * beside it stays usable: text in the other half or the notepad can be selected and copied into it. Astryx's Dialog
 * is always modal (`showModal`, which makes the rest of the window inert), so this lays Astryx's dialog surface
 * (`Dialog` rendered in place) over the region, with what the modal one gives:
 * - a backdrop over the region only, centring the dialog in it; the page under it is inert while it's open;
 * - focus moved into it as it opens, Tab kept within it (`useFocusTrap`), and back to what opened it as it closes;
 * - Escape through Astryx's shared layer stack (`useLayerDismissal`), so a picker or menu open in it closes first;
 *   Escape pressed while working beside it (the last click or focus elsewhere in the app) leaves it open;
 * - a click on the backdrop closes it only for `purpose` 'info'; 'required' blocks Escape.
 * Its own pickers and menus are in the browser's top layer, above it. It's rendered in the region's host (after the
 * app shell), and stays there while closed, hidden, keeping what's in it, as Astryx's does.
 */
export function RegionDialog({isOpen, onOpenChange, title, width, purpose, region, children}: RegionDialogProps) {
  const {containerRef: overlayRef} = useFocusTrap<HTMLDivElement>({isActive: isOpen});
  // Whether the person is working in the dialog (or its popups) rather than beside it: Escape closes it only then.
  const isEngaged = useRef(true);
  const pressStartedOnBackdrop = useRef(false);
  const element = region.element;

  // While open: laid over the region (following it as the window or the layout changes), above any dialog already
  // open, with the page under it inert, and the focus in it; as it closes, the focus goes back to what opened it.
  useLayoutEffect(() => {
    const overlay = overlayRef.current;
    if (!isOpen || !overlay || !element) return;
    const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    opened += 1;
    overlay.style.zIndex = String(BASE_Z_INDEX + opened);
    const place = () => {
      const area = areaOf(element);
      if (area.width === 0 && area.height === 0) return;
      Object.assign(overlay.style, {
        top: `${area.top}px`,
        left: `${area.left}px`,
        width: `${area.width}px`,
        height: `${area.height}px`,
        bottom: 'auto',
        right: 'auto',
      });
    };
    place();
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(place);
    observer?.observe(element);
    const main = element.closest('[role="main"]');
    if (main) observer?.observe(main);
    window.addEventListener('resize', place);

    cover(element);
    isEngaged.current = true;
    // Astryx's dialog surface (it takes no ref when in place).
    if (overlay.firstElementChild instanceof HTMLElement) focusStart(overlay.firstElementChild);

    return () => {
      observer?.disconnect();
      window.removeEventListener('resize', place);
      uncover(element);
      const active = document.activeElement;
      const isFocusLost = active === null || active === document.body || overlay.contains(active);
      if (isFocusLost && opener?.isConnected) opener.focus();
    };
  }, [isOpen, element, overlayRef]);

  // What the person last pressed or focused: in the dialog or a popup (the top layer), or elsewhere in the app.
  useEffect(() => {
    if (!isOpen) return;
    const note = (event: Event) => {
      const target = event.target;
      if (!(target instanceof Element)) return;
      isEngaged.current = overlayRef.current?.contains(target) === true || target.closest('[popover], dialog') !== null;
    };
    document.addEventListener('pointerdown', note, true);
    document.addEventListener('focusin', note, true);
    return () => {
      document.removeEventListener('pointerdown', note, true);
      document.removeEventListener('focusin', note, true);
    };
  }, [isOpen, overlayRef]);

  useLayerDismissal({
    isActive: isOpen,
    escapeBehavior: purpose === 'required' ? 'block' : 'close',
    onDismiss: () => onOpenChange(false),
    getContainer: () => overlayRef.current,
    isPresent: () => isEngaged.current,
  });

  const host = region.host;
  if (!host) return null;
  return createPortal(
    <Center
      ref={overlayRef}
      padding={4}
      data-region-dialog=""
      data-open-dialog={isOpen ? '' : undefined}
      style={isOpen ? OVERLAY : {...OVERLAY, display: 'none'}}
      {...PRESS_BOUNDARY}
      onPointerDownCapture={(event) => {
        pressStartedOnBackdrop.current = event.target === event.currentTarget;
      }}
      onClick={(event) => {
        stopInReactTree(event);
        if (purpose === 'info' && pressStartedOnBackdrop.current && event.target === event.currentTarget) onOpenChange(false);
      }}>
      {/* Astryx's dialog surface, in place: it has no modal behaviour of its own, which the frame around it gives. */}
      <Dialog
        isInline
        isOpen
        onOpenChange={onOpenChange}
        width={width}
        purpose={purpose}
        role={purpose === 'required' ? 'alertdialog' : 'dialog'}
        aria-label={title}
        tabIndex={-1}>
        {children}
      </Dialog>
    </Center>,
    host,
  );
}
