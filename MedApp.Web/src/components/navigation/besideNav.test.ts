import {placeBesideNav} from './besideNav';

/** Gives an element a fixed box, as jsdom lays nothing out. */
function at(element: HTMLElement, {left, top, right, bottom}: {left: number; top: number; right: number; bottom: number}) {
  element.getBoundingClientRect = () =>
    ({left, top, right, bottom, width: right - left, height: bottom - top, x: left, y: top, toJSON: () => ({})}) as DOMRect;
}

/** A side nav 260px wide with one row (its highlight inset 8px from each edge), and a menu Astryx put at (300, 100). */
function nav() {
  const navElement = document.createElement('nav');
  navElement.className = 'astryx-side-nav';
  const row = document.createElement('div');
  navElement.append(row);
  const panel = document.createElement('div');
  panel.setAttribute('popover', '');
  panel.className = 'astryx-popover';
  const content = document.createElement('div');
  panel.append(content);
  document.body.append(navElement, panel);
  at(navElement, {left: 0, top: 0, right: 260, bottom: 900});
  at(row, {left: 8, top: 260, right: 252, bottom: 292});
  at(panel, {left: 300, top: 100, right: 500, bottom: 200});
  return {navElement, row, panel, content};
}

describe('placeBesideNav', () => {
  afterEach(() => document.body.replaceChildren());

  it('moves the panel beside the nav, as far out as the row is in, level with the row, on the cards’ colour', () => {
    const {row, panel, content} = nav();

    const surface = placeBesideNav({panel: content, row, mode: 'light'});

    expect(surface).toBe(panel);
    // To 268 (the nav's edge, 260, and the row's inset, 8) and 260 (the row's top).
    expect(panel.style.translate).toBe('-32px 160px');
    expect(panel.style.backgroundColor).toBe('var(--medapp-card-background)');
    expect(panel.style.colorScheme).toBe('light');
  });

  it('is level with another element when asked (a control on a line of its own)', () => {
    const {row, panel, content} = nav();
    const bell = document.createElement('button');
    row.append(bell);
    at(bell, {left: 216, top: 10, right: 244, bottom: 38});

    placeBesideNav({panel: content, row, levelWith: bell, mode: 'dark'});

    expect(panel.style.translate).toBe('-32px -90px');
  });

  it('waits while the panel isn’t laid out, or isn’t from a nav', () => {
    const {row, panel, content} = nav();
    at(panel, {left: 0, top: 0, right: 0, bottom: 0});
    expect(placeBesideNav({panel: content, row, mode: 'light'})).toBeNull();

    const loose = document.createElement('div');
    expect(placeBesideNav({panel: content, row: loose, mode: 'light'})).toBeNull();
  });
});
