import {coachPosition, endCoachTour, resetCoachForTests, setCoachPosition, startCoachTour, subscribeCoach} from './coachState';

describe('coachState', () => {
  beforeEach(() => resetCoachForTests());

  it('has no tour under way to begin with', () => {
    expect(coachPosition()).toBeNull();
  });

  it('starts at the first highlight of a step, keeps it in storage and tells listeners', () => {
    const listener = vi.fn();
    subscribeCoach(listener);
    startCoachTour(3);
    expect(coachPosition()).toEqual({step: 3, mark: 0});
    expect(JSON.parse(localStorage.getItem('medappLightDemo.coach')!)).toEqual({step: 3, mark: 0});
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it('reads where it had got to after a reload', () => {
    localStorage.setItem('medappLightDemo.coach', JSON.stringify({step: 2, mark: 1, opened: '2:1'}));
    expect(coachPosition()).toEqual({step: 2, mark: 1, opened: '2:1'});
  });

  it('ignores something unreadable in storage', () => {
    localStorage.setItem('medappLightDemo.coach', '{"step":"x"}');
    expect(coachPosition()).toBeNull();
  });

  it('ends, forgetting where it was', () => {
    setCoachPosition({step: 1, mark: 2});
    endCoachTour();
    expect(coachPosition()).toBeNull();
    expect(localStorage.getItem('medappLightDemo.coach')).toBeNull();
  });
});
