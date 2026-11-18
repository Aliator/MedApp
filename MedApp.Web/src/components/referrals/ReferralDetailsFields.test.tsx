import {screen, waitFor} from '@testing-library/react';
import {useState} from 'react';
import type {ReferralDetailsInput} from '../../api/referrals';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {DERMATOLOGY_ID, TEAMS} from '../../test/referrals';
import {aYearBefore, emptyReferralDetails, ReferralDetailsFields, ukToday} from './ReferralDetailsFields';

function Harness({
  onChange,
  currentTeam,
  practiceId = '',
}: {
  onChange: (value: ReferralDetailsInput) => void;
  currentTeam?: {id: string; name: string};
  practiceId?: string;
}) {
  const [value, setValue] = useState(() => ({...emptyReferralDetails('2026-11-18'), practiceId}));
  return (
    <ReferralDetailsFields
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
      currentTeam={currentTeam}
      errors={{title: 'Invalid', fields: {reason: ['Reason is required.']}, general: []}}
    />
  );
}

describe('ReferralDetailsFields', () => {
  it('works out today (UK) and a year back', () => {
    // 23:30 UTC in summer is the next day in the UK.
    expect(ukToday(new Date('2026-07-01T23:30:00Z'))).toBe('2026-07-02');
    expect(aYearBefore('2026-11-18')).toBe('2025-11-18');
    expect(aYearBefore('2028-02-29')).toBe('2027-03-01');
    expect(emptyReferralDetails('2026-11-18')).toMatchObject({receivedOn: '2026-11-18', requestedPriority: 'Medium'});
  });

  it('offers the active teams, then the fields from the letter, with the API\'s errors on them', async () => {
    mockApi({'GET /api/teams': () => json(TEAMS)});
    const onChange = vi.fn();
    const {user} = renderWithApp(<Harness onChange={onChange} />, {user: staffUser});

    await user.click(await screen.findByRole('combobox', {name: /Clinic/}));
    await user.click(await screen.findByRole('option', {name: 'Dermatology'}));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({teamId: DERMATOLOGY_ID}));

    await user.type(screen.getByRole('textbox', {name: /Practice/}), 'Synthetic Surgery');
    await user.type(screen.getByRole('textbox', {name: /Clinician/}), 'Dr Test');
    await user.type(screen.getByRole('textbox', {name: /ODS code/}), 'a12345');
    await user.type(screen.getByRole('textbox', {name: /Reason/}), 'Rash');
    await user.click(screen.getByRole('combobox', {name: /Priority asked for/}));
    await user.click(await screen.findByRole('option', {name: 'Urgent'}));
    expect(onChange).toHaveBeenLastCalledWith({
      teamId: DERMATOLOGY_ID,
      receivedOn: '2026-11-18',
      practiceId: '',
      referrerPractice: 'Synthetic Surgery',
      referrerClinician: 'Dr Test',
      referrerOdsCode: 'a12345',
      requestedPriority: 'Urgent',
      reason: 'Rash',
    });
    expect(screen.getAllByText('Reason is required.').length).toBeGreaterThan(0);

    await user.clear(screen.getByRole('combobox', {name: /Received on/}));
    await user.type(screen.getByRole('combobox', {name: /Received on/}), '03/11/2026');
    await user.tab();
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({receivedOn: '2026-11-03'}));
  });

  it('keeps offering an archived team a referral is already to', async () => {
    mockApi({'GET /api/teams': () => json(TEAMS)});
    const {user} = renderWithApp(<Harness onChange={vi.fn()} currentTeam={{id: 'old', name: 'Old clinic'}} />, {user: staffUser});

    await user.click(await screen.findByRole('combobox', {name: /Clinic/}));
    expect(await screen.findByRole('option', {name: 'Old clinic (archived)'})).toBeInTheDocument();
  });
});

describe('ReferralDetailsFields: the practice from the directory', () => {
  const PRACTICES = [
    {id: 'p-test', name: 'Testbrook Surgery', odsCode: 'ZZZ123', isArchived: false},
    {id: 'p-old', name: 'Oldfield Practice', odsCode: null, isArchived: true},
    {id: 'p-a', name: 'Avonmere Health Centre', odsCode: null, isArchived: false},
  ];

  it('offers the practices not archived, by name, and fills the practice and ODS code from the one chosen', async () => {
    const fetch = mockApi({'GET /api/teams': () => json(TEAMS), 'GET /api/practices': () => json(PRACTICES)});
    const onChange = vi.fn();
    const {user} = renderWithApp(<Harness onChange={onChange} />, {user: staffUser});

    await user.click(await screen.findByRole('combobox', {name: /Practice from the directory/}));
    const options = await screen.findAllByRole('option');
    expect(options.map((o) => o.textContent)).toEqual(['None (as typed below)', 'Avonmere Health Centre', 'Testbrook Surgery (ZZZ123)']);
    await user.click(screen.getByRole('option', {name: 'Testbrook Surgery (ZZZ123)'}));

    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({practiceId: 'p-test', referrerPractice: 'Testbrook Surgery', referrerOdsCode: 'ZZZ123'}));
    expect(screen.getByRole('textbox', {name: /^Practice/})).toHaveValue('Testbrook Surgery');
    expect(String((fetch.mock.calls[0][0] as Request).url) + String((fetch.mock.calls[1][0] as Request).url)).toContain('includeArchived=true');

    // Still editable; None takes the directory's practice away and leaves the words typed.
    await user.type(screen.getByRole('textbox', {name: /^Practice/}), ' (branch)');
    await user.click(screen.getByRole('combobox', {name: /Practice from the directory/}));
    await user.click(await screen.findByRole('option', {name: 'None (as typed below)'}));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({practiceId: '', referrerPractice: 'Testbrook Surgery (branch)', referrerOdsCode: 'ZZZ123'}));
  });

  it('keeps offering an archived practice a referral already has, marked archived', async () => {
    mockApi({'GET /api/teams': () => json(TEAMS), 'GET /api/practices': () => json(PRACTICES)});
    const {user} = renderWithApp(<Harness onChange={vi.fn()} practiceId="p-old" />, {user: staffUser});

    const picker = await screen.findByRole('combobox', {name: /Practice from the directory/});
    await waitFor(() => expect(picker).toHaveTextContent('Oldfield Practice (archived)'));
    await user.click(picker);
    expect(await screen.findByRole('option', {name: 'Oldfield Practice (archived)'})).toBeInTheDocument();
  });
});
