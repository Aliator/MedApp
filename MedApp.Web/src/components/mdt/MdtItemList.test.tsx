import {screen, waitFor, within} from '@testing-library/react';
import {mdtItem, mdtMeeting} from '../../test/mdt';
import {calls, json, mockApi, openDialog, renderWithApp} from '../../test/render';
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
  });
});
