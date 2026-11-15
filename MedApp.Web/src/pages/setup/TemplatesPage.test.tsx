import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, isDisabled, json, mockApi, openDialog, pickFavourite, renderWithApp} from '../../test/render';
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

  it('shows all of them from the favourites, starting on page 1', async () => {
    const onSearchChange = vi.fn();
    mockApi({[`GET ${BASE}`]: () => json(templates)});
    const {user} = renderWithApp(<TemplatesPage search={{page: 2}} onSearchChange={onSearchChange} />, {user: adminUser});

    await pickFavourite(user, 'All');

    expect(onSearchChange).toHaveBeenCalledWith({name: undefined, status: 'any', page: undefined});
  });

  it('says when the templates could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The templates could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('invites creating the first template when there are none', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json([])});

    expect(await screen.findByRole('link', {name: 'Create Your First Template'})).toHaveAttribute('href', '/admin/templates/new');
  });

  it('offers the archived ones when the search matches no active template', async () => {
    const onSearchChange = vi.fn();
    mockApi({[`GET ${BASE}`]: () => json(templates)});
    const {user} = renderWithApp(<TemplatesPage search={{name: 'old'}} onSearchChange={onSearchChange} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));

    expect(onSearchChange).toHaveBeenCalledWith({name: 'old', page: undefined, status: 'any'});
  });

  it('pages through a long list, keeping the first page out of the URL', async () => {
    const many = Array.from({length: 25}, (_, i) => ({...templates[1], id: `m${i}`, name: `Synthetic template ${String(i).padStart(2, '0')}`}));
    const onSearchChange = vi.fn();
    mockApi({[`GET ${BASE}`]: () => json(many)});
    const {user} = renderWithApp(<TemplatesPage search={{page: 2}} onSearchChange={onSearchChange} />, {user: adminUser});

    await screen.findByText('Synthetic template 10');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 3});
  });
});
