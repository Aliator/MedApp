import {screen, within} from '@testing-library/react';
import {expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles, withAnnouncements} from '../../test/render';
import {CASE_ID, testHospitalCase} from '../../test/labCases';
import {SurgeryDetailsDialog} from './SurgeryDetailsDialog';

const labUser = userWithRoles('test-lab', 'Lab');
const dialog = () => within(openDialog()!);
const bodyOf = (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls[0][0] as Request).clone().json();

function renderDialog(answer: () => Response, labCase = testHospitalCase()) {
  const onClose = vi.fn();
  const fetch = mockApi({[`POST /api/lab-cases/${CASE_ID}/surgery-details`]: answer});
  const result = renderWithApp(<SurgeryDetailsDialog labCase={labCase} onClose={onClose} />, {user: labUser});
  return {...result, fetch, onClose};
}

describe('SurgeryDetailsDialog', () => {
  it('adds the hospital’s details as written, with the case and hospital shown, and closes a case awaiting them', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog(() => json(testHospitalCase({status: 'Closed'})));

    expect(await screen.findByRole('heading', {name: 'Add Surgery Details'})).toBeInTheDocument();
    expect(dialog().getByText('MA26-000300')).toBeInTheDocument();
    expect(dialog().getByText('Riverside General (test)')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add and Close Case'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Procedure/}), 'Excision (synthetic)');
    await user.click(dialog().getByRole('button', {name: 'Add and Close Case'}));

    await expectAnnounced('Surgery details added; the case is closed');
    expect(await bodyOf(fetch)).toEqual({surgeryOn: null, procedure: 'Excision (synthetic)', outcome: null, noSurgeryExpected: false, detail: null, expectedVersion: 3});
    expect(onClose).toHaveBeenCalled();
  });

  it('asks why when no surgery is expected', async () => {
    const {user, fetch} = renderDialog(() => json(testHospitalCase({status: 'Closed'})));
    await screen.findByRole('heading', {name: 'Add Surgery Details'});
    await user.click(dialog().getByRole('switch', {name: 'No surgery expected'}));
    expect(dialog().queryByRole('textbox', {name: /Procedure/})).not.toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add and Close Case'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Why no surgery is expected/}), 'Treated another way (synthetic).');
    await user.click(dialog().getByRole('button', {name: 'Add and Close Case'}));
    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await bodyOf(fetch)).toMatchObject({noSurgeryExpected: true, detail: 'Treated another way (synthetic).', procedure: null});
  });

  it('keeps details arriving before the report without closing, and shows a refusal in the API’s words', async () => {
    const {user} = renderDialog(() => json({title: 'Someone else changed this lab case.'}, 409), testHospitalCase({status: 'Open'}));
    await screen.findByRole('heading', {name: 'Add Surgery Details'});
    expect(dialog().queryByRole('switch', {name: 'No surgery expected'})).not.toBeInTheDocument();
    await user.type(dialog().getByRole('textbox', {name: /Other details/}), 'Extra details (synthetic).');
    await user.click(dialog().getByRole('button', {name: 'Add Details'}));
    expect(await dialog().findByText(/Someone else changed this lab case/)).toBeInTheDocument();
  });
});
