import {PLACE_ATTRIBUTE, rememberReadMePlace, restoreReadMePlace, scrollerOf} from './readMePlace';

/** A scrolling area holding a marked part, at `top` pixels down the window, the area's visible part at `scrollerTop`. */
function layout({top, scrollerTop = 100}: {top: number; scrollerTop?: number}) {
  const scroller = document.createElement('section');
  scroller.style.overflowY = 'auto';
  // jsdom has no layout: the area has more than it shows.
  Object.defineProperty(scroller, 'scrollHeight', {value: 3000});
  Object.defineProperty(scroller, 'clientHeight', {value: 800});
  const part = document.createElement('section');
  part.setAttribute(PLACE_ATTRIBUTE, 'pin-patient');
  scroller.append(part);
  document.body.append(scroller);
  vi.spyOn(scroller, 'getBoundingClientRect').mockReturnValue({top: scrollerTop} as DOMRect);
  // jsdom has no layout: the part moves up as the area scrolls down.
  vi.spyOn(part, 'getBoundingClientRect').mockImplementation(() => ({top: top - scroller.scrollTop}) as DOMRect);
  return {scroller, part};
}

describe('readMePlace', () => {
  afterEach(() => {
    document.body.replaceChildren();
    sessionStorage.clear();
  });

  it('finds the scrolling area around a part, or the page when nothing around it scrolls', () => {
    const {scroller, part} = layout({top: 0});
    expect(scrollerOf(part)).toBe(scroller);

    const alone = document.createElement('section');
    document.body.append(alone);
    expect(scrollerOf(alone)).toBe(document.scrollingElement ?? document.documentElement);
  });

  it('keeps the part clicked, how far down the visible area it was, and where the Read Me is going', () => {
    layout({top: 340});

    rememberReadMePlace(document.body, 'pin-patient', 'right');

    expect(JSON.parse(sessionStorage.getItem('medapp.readMePlace')!)).toMatchObject({place: 'pin-patient', offset: 240, position: 'right'});
  });

  it('puts the part back where it was in the Read Me showing where it was going, then forgets it', () => {
    sessionStorage.setItem('medapp.readMePlace', JSON.stringify({place: 'pin-patient', offset: 240, position: 'right', at: Date.now()}));
    const {scroller} = layout({top: 1500});

    // The Read Me in another position (the left half, a moment before it's replaced) leaves the place alone.
    expect(restoreReadMePlace(document.body, 'whole')).toBe('none');
    expect(sessionStorage.getItem('medapp.readMePlace')).not.toBeNull();

    expect(restoreReadMePlace(document.body, 'right')).toBe('restored');
    expect(scroller.scrollTop).toBe(1500 - 100 - 240);
    expect(sessionStorage.getItem('medapp.readMePlace')).toBeNull();
    expect(restoreReadMePlace(document.body, 'right')).toBe('none');
  });

  it('waits, keeping the place, while the Read Me is not yet laid out in something that scrolls', () => {
    sessionStorage.setItem('medapp.readMePlace', JSON.stringify({place: 'pin-patient', offset: 0, position: 'whole', at: Date.now()}));
    expect(restoreReadMePlace(document.body, 'whole')).toBe('not yet');

    const part = document.createElement('section');
    part.setAttribute(PLACE_ATTRIBUTE, 'pin-patient');
    document.body.append(part);
    expect(restoreReadMePlace(document.body, 'whole')).toBe('not yet');
    expect(sessionStorage.getItem('medapp.readMePlace')).not.toBeNull();
  });

  it('drops a place kept too long ago, or one it cannot read', () => {
    const {scroller} = layout({top: 1500});
    sessionStorage.setItem('medapp.readMePlace', JSON.stringify({place: 'pin-patient', offset: 0, position: 'whole', at: Date.now() - 60_000}));
    expect(restoreReadMePlace(document.body, 'whole')).toBe('none');
    expect(sessionStorage.getItem('medapp.readMePlace')).toBeNull();

    sessionStorage.setItem('medapp.readMePlace', '{not json');
    expect(restoreReadMePlace(document.body, 'whole')).toBe('none');
    expect(scroller.scrollTop).toBe(0);
  });

  it('keeps nothing, and fails nothing, when this browser blocks storage', () => {
    layout({top: 340});
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('Storage is blocked.');
    });

    expect(() => rememberReadMePlace(document.body, 'pin-patient', 'whole')).not.toThrow();
    expect(restoreReadMePlace(document.body, 'whole')).toBe('none');
  });
});
