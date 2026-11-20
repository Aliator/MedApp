import {TOUR} from '../tour';
import {COACH_MARKS} from './coachMarks';
import {marksFor, nextPosition, pageForMark, placeCard, previousPosition} from './CoachTour';

describe('COACH_MARKS', () => {
  it('has highlights for every step of the tour, and none for a step that is gone', () => {
    for (const step of TOUR) expect(COACH_MARKS[step.key]?.length, step.key).toBeGreaterThan(0);
    expect(Object.keys(COACH_MARKS).sort()).toEqual(TOUR.map((s) => s.key).sort());
  });

  it('offers the sample text only on a step that has some', () => {
    for (const step of TOUR)
      for (const mark of COACH_MARKS[step.key]) if (mark.showsSampleText) expect(step.sampleText, step.key).toBeDefined();
  });

  it('opens a step’s page on its first highlight, or the one that says so', () => {
    const halfView = TOUR.findIndex((s) => s.key === 'half-view');
    const last = marksFor(halfView).length - 1;
    expect(pageForMark(halfView, 0)).toBeUndefined();
    expect(pageForMark(halfView, last)).toBe('/account');
    const pin = TOUR.findIndex((s) => s.key === 'pin-patient');
    expect(pageForMark(pin, 0)).toBe(TOUR[pin].path);
    expect(pageForMark(pin, 1)).toBeUndefined();
  });
});

describe('moving through the tour', () => {
  it('goes through a step’s highlights, then on to the next step, and ends after the last', () => {
    expect(nextPosition({step: 0, mark: 0})).toEqual({step: 0, mark: 1});
    expect(nextPosition({step: 0, mark: marksFor(0).length - 1})).toEqual({step: 1, mark: 0});
    const lastStep = TOUR.length - 1;
    expect(nextPosition({step: lastStep, mark: marksFor(lastStep).length - 1})).toBeNull();
  });

  it('goes back to the previous step’s last highlight, and not before the first', () => {
    expect(previousPosition({step: 1, mark: 0})).toEqual({step: 0, mark: marksFor(0).length - 1});
    expect(previousPosition({step: 0, mark: 0})).toBeNull();
  });
});

describe('placeCard', () => {
  const view = {width: 1000, height: 800};
  const card = {width: 300, height: 200};
  const rect = (left: number, top: number, width: number, height: number) =>
    ({left, top, width, height, right: left + width, bottom: top + height}) as DOMRect;

  it('puts the card to the right of the target when it fits', () => {
    expect(placeCard(rect(100, 300, 100, 40), card, view)).toEqual({left: 216, top: 220});
  });

  it('puts it below when the right is full', () => {
    expect(placeCard(rect(700, 100, 280, 40), card, view)).toMatchObject({top: 156});
  });

  it('puts it in the bottom corner with nothing to point at', () => {
    expect(placeCard(null, card, view)).toEqual({left: 676, top: 576});
  });
});
