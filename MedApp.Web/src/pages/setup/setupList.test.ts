import {fromSetupBar, setupBarValues, setupPage, setupSubtitle} from './setupList';

const items = Array.from({length: 12}, (_, i) => ({name: `Stage ${String.fromCharCode(76 - i)}`, isArchived: i % 4 === 0}));

describe('setupPage', () => {
  it('shows the active ones to begin with, sorted by name and paged by ten', () => {
    const {rows, total, page} = setupPage(items, {});

    expect(total).toBe(9);
    expect(page).toBe(1);
    // L, H and D are archived.
    expect(rows.map((r) => r.name)).toEqual(['Stage A', 'Stage B', 'Stage C', 'Stage E', 'Stage F', 'Stage G', 'Stage I', 'Stage J', 'Stage K']);
  });

  it('shows only the archived ones, or all of them, when asked, and keeps the page in range', () => {
    expect(setupPage(items, {status: 'archived'}).rows.map((r) => r.name)).toEqual(['Stage D', 'Stage H', 'Stage L']);
    expect(setupPage(items, {status: 'any', page: 2}).rows.map((r) => r.name)).toEqual(['Stage K', 'Stage L']);
    expect(setupPage(items, {page: 5}).page).toBe(1);
  });

  it('filters by name, ignoring case', () => {
    expect(setupPage(items, {name: 'stage k', status: 'any'}).rows.map((r) => r.name)).toEqual(['Stage K']);
  });
});

describe('the setup lists search', () => {
  it('shows Active as a filter when the URL has no status, and nothing for both', () => {
    expect(setupBarValues({})).toEqual({name: undefined, status: 'active'});
    expect(setupBarValues({status: 'any', name: 'scope'})).toEqual({name: 'scope', status: undefined});
  });

  it('keeps Active out of the URL (the default), and a removed status as both', () => {
    expect(fromSetupBar({status: 'active'})).toEqual({name: undefined, status: undefined});
    expect(fromSetupBar({status: 'archived', name: 'scope'})).toEqual({name: 'scope', status: 'archived'});
    expect(fromSetupBar({})).toEqual({name: undefined, status: 'any'});
  });

  it('says what the list shows', () => {
    expect(setupSubtitle(3, {}, 'stage', 'stages')).toBe('3 active stages');
    expect(setupSubtitle(1, {status: 'archived'}, 'stage', 'stages')).toBe('1 archived stage');
    expect(setupSubtitle(4, {status: 'any'}, 'stage', 'stages')).toBe('4 stages');
    expect(setupSubtitle(2, {name: 'con'}, 'stage', 'stages')).toBe('2 matching stages');
  });
});
