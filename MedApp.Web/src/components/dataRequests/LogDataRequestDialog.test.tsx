import {waitFor, within} from '@testing-library/react';
import {adminUser, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT, DATA_REQUEST_PATIENT_ID, testDataRequest} from '../../test/dataRequests';
import {LogDataRequestDialog} from './LogDataRequestDialog';

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function render(handler: () => Response) {
  const fetch = mockApi({'POST /api/data-requests': handler, [`GET /api/data-requests/${DATA_REQUEST_ID}`]: () => json(testDataRequest())});
  const onClose = vi.fn();
  const result = renderWithApp(<LogDataRequestDialog isOpen patientId={DATA_REQUEST_PATIENT_ID} patient={DATA_REQUEST_PATIENT} onClose={onClose} />, {
    user: adminUser,
  });
  return {fetch, onClose, ...result};
}

describe('LogDataRequestDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("logs the patient's own request, received today, then opens it", async () => {
    const {user, fetch, onClose, router} = render(() => json(testDataRequest(), 201));
    const dialog = within(await dialogOpen());

    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    const confirm = dialog.getByRole('button', {name: 'Log Data Request'});
    expect(isDisabled(confirm)).toBe(true);
    await user.click(dialog.getByRole('radio', {name: 'The patient'}));
    expect(dialog.queryByRole('textbox', {name: /Their name/})).not.toBeInTheDocument();
    await user.click(confirm);

    await waitFor(() => expect(router.state.location.pathname).toBe(`/data-requests/${DATA_REQUEST_ID}`));
    expect(onClose).toHaveBeenCalled();
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toMatchObject({
      patientId: DATA_REQUEST_PATIENT_ID,
      requestedByPatient: true,
      requesterName: null,
      requesterRelationship: null,
    });
  });

  it('needs the name and relationship of someone asking on their behalf, and shows a 400 on its field', async () => {
    const {user} = render(() => json({title: 'Invalid', status: 400, errors: {ReceivedOn: ["It can't be in the future."]}}, 400));
    const dialog = within(await dialogOpen());

    await user.click(dialog.getByRole('radio', {name: 'Someone on their behalf'}));
    const confirm = dialog.getByRole('button', {name: 'Log Data Request'});
    await user.type(dialog.getByRole('textbox', {name: /Their name/}), 'Synthetic Requester');
    expect(isDisabled(confirm)).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Relationship or authority/}), 'Parent');
    await user.click(confirm);

    expect(await dialog.findByText("It can't be in the future.")).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
  });

  it('sends the day chosen, and shows a 400 about no field of its own above the fields', async () => {
    const {user, fetch} = render(() => json({status: 400, errors: {PatientId: ['That patient is anonymised.']}}, 400));
    const dialog = within(await dialogOpen());

    await user.clear(dialog.getByRole('combobox', {name: /Received on/}));
    await user.type(dialog.getByRole('combobox', {name: /Received on/}), '03/11/2026');
    await user.tab();
    await user.click(dialog.getByRole('radio', {name: 'The patient'}));
    await user.click(dialog.getByRole('button', {name: 'Log Data Request'}));

    expect(await dialog.findByText('That patient is anonymised.')).toBeInTheDocument();
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toMatchObject({receivedOn: '2026-11-03'});
  });

  it("shows any other refusal in the API's words, or its own", async () => {
    const {user} = render(() => json({}, 500));
    const dialog = within(await dialogOpen());

    await user.click(dialog.getByRole('radio', {name: 'The patient'}));
    await user.click(dialog.getByRole('button', {name: 'Log Data Request'}));

    expect(await dialog.findByText('The data request could not be logged. Please try again.')).toBeInTheDocument();
  });
});

