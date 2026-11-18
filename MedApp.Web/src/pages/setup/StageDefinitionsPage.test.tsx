import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, findAlert, isDisabled, json, mockApi, openDialog, pickFavourite, renderWithApp, searchBar, expectAnnounced, withAnnouncements} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {StageDefinitionsPage} from './StageDefinitionsPage';
import {richText, typeRichText} from '../../test/richText';

const BASE = '/api/pathways/patient-pathway-stage/definitions';

const definitions = [
  // Real SNOMED CT concept IDs, used only as codes: 71388002 is Procedure.
  {
    id: 'd1',
    name: 'Consent',
    description: 'Consent recorded.',
    instructions: 'Give the patient a copy.',
    visitRequirement: 'Required',
    needsLetterKind: 'DischargeSummary',
    waitsForResults: true,
    snomedCode: '71388002',
    snomedTerm: 'Procedure',
    isArchived: false,
    templateCount: 2,
    canBeDeleted: false,
  },
  {id: 'd2', name: 'Biopsy', description: 'Sample taken.', instructions: '', isArchived: false, templateCount: 0, canBeDeleted: true},
  {id: 'd3', name: 'Old triage', description: '', instructions: '', isArchived: true, templateCount: 1, canBeDeleted: false},
];

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[`GET ${BASE}`]: () => json(definitions), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<StageDefinitionsPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method)!;

describe('StageDefinitionsPage', () => {
  it('lists the active definitions by name, with what uses them; archived ones from the search', async () => {
    const {user, onSearchChange} = renderPage();

    await screen.findByText('Consent');
    const rows = screen.getAllByRole('row');
    // Consent involves a visit, and says so beside its name.
    expect(rows.slice(1).map((r) => within(r).getAllByRole('cell')[0].textContent)).toEqual([
      'BiopsySample taken.',
      'ConsentVisit requiredConsent recorded.',
    ]);
    expect(within(await rowOf('Consent')).getByText('2 templates')).toBeInTheDocument();
    expect(within(await rowOf('Biopsy')).getByText('No templates')).toBeInTheDocument();
    expect(screen.queryByText('Old triage')).not.toBeInTheDocument();
    expect(screen.getByText('2 active stages')).toBeInTheDocument();

    // The search starts on the active ones; the archived ones are a favourite away.
    expect(searchBar()).toHaveTextContent(/Status: is\s*Active/);
    // The bar's token says it, so the favourite's name isn't repeated beside the star (which still says it).
    expect(screen.getByRole('button', {name: 'Favourites (showing Active)'})).toBeInTheDocument();
    // (Each active row says Active in its status column too.)
    const outsideBarMenuAndTable = (el: HTMLElement) => !searchBar().contains(el) && !el.closest('[role="menu"]') && !el.closest('table');
    expect(screen.queryAllByText('Active').filter(outsideBarMenuAndTable)).toEqual([]);
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    await pickFavourite(user, 'Archived');
    expect(onSearchChange).toHaveBeenCalledWith({name: undefined, status: 'archived', page: undefined});
  });

  it('adds a definition', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`POST ${BASE}`]: () => json({id: 'd9'}, 201)});

    await user.click(await screen.findByRole('button', {name: 'Add Stage'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Scan');
    await user.type(dialog.getByRole('textbox', {name: /Description/}), 'Imaging done.');
    await typeRichText(dialog.getByRole('textbox', {name: /Instructions/}), 'Book the scanner.');
    // No visit unless chosen.
    expect(dialog.getByRole('radio', {name: /^None/})).toBeChecked();
    await user.click(dialog.getByRole('radio', {name: /^Suggested/}));
    // No letter unless chosen; then a kind of letter.
    const letter = dialog.getByRole('combobox', {name: /Needs a letter/});
    expect(letter).toHaveTextContent('None');
    await user.click(letter);
    await user.click(await screen.findByRole('option', {name: 'Clinic letter'}));
    expect(dialog.getByRole('switch', {name: /^Waits for results/})).not.toBeChecked();
    await user.click(dialog.getByRole('switch', {name: /^Waits for results/}));
    await user.click(dialog.getByRole('button', {name: 'Add Stage'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'POST').json()).toEqual({
      name: 'Scan',
      description: 'Imaging done.',
      instructions: 'Book the scanner.',
      snomedCode: '',
      snomedTerm: '',
      visitRequirement: 'Suggested',
      needsLetterKind: 'ClinicLetter',
      waitsForResults: true,
    });
    await expectAnnounced('Stage added');
  });

  it("edits one, starting from what it says, and shows the API's errors on the fields", async () => {
    const {user, fetch} = renderPage(
      {},
      {[`PATCH ${BASE}/d1`]: () => json({title: 'Validation failed', status: 400, errors: {Name: ['Name is already used.']}}, 400)},
    );

    await user.click(within(await rowOf('Consent')).getByRole('button', {name: 'Edit stage'}));
    const dialog = within(openDialog()!);
    expect(richText(dialog.getByRole('textbox', {name: /Instructions/}))).toBe('Give the patient a copy.');
    expect(dialog.getByRole('radio', {name: /^Required/})).toBeChecked();
    expect(dialog.getByRole('combobox', {name: /Needs a letter/})).toHaveTextContent('Discharge summary');
    expect(dialog.getByRole('switch', {name: /^Waits for results/})).toBeChecked();
    await user.type(dialog.getByRole('textbox', {name: /Name/}), '!');
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    expect((await dialog.findAllByText('Name is already used.')).length).toBeGreaterThan(0);
    expect(await sent(fetch, 'PATCH').json()).toEqual({
      name: 'Consent!',
      description: 'Consent recorded.',
      instructions: 'Give the patient a copy.',
      visitRequirement: 'Required',
      needsLetterKind: 'DischargeSummary',
      waitsForResults: true,
      snomedCode: '71388002',
      snomedTerm: 'Procedure',
    });
  });

  it("shows each definition's SNOMED CT concept, or a dash for none", async () => {
    renderPage();

    const consent = await rowOf('Consent');
    expect(within(consent).getByText('71388002')).toBeInTheDocument();
    expect(within(consent).getByText('Procedure')).toBeInTheDocument();
    expect(within(await rowOf('Biopsy')).getByText('—')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', {name: 'SNOMED CT'})).toBeInTheDocument();
  });

  it('adds a SNOMED CT concept to a definition, and removes one by clearing its code', async () => {
    const {user, fetch} = renderPage({}, {[`PATCH ${BASE}/d2`]: () => json({}), [`PATCH ${BASE}/d1`]: () => json({})});

    await user.click(within(await rowOf('Biopsy')).getByRole('button', {name: 'Edit stage'}));
    let dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /SNOMED CT Code/}), '80146002');
    await user.type(dialog.getByRole('textbox', {name: /Display Term/}), 'Appendectomy');
    await user.click(dialog.getByRole('button', {name: 'Save'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(await sent(fetch, 'PATCH').json()).toEqual(expect.objectContaining({snomedCode: '80146002', snomedTerm: 'Appendectomy'}));

    await user.click(within(await rowOf('Consent')).getByRole('button', {name: 'Edit stage'}));
    dialog = within(openDialog()!);
    const code = dialog.getByRole('textbox', {name: /SNOMED CT Code/});
    expect(code).toHaveValue('71388002');
    expect(dialog.getByRole('textbox', {name: /Display Term/})).toHaveValue('Procedure');
    await user.clear(code);
    await user.clear(dialog.getByRole('textbox', {name: /Display Term/}));
    await user.click(dialog.getByRole('button', {name: 'Save'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    const patches = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PATCH');
    expect(await patches[1].json()).toEqual(expect.objectContaining({snomedCode: '', snomedTerm: ''}));
  });

  it("shows the API's errors on the SNOMED CT fields, not in the summary", async () => {
    const message = "Give the concept's display term with its SNOMED CT code.";
    const {user} = renderPage(
      {},
      {[`PATCH ${BASE}/d2`]: () => json({title: 'Validation failed', status: 400, errors: {SnomedTerm: [message], SnomedCode: ['Not a concept ID.']}}, 400)},
    );

    await user.click(within(await rowOf('Biopsy')).getByRole('button', {name: 'Edit stage'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /SNOMED CT Code/}), '123');
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(dialog.getByRole('textbox', {name: /Display Term/})).toHaveAccessibleDescription(expect.stringContaining(message)));
    expect(dialog.getByRole('textbox', {name: /SNOMED CT Code/})).toHaveAccessibleDescription(/Not a concept ID\./);
  });

  it('archives after a confirmation, and restores an archived one', async () => {
    const {user, fetch} = renderPage(
      {status: 'any'},
      {[`POST ${BASE}/d1/archive`]: () => json({}), [`POST ${BASE}/d3/restore`]: () => json({})},
    );

    await user.click(within(await rowOf('Consent')).getByRole('button', {name: 'Archive stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));
    await user.click(within(await rowOf('Old triage')).getByRole('button', {name: 'Restore stage'}));

    await waitFor(() => expect(calls(fetch)).toEqual(expect.arrayContaining([`POST ${BASE}/d1/archive`, `POST ${BASE}/d3/restore`])));
  });

  it('deletes only definitions no template uses', async () => {
    const {user, fetch} = renderPage({}, {[`DELETE ${BASE}/d2`]: () => new Response(null, {status: 204})});

    const used = within(await rowOf('Consent')).getByRole('button', {name: /can be archived but not deleted/});
    expect(isDisabled(used)).toBe(true);

    await user.click(within(await rowOf('Biopsy')).getByRole('button', {name: 'Delete stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${BASE}/d2`));
  });

  it('says why an archive failed, with the dialog closed', async () => {
    const {user} = renderPage({}, {[`POST ${BASE}/d1/archive`]: () => json({title: 'The stage is being changed by someone else.'}, 409)});

    await user.click(within(await rowOf('Consent')).getByRole('button', {name: 'Archive stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Archive'}));

    expect(await findAlert('The stage is being changed by someone else.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('says when an add failed for a reason other than its fields', async () => {
    const {user} = renderPage({}, {[`POST ${BASE}`]: () => json({title: 'Server error'}, 500)});

    await user.click(await screen.findByRole('button', {name: 'Add Stage'}));
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Scan');
    await user.click(dialog.getByRole('button', {name: 'Add Stage'}));

    expect(await dialog.findByText('A server error occurred. Please try again later.')).toBeInTheDocument();
  });

  it('says when the stages could not be loaded', async () => {
    renderPage({}, {[`GET ${BASE}`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The stages could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('offers to add the first stage when there are none', async () => {
    const {user} = renderPage({}, {[`GET ${BASE}`]: () => json([])});

    await user.click(await screen.findByRole('button', {name: 'Add Your First Stage'}));

    expect(within(openDialog()!).getByRole('button', {name: 'Add Stage'})).toBeInTheDocument();
  });

  it('offers the archived ones when the search matches no active stage', async () => {
    const {user, onSearchChange} = renderPage({name: 'triage'});

    await user.click(await screen.findByRole('button', {name: 'Show Archived'}));

    expect(onSearchChange).toHaveBeenCalledWith({name: 'triage', page: undefined, status: 'any'});
  });

  it('pages through a long list, keeping the first page out of the URL', async () => {
    const many = Array.from({length: 25}, (_, i) => ({...definitions[1], id: `m${i}`, name: `Synthetic stage ${String(i).padStart(2, '0')}`}));
    const {user, onSearchChange} = renderPage({page: 2}, {[`GET ${BASE}`]: () => json(many)});

    await screen.findByText('Synthetic stage 10');
    await user.click(screen.getByRole('button', {name: 'Go to page 1'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: undefined});
    await user.click(screen.getByRole('button', {name: 'Go to page 3'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({page: 3});
  });
});
