import {screen, waitFor, within} from '@testing-library/react';
import type {Pathway} from '../../api/pathways';
import {testConsult} from '../../test/consults';
import {mdtPatient} from '../../test/mdt';
import {calls, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {PathwayConsults} from './PathwayConsults';

const pathway: Pathway = {id: 'proc-1', title: 'Skin lesion', status: 'InProgress', teamId: 'team-derm', patient: mdtPatient};
const TEAMS = [
  {id: 'team-derm', name: 'Dermatology', isArchived: false},
  {id: 'team-lab', name: 'Lab', isArchived: false},
  {id: 'team-old', name: 'Archived clinic', isArchived: true},
];

describe('PathwayConsults', () => {
  it("shows how many are open, and asks another team: never the pathway's own, none chosen", async () => {
    const fetch = mockApi({
      'GET /api/consults/pathways/proc-1': () =>
        json({canRequest: true, consults: [testConsult({status: 'Replied', reply: 'Synthetic reply.', canClose: true})]}),
      'GET /api/teams': () => json(TEAMS),
      'POST /api/consults': () => json(testConsult(), 201),
    });
    const {user} = renderWithApp(<PathwayConsults pathway={pathway} />, {user: staffUser});

    expect(await screen.findByText('1 open, 1 replied and waiting to be closed')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Ask Another Team'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('combobox', {name: /Team to ask/}));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['Lab']);
    await user.click(screen.getByRole('option', {name: 'Lab'}));
    await user.type(dialog.getByRole('textbox', {name: /Question/}), 'Punch or excision?');
    await user.click(dialog.getByRole('button', {name: 'Ask'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/consults'));
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.clone().json()).toEqual({pathwayId: 'proc-1', askedTeamId: 'team-lab', question: 'Punch or excision?'});
  });

  it('offers no asking when the API says the viewer may not', async () => {
    mockApi({'GET /api/consults/pathways/proc-1': () => json({canRequest: false, consults: []})});
    renderWithApp(<PathwayConsults pathway={pathway} />, {user: staffUser});
    expect(await screen.findByText('No consults asked.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Ask Another Team'})).not.toBeInTheDocument();
  });
});
