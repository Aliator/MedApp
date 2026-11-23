import {FULL_DEMO_VERSION} from '../test/fullDemo';
import {accountDescription, accountLabel, orderAccounts} from './accounts';

const byName = (username: string) => FULL_DEMO_VERSION.accounts.find((a) => a.username === username)!;

describe('full demo accounts', () => {
  it('puts the four demo accounts first, in the walkthrough’s order, then everyone else', () => {
    const {demo, others} = orderAccounts(FULL_DEMO_VERSION.accounts);
    expect(demo.map((a) => a.username)).toEqual(['admin', 'n.thornbury', 'r.aldous', 's.varma']);
    expect(others.map((a) => a.username)).toEqual(['j.whitlock']);
  });

  it('describes each in a line, as the light demo does', () => {
    expect(accountDescription(byName('admin'))).toBe('Admin, read only. Centre administrator.');
    expect(accountDescription(byName('n.thornbury'))).toBe('Clinic admin (Dermatology). Clinician. Consultant dermatologist.');
    expect(accountDescription(byName('r.aldous'))).toBe('Staff (Lab). Lab and Pathologist. Consultant histopathologist.');
    expect(accountDescription(byName('j.whitlock'))).toBe('Staff (Dermatology and Eye clinic). Manager. Service manager.');
    expect(accountDescription({...byName('j.whitlock'), teams: []})).toBe('Staff. Manager. Service manager.');
  });

  it('names the admin as such, and everyone else by name', () => {
    expect(accountLabel(byName('admin'))).toBe('the admin');
    expect(accountLabel(byName('s.varma'))).toBe('Saira Varma');
  });
});
