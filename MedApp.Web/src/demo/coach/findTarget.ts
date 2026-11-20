export type CoachTarget =
  | {css: string; area?: boolean}
  | {
      name: string;
      kind?: TargetKind;
      after?: string;
      inNav?: boolean;
      area?: boolean;
    };

export type TargetKind = 'button' | 'link' | 'menuitem' | 'heading' | 'field' | 'tab' | 'any';

const KINDS: Record<TargetKind, string> = {
  button: 'button, [role=button], [role=radio], [role=combobox]',
  link: 'a',
  menuitem: '[role=menuitem]',
  heading: 'h1, h2, h3',
  field: 'label',
  tab: '[role=tab]',
  any: 'button, a, [role=menuitem], [role=tab], h1, h2, h3, label, [role=region], [role=textbox]',
};

export const COACH_CARD_ATTRIBUTE = 'data-coach-card';

const normalise = (text: string) => text.replace(/\s+/g, ' ').trim();

export function nameOf(element: Element): string {
  return normalise(element.getAttribute('aria-label') ?? (element as HTMLElement).innerText ?? element.textContent ?? '');
}

export function isShown(element: Element): boolean {
  if (element.getClientRects().length === 0) return false;
  const style = getComputedStyle(element);
  return style.visibility !== 'hidden' && style.display !== 'none';
}

const inCard = (element: Element) => element.closest(`[${COACH_CARD_ATTRIBUTE}]`) !== null;
const inNav = (element: Element) => element.closest('nav') !== null;
const placeMatches = (element: Element, target: {kind?: TargetKind; inNav?: boolean}) =>
  target.kind === 'menuitem' || inNav(element) === (target.inNav ?? false);

function widen(element: Element, area: boolean | undefined): Element {
  if (area) return element.closest('.astryx-section') ?? element;
  if (element.tagName === 'LABEL') return element.parentElement ?? element;
  return element;
}

export function findTarget(target: CoachTarget, root: ParentNode = document): Element | null {
  if ('css' in target) {
    const found = [...root.querySelectorAll(target.css)].find((e) => isShown(e) && !inCard(e));
    return found ? widen(found, target.area) : null;
  }
  const heading = target.after
    ? [...root.querySelectorAll('h1, h2, h3')].find((h) => isShown(h) && nameOf(h) === target.after)
    : undefined;
  if (target.after && !heading) return null;
  const found = [...root.querySelectorAll(KINDS[target.kind ?? 'any'])].find(
    (e) =>
      nameOf(e) === target.name &&
      placeMatches(e, target) &&
      !inCard(e) &&
      isShown(e) &&
      (!heading || (heading.compareDocumentPosition(e) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0),
  );
  return found ? widen(found, target.area) : null;
}

export function findClickable(target: CoachTarget, root: ParentNode = document): HTMLElement | null {
  const shown = findTarget(target, root);
  if (shown) return shown as HTMLElement;
  if ('css' in target) return root.querySelector<HTMLElement>(target.css);
  return (
    [...root.querySelectorAll<HTMLElement>(KINDS[target.kind ?? 'any'])].find(
      (e) => nameOf(e) === target.name && placeMatches(e, target) && !inCard(e),
    ) ?? null
  );
}
