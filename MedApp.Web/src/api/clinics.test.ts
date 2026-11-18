import {slotsWords} from './clinics';

describe('slotsWords', () => {
  it('says how full a session is, or that it was cancelled', () => {
    expect(slotsWords({status: 'Planned', freeSlotCount: 4, slotCount: 9})).toBe('4 of 9 free');
    expect(slotsWords({status: 'Planned', freeSlotCount: 0, slotCount: 9})).toBe('Full');
    expect(slotsWords({status: 'Cancelled', freeSlotCount: 0, slotCount: 9})).toBe('Cancelled');
    expect(slotsWords({status: 'Planned'})).toBe('Full');
  });
});
