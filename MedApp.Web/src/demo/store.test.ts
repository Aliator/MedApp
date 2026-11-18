import {STAFF} from './accounts';
import {PATHWAYS} from './ids';
import {seed, SEED_VERSION} from './seed';
import {db, load, replaceState, reset} from './store';

describe('the light demo store', () => {
  it('seeds on the first visit, even without IndexedDB (in memory only)', async () => {
    const state = await load();
    expect(state.version).toBe(SEED_VERSION);
    expect(db()).toBe(state);
  });

  it('resets to the seed, keeping who is signed in', async () => {
    const state = replaceState(seed());
    state.user = STAFF.username;
    state.pathways = state.pathways.filter((p) => p.id !== PATHWAYS.onHold);
    await reset();
    expect(db().user).toBe(STAFF.username);
    expect(db().pathways.some((p) => p.id === PATHWAYS.onHold)).toBe(true);
  });
});
