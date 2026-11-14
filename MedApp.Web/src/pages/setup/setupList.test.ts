import {setupPage} from './setupList';

const items = Array.from({length: 12}, (_, i) => ({name: `Stage ${String.fromCharCode(76 - i)}`, isArchived: i % 4 === 0}));

describe('setupPage', () => {
  it('leaves out archived ones, sorts by name and pages by ten', () => {
    const {rows, total, page, archivedCount} = setupPage(items, {});

    expect(archivedCount).toBe(3);
    expect(total).toBe(9);
    expect(page).toBe(1);
    // L, H and D are archived.
    expect(rows.map((r) => r.name)).toEqual(['Stage A', 'Stage B', 'Stage C', 'Stage E', 'Stage F', 'Stage G', 'Stage I', 'Stage J', 'Stage K']);
  });

  it('shows archived ones when asked, and keeps the page in range', () => {
    expect(setupPage(items, {archived: true, page: 2}).rows.map((r) => r.name)).toEqual(['Stage K', 'Stage L']);
    expect(setupPage(items, {page: 5}).page).toBe(1);
  });
});
