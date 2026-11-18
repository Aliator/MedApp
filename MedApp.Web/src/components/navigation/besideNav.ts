/** What `placeBesideNav` places, and by what. */
export interface BesideNavPlacement {
  /** The open menu or panel: its content, or the top-layer element that holds it. */
  panel: HTMLElement;
  /**
   * The nav row it opens from (its highlight, as wide as the nav's rows): it opens beside the nav, out from its edge by as
   * much as this row is in from it. In a folded nav's flyout (a row brought out over the page), beside that row instead.
   */
  row: HTMLElement;
  /** What its top is level with: the row's, unless the control sits on a line of its own (the folded nav's bell). */
  levelWith?: HTMLElement;
  /** The app's own mode: the nav is always dark, but what opens from it isn't. */
  mode: string;
}

/**
 * Sets a menu or panel opened from the side nav where a nav row's menu goes (`RowMenu`), so they all open alike: beside
 * the nav, as far out from its edge as the row's highlight is in from it, and level with the row; in the app's own mode
 * and on the cards' colour, not the nav's. Astryx anchors a popover to its button and hosts it in the nav (outside any
 * Theme put round it), so it's moved once it's open and its colours switched where it is.
 *
 * Returns the panel's surface once it's placed, or null while it isn't laid out yet (try again next frame).
 */
export function placeBesideNav({panel: content, row, levelWith = row, mode}: BesideNavPlacement): HTMLElement | null {
  const nav = row.closest<HTMLElement>('.astryx-side-nav');
  const panel = content.closest<HTMLElement>('[popover]') ?? content;
  if (!nav) return null;
  (panel.closest<HTMLElement>('[data-astryx-layer-root]') ?? panel).style.colorScheme = mode;
  // The panel's own surface: the popover element itself or inside it, never one round it (a folded nav's flyout's).
  const surface = panel.matches('.astryx-popover') ? panel : (panel.querySelector<HTMLElement>('.astryx-popover') ?? panel);
  surface.style.backgroundColor = 'var(--medapp-card-background)';
  panel.style.translate = '';
  const box = panel.getBoundingClientRect();
  if (box.width === 0) return null;
  // The nav's edge; in a folded nav's flyout, the row's own (the flyout is just its rows). The gap is the nav's inset of
  // its rows.
  const navRight = nav.getBoundingClientRect().right;
  const rowRight = row.getBoundingClientRect().right;
  const edge = row.closest('[popover]') ? rowRight : navRight;
  const dx = edge + (navRight - rowRight) - box.left;
  const dy = levelWith.getBoundingClientRect().top - box.top;
  panel.style.translate = `${dx}px ${dy}px`;
  return surface;
}
