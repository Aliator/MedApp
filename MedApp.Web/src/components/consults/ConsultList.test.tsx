import {screen, waitFor, within} from '@testing-library/react';
import {testConsult} from '../../test/consults';
import {calls, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {ConsultList} from './ConsultList';

describe('ConsultList', () => {
  it('shows who asked whom, the question, its status, and the reply once given', async () => {
    const replied = testConsult({
      status: 'Replied',
      reply: 'Synthetic reply.',
      repliedByUsername: 'r.aldous',
      repliedAtUtc: '2026-11-18T12:00:00Z',
      canClose: true,
    });
    renderWithApp(<ConsultList consults={[replied]} emptyText="None." />);

    expect(await screen.findByText('From Dermatology to Lab')).toBeInTheDocument();
    expect(screen.getByText('Question: Synthetic question for the lab.')).toBeInTheDocument();
    expect(screen.getByText('Reply: Synthetic reply.')).toBeInTheDocument();
    expect(screen.getByText(/^Replied by r.aldous/)).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Close Consult'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Reply'})).not.toBeInTheDocument();
  });

  it("shows the patient and links the pathway in a team's incoming list", async () => {
    renderWithApp(<ConsultList consults={[testConsult()]} showsPathway emptyText="None." />);
    expect(await screen.findByRole('region', {name: 'Patient: Patient, Test'})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Skin lesion'})).toHaveAttribute('href', '/pathways/proc-1');
    expect(screen.getByText('Awaiting reply')).toBeInTheDocument();
  });

  it('says when there is nothing', async () => {
    renderWithApp(<ConsultList consults={[]} emptyText="No consults sent." />);
    expect(await screen.findByText('No consults sent.')).toBeInTheDocument();
  });

  it('replies from the version shown', async () => {
    const consult = testConsult({version: 2, canReply: true});
    const fetch = mockApi({[`POST /api/consults/${consult.id}/reply`]: () => json({...consult, status: 'Replied'})});
    const {user} = renderWithApp(<ConsultList consults={[consult]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Reply'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Reply/}), 'Synthetic reply.');
    await user.click(dialog.getByRole('button', {name: 'Reply'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/consults/${consult.id}/reply`));
    expect(await (fetch.mock.calls[0][0] as Request).clone().json()).toEqual({reply: 'Synthetic reply.', expectedVersion: 2});
  });

  it('withdraws an open consult from the version shown', async () => {
    const consult = testConsult({version: 3, canWithdraw: true});
    const fetch = mockApi({[`POST /api/consults/${consult.id}/withdraw`]: () => json({...consult, status: 'Withdrawn'})});
    const {user} = renderWithApp(<ConsultList consults={[consult]} emptyText="None." />);

    await user.click(await screen.findByRole('button', {name: 'Withdraw'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.click(within(openDialog()!).getByRole('button', {name: 'Withdraw'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST /api/consults/${consult.id}/withdraw`));
    expect(await (fetch.mock.calls[0][0] as Request).clone().json()).toEqual({expectedVersion: 3});
  });
});
