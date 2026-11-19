import {QueryClient} from '@tanstack/react-query';
import {screen} from '@testing-library/react';
import {whoamiQuery, type CurrentUser} from '../../auth/auth';
import {adminUser, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {clinicFor, pickedClinic, useClinicPicker, type ClinicPickerPage} from './ClinicPicker';

const DERMATOLOGY = '000000d1-0000-4000-8000-000000000001';
const GYNAECOLOGY = '000000d1-0000-4000-8000-000000000002';
const LAB = '000000d1-0000-4000-8000-000000000006';

const team = (id: string, name: string) => ({id, name, kind: 'Clinic' as const, role: 'Member' as const});
const oneTeam: CurrentUser = {...staffUser, teams: [team(DERMATOLOGY, 'Dermatology')]};
const twoTeams: CurrentUser = {...staffUser, teams: [team(GYNAECOLOGY, 'Gynaecology'), team(DERMATOLOGY, 'Dermatology')]};

function Picker({page = 'patients', clinic, onChange}: {page?: ClinicPickerPage; clinic?: string; onChange: (clinic: string | undefined) => void}) {
  const picker = useClinicPicker(page, clinic, onChange);
  return (
    <>
      {picker.picker}
      <output data-testid="picked">{picker.teamId ?? 'all'}</output>
    </>
  );
}

function renderPicker(user: CurrentUser, clinic?: string) {
  mockApi({
    'GET /api/teams': () =>
      json([
        {id: DERMATOLOGY, name: 'Dermatology', isArchived: false},
        {id: LAB, name: 'Laboratory', isArchived: false},
        {id: GYNAECOLOGY, name: 'Gynaecology', isArchived: true},
      ]),
  });
  const onChange = vi.fn();
  return {onChange, ...renderWithApp(<Picker clinic={clinic} onChange={onChange} />, {user})};
}

describe('pickedClinic', () => {
  it('is the URL’s team, else the remembered one, else every team', () => {
    expect(pickedClinic(adminUser, DERMATOLOGY, LAB)).toBe(DERMATOLOGY);
    expect(pickedClinic(adminUser, undefined, LAB)).toBe(LAB);
    expect(pickedClinic(adminUser, 'all', LAB)).toBeUndefined();
    expect(pickedClinic(adminUser, undefined, null)).toBeUndefined();
    expect(pickedClinic(adminUser, undefined, 'not an id')).toBeUndefined();
  });

  it('gives staff only their own teams, and nobody in one team any', () => {
    expect(pickedClinic(twoTeams, GYNAECOLOGY, null)).toBe(GYNAECOLOGY);
    expect(pickedClinic(twoTeams, LAB, null)).toBeUndefined();
    expect(pickedClinic(oneTeam, DERMATOLOGY, DERMATOLOGY)).toBeUndefined();
  });
});

describe('clinicFor', () => {
  it('reads the person and the remembered choice, for a loader', () => {
    const queryClient = new QueryClient();
    expect(clinicFor(queryClient, 'letters', DERMATOLOGY)).toBeUndefined();
    queryClient.setQueryData(whoamiQuery.queryKey, adminUser);
    localStorage.setItem('medapp.clinicPicker.letters', LAB);
    expect(clinicFor(queryClient, 'letters', undefined)).toBe(LAB);
    expect(clinicFor(queryClient, 'letters', DERMATOLOGY)).toBe(DERMATOLOGY);
  });
});

describe('ClinicPicker', () => {
  it('offers an admin every team, archived ones too, and remembers the one chosen', async () => {
    const {user, onChange} = renderPicker(adminUser);

    await user.click(await screen.findByRole('combobox', {name: /^Team/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All teams', 'Dermatology', 'Laboratory', 'Gynaecology (archived)']);
    await user.click(screen.getByRole('option', {name: 'Laboratory'}));

    expect(onChange).toHaveBeenLastCalledWith(LAB);
    expect(localStorage.getItem('medapp.clinicPicker.patients')).toBe(LAB);
    expect(await screen.findByTestId('picked')).toHaveTextContent(LAB);
  });

  it('forgets the choice for All teams', async () => {
    localStorage.setItem('medapp.clinicPicker.patients', LAB);
    const {user, onChange} = renderPicker(adminUser);

    expect(await screen.findByTestId('picked')).toHaveTextContent(LAB);
    await user.click(await screen.findByRole('combobox', {name: /^Team/}));
    await user.click(screen.getByRole('option', {name: 'All teams'}));

    expect(onChange).toHaveBeenLastCalledWith(undefined);
    expect(localStorage.getItem('medapp.clinicPicker.patients')).toBeNull();
  });

  it('offers staff in more than one team only their own, by name', async () => {
    const {user} = renderPicker(twoTeams);

    await user.click(await screen.findByRole('combobox', {name: /^Team/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All teams', 'Dermatology', 'Gynaecology']);
  });

  it('isn’t shown to staff in one team', async () => {
    renderPicker(oneTeam, DERMATOLOGY);

    expect(screen.queryByRole('combobox', {name: /^Team/})).not.toBeInTheDocument();
    expect(await screen.findByTestId('picked')).toHaveTextContent('all');
  });
});
