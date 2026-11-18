import {screen, waitFor, within} from '@testing-library/react';
import type {Pathway} from '../../api/pathways';
import {mdtItem, mdtMeeting, mdtPatient} from '../../test/mdt';
import {calls, json, managerUser, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {meetingLabel} from './MdtDialogs';
import {PathwayMdt} from './PathwayMdt';

const pathway: Pathway = {id: 'proc-1', title: 'Skin lesion', status: 'InProgress', patient: mdtPatient};

describe('PathwayMdt', () => {
  it("shows the pathway's MDT items, and lists it for an MDT with the question", async () => {
    const fetch = mockApi({
      'GET /api/mdt/pathways/proc-1/items': () => json([mdtItem()]),
      'GET /api/mdt/meetings': () => json([mdtMeeting()]),
      'POST /api/mdt/items': () => json(mdtItem(), 201),
    });
    const {user} = renderWithApp(<PathwayMdt pathway={pathway} />, {user: staffUser});

    expect(await screen.findByText('Question: Synthetic question for the team.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'List for MDT'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('combobox', {name: /MDT/}));
    await user.click(await screen.findByRole('option', {name: meetingLabel(mdtMeeting())}));
    await user.type(dialog.getByRole('textbox', {name: /Question for the team/}), 'Excise or watch?');
    await user.click(dialog.getByRole('button', {name: 'List for MDT'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/mdt/items'));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.clone().json()).toEqual({pathwayId: 'proc-1', meetingId: mdtMeeting().id, question: 'Excise or watch?'});
  });

  it('offers no listing without WorkPathways, or for a closed or read-only pathway', async () => {
    mockApi({'GET /api/mdt/pathways/proc-1/items': () => json([]), 'GET /api/mdt/meetings': () => json([])});
    renderWithApp(<PathwayMdt pathway={pathway} />, {user: managerUser});
    expect(await screen.findByText('Not listed for an MDT.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'List for MDT'})).not.toBeInTheDocument();
  });

  it('offers no listing for a discharged pathway', async () => {
    mockApi({'GET /api/mdt/pathways/proc-1/items': () => json([]), 'GET /api/mdt/meetings': () => json([])});
    renderWithApp(<PathwayMdt pathway={{...pathway, status: 'Completed'}} />, {user: staffUser});
    expect(await screen.findByText('Not listed for an MDT.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'List for MDT'})).not.toBeInTheDocument();
  });
});
