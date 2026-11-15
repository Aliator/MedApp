import {acronym, initials, procedureNavLabel} from './navLabels';

describe('navLabels', () => {
  it('takes initials from a name', () => {
    expect(initials('Freya Ahmed')).toBe('FA');
    expect(initials("Siobhan O'Brien")).toBe('SO');
    expect(initials('  freya   ahmed ')).toBe('FA');
  });

  it('takes an acronym from a title, splitting on spaces, hyphens and slashes', () => {
    expect(acronym('Total knee replacement')).toBe('TKR');
    expect(acronym('Ultrasound-guided biopsy')).toBe('UGB');
    expect(acronym('Upper GI endoscopy')).toBe('UGE');
    expect(acronym('Bronchoscopy')).toBe('B');
  });

  it('puts the initials before the acronym', () => {
    expect(procedureNavLabel('Freya Ahmed', 'Total knee replacement')).toBe('FA TKR');
  });

  it('falls back when there is nothing to abbreviate', () => {
    expect(procedureNavLabel('', '')).toBe('Procedure');
  });
});
