import {findClickable, findTarget, nameOf} from './findTarget';

function page(html: string) {
  document.body.innerHTML = html;
  return document.body;
}

describe('findTarget', () => {
  beforeEach(() => {
    vi.spyOn(Element.prototype, 'getClientRects').mockImplementation(function (this: Element) {
      return (this.closest('[hidden]') ? [] : [{}]) as unknown as DOMRectList;
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = '';
  });

  it('names an element by its aria-label, else its text', () => {
    const root = page('<button aria-label="Close half view">×</button><button> Finish   Stage </button>');
    const [labelled, plain] = root.querySelectorAll('button');
    expect(nameOf(labelled)).toBe('Close half view');
    expect(nameOf(plain)).toBe('Finish Stage');
  });

  it('finds a button by name outside the side nav unless asked for the nav', () => {
    page('<nav><a href="/patients">Patients</a></nav><main><a href="/x">Patients</a></main>');
    expect(findTarget({name: 'Patients', kind: 'link'})).toHaveAttribute('href', '/x');
    expect(findTarget({name: 'Patients', kind: 'link', inNav: true})).toHaveAttribute('href', '/patients');
  });

  it('finds a menu item wherever its menu is', () => {
    page('<nav><div role="menu"><div role="menuitem">Pin</div></div></nav>');
    expect(findTarget({name: 'Pin', kind: 'menuitem'})?.textContent).toBe('Pin');
  });

  it('takes only a match after the named heading', () => {
    page('<h2>Samples</h2><button id="a">Record</button><h2>Consent</h2><button id="b">Record</button>');
    expect(findTarget({name: 'Record', kind: 'button', after: 'Consent'})).toHaveAttribute('id', 'b');
    expect(findTarget({name: 'Record', kind: 'button', after: 'Missing'})).toBeNull();
  });

  it('skips hidden matches and the tour card itself', () => {
    page('<div hidden><button>Next</button></div><div data-coach-card><button>Next</button></div>');
    expect(findTarget({name: 'Next', kind: 'button'})).toBeNull();
  });

  it('widens a heading to its section, and a label to its field', () => {
    page('<div class="astryx-section" id="s"><h2>Consent</h2></div><div id="f"><label>Who</label><button>Choose</button></div>');
    expect(findTarget({name: 'Consent', kind: 'heading', area: true})).toHaveAttribute('id', 's');
    expect(findTarget({name: 'Who', kind: 'field'})).toHaveAttribute('id', 'f');
  });

  it('finds by CSS', () => {
    page('<button aria-label="Notifications, 3 unread"></button>');
    expect(findTarget({css: '[aria-label^="Notifications"]'})).not.toBeNull();
  });

  it('can still click a hidden button to bring its menu up', () => {
    page('<nav><div hidden><button aria-label="Patients options"></button></div></nav>');
    expect(findTarget({name: 'Patients options', kind: 'button', inNav: true})).toBeNull();
    expect(findClickable({name: 'Patients options', kind: 'button', inNav: true})).toHaveAttribute('aria-label', 'Patients options');
  });
});
