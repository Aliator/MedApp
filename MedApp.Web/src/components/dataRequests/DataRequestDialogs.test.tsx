import {waitFor, within} from '@testing-library/react';
import {adminUser, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT, readyDataRequest, testDataRequest} from '../../test/dataRequests';
import {ExtendDialog, extensionChoices, IdentityCheckDialog, RecordSentDialog, RefuseDialog, WithdrawDialog} from './DataRequestDialogs';

const BASE = `/api/data-requests/${DATA_REQUEST_ID}`;
const body = (fetch: ReturnType<typeof mockApi>, method = 'POST') =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method) as Request).json();

/** The open dialog, once it's open. */
const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function setUp(dialog: React.ReactElement, handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi(handlers);
  return {...renderWithApp(dialog, {user: adminUser}), fetch};
}

describe('data request dialogs', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('records the identity check: when, how and, for Other, how in words, from the version shown', async () => {
    const onDone = vi.fn();
    const {user, fetch} = setUp(<IdentityCheckDialog request={testDataRequest()} patient={DATA_REQUEST_PATIENT} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/identity-check`]: () => json(readyDataRequest()),
    });

    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('2 November 2026')).toBeInTheDocument();
    const confirm = dialog.getByRole('button', {name: 'Record Identity Check'});
    expect(isDisabled(confirm)).toBe(true);
    await user.click(dialog.getByRole('radio', {name: 'Other'}));
    expect(isDisabled(confirm)).toBe(true);
    await user.clear(dialog.getByRole('combobox', {name: /Checked on/}));
    await user.type(dialog.getByRole('combobox', {name: /Checked on/}), '03/11/2026');
    await user.tab();
    await user.type(dialog.getByRole('textbox', {name: /Detail/}), 'Video call with photo ID');
    await user.click(confirm);

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toMatchObject({checkedOn: '2026-11-03', method: 'Other', detail: 'Video call with photo ID', expectedVersion: 3});
  });

  it("shows the API's 409 in the dialog, which stays open", async () => {
    const {user} = setUp(<WithdrawDialog request={testDataRequest()} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/withdraw`]: () => json({title: 'Someone else changed this data request. Reload it, then try again.'}, 409),
      [`GET ${BASE}`]: () => json(testDataRequest()),
    });

    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('button', {name: 'Withdraw Request'}));
    expect(await dialog.findByText('Someone else changed this data request. Reload it, then try again.')).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
  });

  it('offers only the extensions left, and sends the months and reason', async () => {
    expect(extensionChoices({extensionMonths: 0})).toEqual([1, 2]);
    expect(extensionChoices({extensionMonths: 1})).toEqual([2]);
    expect(extensionChoices({extensionMonths: 2})).toEqual([]);

    const onDone = vi.fn();
    const {user, fetch} = setUp(<ExtendDialog request={testDataRequest()} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/extension`]: () => json(testDataRequest({extensionMonths: 2})),
    });
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('radio', {name: 'Two more months'}));
    await user.type(dialog.getByRole('textbox', {name: /Reason given to the requester/}), 'Several requests at once');
    await user.click(dialog.getByRole('button', {name: 'Extend'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({months: 2, reason: 'Several requests at once', expectedVersion: 3});
  });

  it('records it sent, showing a 400 on its field', async () => {
    const {user} = setUp(<RecordSentDialog request={readyDataRequest()} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/sent`]: () => json({title: 'Invalid', status: 400, errors: {SentOn: ["It can't be sent before it was received."]}}, 400),
    });
    const dialog = within(await dialogOpen());
    expect(isDisabled(dialog.getByRole('button', {name: 'Record as Sent'}))).toBe(true);
    await user.click(dialog.getByRole('radio', {name: 'Secure email'}));
    await user.clear(dialog.getByRole('combobox', {name: /Sent on/}));
    await user.type(dialog.getByRole('combobox', {name: /Sent on/}), '01/11/2026');
    await user.tab();
    await user.click(dialog.getByRole('button', {name: 'Record as Sent'}));
    expect(await dialog.findByText("It can't be sent before it was received.")).toBeInTheDocument();
  });

  it('refuses with the reason the requester is given', async () => {
    const onDone = vi.fn();
    const {user, fetch} = setUp(<RefuseDialog request={testDataRequest()} onCancel={vi.fn()} onDone={onDone} />, {
      [`POST ${BASE}/refuse`]: () => json(testDataRequest({status: 'Refused', canManage: false})),
    });
    const dialog = within(await dialogOpen());
    expect(isDisabled(dialog.getByRole('button', {name: 'Refuse Request'}))).toBe(true);
    await user.type(dialog.getByRole('textbox', {name: /Reason given to the requester/}), 'Manifestly excessive');
    await user.click(dialog.getByRole('button', {name: 'Refuse Request'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(await body(fetch)).toEqual({reason: 'Manifestly excessive', expectedVersion: 3});
  });

  it('starts an extension with the reason already given, and shows a failure in its own words', async () => {
    const {user} = setUp(<ExtendDialog request={testDataRequest({extensionMonths: 1, extensionReason: 'Complex request'})} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/extension`]: () => json({}, 500),
    });
    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('textbox', {name: /Reason given to the requester/})).toHaveValue('Complex request');
    expect(dialog.queryByRole('radio', {name: 'One more month'})).not.toBeInTheDocument();
    await user.click(dialog.getByRole('radio', {name: 'Two more months'}));
    await user.click(dialog.getByRole('button', {name: 'Extend'}));
    expect(await dialog.findByText('The due date could not be extended. Please try again.')).toBeInTheDocument();
  });

  it('shows a 400 about no field of its own above the fields', async () => {
    const {user} = setUp(<IdentityCheckDialog request={testDataRequest()} onCancel={vi.fn()} onDone={vi.fn()} />, {
      [`POST ${BASE}/identity-check`]: () => json({title: 'Invalid', status: 400, errors: {ExpectedVersion: ['Expected version is required.']}}, 400),
    });
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('radio', {name: 'Documents'}));
    await user.click(dialog.getByRole('button', {name: 'Record Identity Check'}));
    expect(await dialog.findByText('Expected version is required.')).toBeInTheDocument();
  });

  it.each([
    ['identity check', IdentityCheckDialog],
    ['extension', ExtendDialog],
    ['sent', RecordSentDialog],
    ['refusal', RefuseDialog],
    ['withdrawal', WithdrawDialog],
  ])('closes the %s dialog on Cancel, sending nothing', async (_, Dialog) => {
    const onCancel = vi.fn();
    const {user, fetch} = setUp(<Dialog request={testDataRequest()} onCancel={onCancel} onDone={vi.fn()} />);
    const dialog = within(await dialogOpen());
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });
});
