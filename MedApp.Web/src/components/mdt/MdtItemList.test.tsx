import {screen, waitFor, within} from '@testing-library/react';
import {mdtItem, mdtMeeting} from '../../test/mdt';
import {calls, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {meetingLabel} from './MdtDialogs';
import {mdtMeetingWords, MdtItemList} from './MdtItemList';

describe('MdtItemList', () => {
  it('shows each item with the patient, the question, its pathway and where it stands', async () => {
    renderWithApp(<MdtItemList items={[mdtItem()]} meetings={[mdtMeeting()]} showsPatient linksPathway emptyText="None." />);

    expect(await screen.findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Skin lesion'})).toHaveAttribute('href', '/pathways/proc-1');
    expect(screen.getByText('Question: Synthetic question for the team.')).toBeInTheDocument();
    expect(screen.getByText('Listed')).toBeInTheDocument();
  });

  it('shows the outcome as written and who was present once discussed, with no actions', async () => {
    const discussed = mdtItem({
      status: 'Discussed',
      outcome: 'Synthetic outcome.',
      outcomeByUsername: 'k.ashford',
      outcomeAtUtc: '2099-12-03T13:30:00Z',
      present: ['k.ashford', 's.brightwell'],
      canWithdraw: false,
      canDefer: false,
    });
    renderWithApp(<MdtItemList items={[discussed]} meetings={[]} emptyText="None." />);

    expect(await screen.findByText('Outcome: Synthetic outcome.')).toBeInTheDocument();
    expect(screen.getByText(/Present: k.ashford, s.brightwell/)).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Withdraw'})).not.toBeInTheDocument();
  });

  it('says when there is nothing', async () => {
    renderWithApp(<MdtItemList items={[]} meetings={[]} emptyText="Not listed for an MDT." />);
    expect(await screen.findByText('Not listed for an MDT.')).toBeInTheDocument();
  });

  it('withdraws an item from the version shown', async () => {
    const item = mdtItem({version: 3});
    const fetch = mockApi({[`POST /api/mdt/items/${item.id}/withdraw`]: () => json({...item, status: 'Withdrawn'})});
    const {user} = renderWithApp(<MdtItemList items={[item]} meetings={[mdtMeeting()]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Withdraw'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.click(within(openDialog()!).getByRole('button', {name: 'Withdraw'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/mdt/items/${item.id}/withdraw`));
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toEqual({expectedVersion: 3});
  });

  it('records the outcome as written, with who was present, from the version shown', async () => {
    const item = mdtItem({canRecordOutcome: true, version: 2});
    const fetch = mockApi({[`POST /api/mdt/items/${item.id}/outcome`]: () => json({...item, status: 'Discussed'})});
    const {user} = renderWithApp(<MdtItemList items={[item]} meetings={[mdtMeeting()]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Record Outcome'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Outcome/}), 'Synthetic outcome.');
    await user.click(dialog.getByRole('checkbox', {name: 's.brightwell'}));
    await user.click(dialog.getByRole('button', {name: 'Record Outcome'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/mdt/items/${item.id}/outcome`));
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toEqual({outcome: 'Synthetic outcome.', presentUserIds: ['u-2'], expectedVersion: 2});
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('moves an item to a later MDT', async () => {
    const item = mdtItem();
    const later = mdtMeeting({id: '00000000-0000-4000-8000-0000000000a2', startUtc: '2099-12-10T13:00:00Z', endUtc: '2099-12-10T14:00:00Z'});
    const fetch = mockApi({[`POST /api/mdt/items/${item.id}/defer`]: () => json({...item, status: 'Deferred', meetingId: later.id})});
    const {user} = renderWithApp(<MdtItemList items={[item]} meetings={[mdtMeeting(), later]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Move'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(later)}));
    await user.click(dialog.getByRole('button', {name: 'Move'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/mdt/items/${item.id}/defer`));
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toEqual({meetingId: later.id, expectedVersion: 1});
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("shows the API's refusal in the dialog, and closes it on cancel", async () => {
    const item = mdtItem();
    mockApi({[`POST /api/mdt/items/${item.id}/withdraw`]: () => json({title: 'Synthetic refusal.', status: 409}, 409)});
    const {user} = renderWithApp(<MdtItemList items={[item]} meetings={[mdtMeeting()]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Withdraw'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.click(within(openDialog()!).getByRole('button', {name: 'Withdraw'}));
    expect(await within(openDialog()!).findByText('Synthetic refusal.')).toBeInTheDocument();

    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('offers to list again an item whose meeting was cancelled, and says who withdrew one', async () => {
    const relist = mdtItem({id: 'c2', meetingId: null, meetingStartUtc: null, meetingCancelledStartUtc: '2099-12-03T13:00:00Z', canWithdraw: false});
    const withdrawn = mdtItem({
      id: 'c3',
      status: 'Withdrawn',
      withdrawnByUsername: 'k.ashford',
      withdrawnAtUtc: '2099-12-01T09:00:00Z',
      canWithdraw: false,
      canDefer: false,
    });
    renderWithApp(<MdtItemList items={[relist, withdrawn]} meetings={[]} linksPathway emptyText="None." />);

    expect(await screen.findByRole('button', {name: 'List Again'})).toBeInTheDocument();
    expect(screen.getByText(/^Withdrawn by k.ashford, /)).toBeInTheDocument();
    expect(screen.getByText(/^Meeting cancelled \(.+\): list again$/)).toBeInTheDocument();
  });

  it('copes with an item missing its details, and shows no actions when read only', async () => {
    const bare = mdtItem({
      id: undefined,
      patient: undefined,
      pathwayTitle: null,
      question: null,
      listedByUsername: null,
      listedAtUtc: null,
      version: undefined,
      specialtyName: null,
      status: 'Discussed',
      outcome: null,
      outcomeByUsername: null,
      outcomeAtUtc: null,
      present: undefined,
      canRecordOutcome: true,
    });
    renderWithApp(<MdtItemList items={[bare]} meetings={[]} showsPatient linksPathway isReadOnly emptyText="None." />);

    expect(await screen.findByRole('link', {name: 'Pathway'})).toBeInTheDocument();
    expect(screen.getByText('Question:')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it("offers Record Outcome only when the item's flag says so", async () => {
    renderWithApp(<MdtItemList items={[mdtItem({canRecordOutcome: true})]} meetings={[mdtMeeting()]} emptyText="None." />);
    expect(await screen.findByRole('button', {name: 'Record Outcome'})).toBeInTheDocument();
  });
});

describe('mdtMeetingWords', () => {
  it('names the meeting, or says it was cancelled and to list it again', () => {
    expect(mdtMeetingWords(mdtItem())).toMatch(/^Dermatology MDT, /);
    expect(mdtMeetingWords(mdtItem({meetingId: null, meetingStartUtc: null, meetingCancelledStartUtc: '2099-12-03T13:00:00Z'}))).toMatch(
      /^Meeting cancelled \(.+\): list again$/,
    );
    expect(mdtMeetingWords(mdtItem({status: 'Discussed', meetingId: null, meetingStartUtc: null, meetingCancelledStartUtc: '2099-12-03T13:00:00Z'}))).toMatch(
      /^Meeting cancelled \(.+\)$/,
    );
    expect(mdtMeetingWords(mdtItem({meetingStartUtc: null, specialtyName: null}))).toBe(' MDT');
    expect(mdtMeetingWords(mdtItem({meetingStartUtc: null}))).toBe('Dermatology MDT');
  });
});
