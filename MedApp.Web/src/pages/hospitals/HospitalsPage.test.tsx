import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, expectAnnounced, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {HOSPITAL_ID, HOSPITALS} from '../../test/hospitals';
import {hospitalDetailWords, HospitalsPage} from './HospitalsPage';

function renderPage(handlers: Record<string, (request: Request) => Response> = {}) {
  const fetch = mockApi({'GET /api/hospitals': () => json(HOSPITALS), ...handlers});
  return {...renderWithApp(<HospitalsPage />, {user: adminUser}), fetch};
}

const dialog = () => within(openDialog()!);

describe('HospitalsPage', () => {
  it('lists the hospitals with their details, active first, one that sent requests not deletable', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Hospitals', level: 1})).toBeInTheDocument();
    const list = within(await screen.findByRole('list', {name: 'Hospitals'}));
    expect(list.getByText('ODS ZZR01 · Reports by post · 10 days when no date is given')).toBeInTheDocument();
    expect(list.getByText('Archived')).toBeInTheDocument();
    expect(list.getByRole('button', {name: /Riverside General has sent requests/})).toBeInTheDocument();
    expect(list.getByRole('button', {name: 'Restore Old Infirmary'})).toBeInTheDocument();
    expect(screen.getByText('1 active hospital')).toBeInTheDocument();
  });

  it('edits a hospital, sending every field', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({[`PUT /api/hospitals/${HOSPITAL_ID}`]: () => json(HOSPITALS[0])});

    await user.click(await screen.findByRole('button', {name: 'Edit Riverside General'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const name = dialog().getByRole('textbox', {name: /Name/});
    expect(name).toHaveValue('Riverside General');
    await user.type(name, ' NHS');
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Hospital saved');
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await put.clone().json()).toEqual({
      name: 'Riverside General NHS',
      odsCode: 'ZZR01',
      contact: 'Requests desk',
      reportDeliveryMethod: 'Post',
      defaultDeadlineDays: 10,
    });
  });

  it('describes a hospital in a line', () => {
    expect(hospitalDetailWords({...HOSPITALS[1], defaultDeadlineDays: 1})).toBe('ODS ZZO02 · Reports by other · 1 day when no date is given');
  });
});
