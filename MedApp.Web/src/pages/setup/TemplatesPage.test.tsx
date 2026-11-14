import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, isDisabled, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {stagesSummary, TemplatesPage} from './TemplatesPage';

const BASE = '/api/procedures/patient-procedure-stage/templates';
const stage = (stageOrder: number, name: string) => ({stageOrder, stageDefinitionId: `d-${name}`, name});

const templates = [
  {id: 't1', name: 'Knee review', isArchived: false, procedureCount: 3, stages: [stage(2, 'Scan'), stage(1, 'Consultation')]},
  {id: 't2', name: 'Draft pathway', isArchived: false, procedureCount: 0, stages: [stage(1, 'Consent')]},
  {id: 't3', name: 'Old pathway', isArchived: true, procedureCount: 0, stages: []},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(templates), ...extra});
  return {fetch, ...renderWithApp(<TemplatesPage search={search} onSearchChange={vi.fn()} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;

describe('TemplatesPage', () => {
  it("sums up a template's stages in order", () => {
    expect(stagesSummary(templates[0])).toBe('2 stages: Consultation, Scan');
    expect(stagesSummary(templates[2])).toBe('No stages');
  });

  it('lists the active templates, with their stages and how many procedures use them', async () => {
    renderPage();

    const knee = await rowOf('Knee review');
    expect(within(knee).getByText('2 stages: Consultation, Scan')).toBeInTheDocument();
    expect(within(knee).getByText('3 procedures')).toBeInTheDocument();
    expect(within(knee).getByRole('link', {name: 'Edit template'})).toHaveAttribute('href', '/admin/templates/t1');
    expect(within(await rowOf('Draft pathway')).getByText('Not used yet')).toBeInTheDocument();
    expect(screen.queryByText('Old pathway')).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'New Template'})).toHaveAttribute('href', '/admin/templates/new');
  });

  it('archives after a confirmation, and restores an archived one', async () => {
    const {user, fetch} = renderPage(
      {status: 'any'},
      {[`POST ${BASE}/t1/archive`]: () => json({}), [`POST ${BASE}/t3/restore`]: () => json({})},
    );

    await user.click(within(await rowOf('Knee review')).getByRole('button', {name: 'Archive template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));
    await user.click(within(await rowOf('Old pathway')).getByRole('button', {name: 'Restore template'}));

    await waitFor(() => expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/t1/archive`, `POST ${BASE}/t3/restore`])));
  });

  it('deletes only templates never used, and explains a refusal', async () => {
    const {user, fetch} = renderPage(
      {},
      {[`DELETE ${BASE}/t2`]: () => json({title: 'This template has been used, so it can only be archived.'}, 409)},
    );

    expect(isDisabled(within(await rowOf('Knee review')).getByRole('button', {name: /can be archived but not deleted/}))).toBe(true);
    await user.click(within(await rowOf('Draft pathway')).getByRole('button', {name: 'Delete template'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Template'}));

    expect(await screen.findByText('This template has been used, so it can only be archived.')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`DELETE ${BASE}/t2`);
  });
});
