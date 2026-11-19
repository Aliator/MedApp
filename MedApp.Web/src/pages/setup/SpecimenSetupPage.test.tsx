import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, expectAnnounced, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {BIOPSY_TYPE_ID, BLOOD_COUNT_TEST_ID, HISTOLOGY_TEST_ID, LAB_TESTS, SPECIMEN_TYPES, SWAB_TYPE_ID} from '../../test/specimens';
import {SpecimenSetupPage, turnaroundWords} from './SpecimenSetupPage';

const ARCHIVED_TYPE = {id: 'archived-type', name: 'Old swab', expectedTurnaroundDays: 2, snomedCode: null, isArchived: true, canBeDeleted: false};

function renderPage(handlers: Record<string, (request: Request) => Response> = {}) {
  const fetch = mockApi({
    'GET /api/specimen-types': () => json([{...SPECIMEN_TYPES[0], snomedCode: '309066003'}, SPECIMEN_TYPES[1], ARCHIVED_TYPE]),
    'GET /api/lab-tests': () => json(LAB_TESTS),
    ...handlers,
  });
  return {...renderWithApp(<SpecimenSetupPage />, {user: adminUser}), fetch};
}

const dialog = () => within(openDialog()!);
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('SpecimenSetupPage', () => {
  it('lists the types with their expected days and codes, and the lab tests with their times, active first', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Sample Types and Lab Tests', level: 1})).toBeInTheDocument();
    const types = within(await screen.findByRole('list', {name: 'Sample types'}));
    expect(types.getByText('Skin biopsy (punch)')).toBeInTheDocument();
    expect(types.getByText('Expected in 5 days · SNOMED CT 309066003')).toBeInTheDocument();
    expect(types.getByText('Expected in 1 day')).toBeInTheDocument();
    expect(types.getByText('Archived')).toBeInTheDocument();
    expect(types.getByRole('button', {name: 'Restore Old swab'})).toBeInTheDocument();
    const tests = within(screen.getByRole('list', {name: 'Lab tests'}));
    expect(tests.getByText('Histology · Target 7 days, limit 10, at risk 2 days before · 2 steps')).toBeInTheDocument();
    expect(tests.getByText('Haematology · Target 1 day, limit 2, at risk 0 days before · 1 step')).toBeInTheDocument();
    expect(screen.getByText('2 active types, 2 active lab tests')).toBeInTheDocument();
    // One a specimen or case has can be archived, not deleted.
    expect(types.getByRole('button', {name: /Skin biopsy \(punch\) has been used/})).toBeInTheDocument();
    expect(tests.getByRole('button', {name: /Skin histology has been used/})).toBeInTheDocument();
    // The external labs list is gone (MC13).
    expect(screen.queryByRole('list', {name: 'Labs'})).not.toBeInTheDocument();
  });

  it('adds a type with its expected days and SNOMED CT code, showing a 409 in the dialog', async () => {
    withAnnouncements();
    let answer = json({title: 'A specimen type with that name already exists.'}, 409);
    const {user, fetch} = renderPage({'POST /api/specimen-types': () => answer});

    await user.click(await screen.findByRole('button', {name: 'Add Sample Type'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Blood culture');
    const days = dialog().getByRole('spinbutton', {name: /Expected turnaround/});
    await user.clear(days);
    await user.type(days, '3');
    await user.type(dialog().getByRole('textbox', {name: /SNOMED CT code/}), ' 119297000 ');
    await user.click(dialog().getByRole('button', {name: 'Add Sample Type'}));

    expect(await dialog().findByText('A specimen type with that name already exists.')).toBeInTheDocument();
    expect(await sent(fetch, 'POST')[0].clone().json()).toEqual({name: 'Blood culture', expectedTurnaroundDays: 3, snomedCode: '119297000'});

    answer = json({id: 'new', name: 'Blood culture', expectedTurnaroundDays: 3}, 201);
    await user.click(dialog().getByRole('button', {name: 'Add Sample Type'}));
    await expectAnnounced('Sample type added');
  });

  it('edits a type, keeping its code, and edits a lab test', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      [`PUT /api/specimen-types/${BIOPSY_TYPE_ID}`]: () => json(SPECIMEN_TYPES[0]),
      [`PUT /api/lab-tests/${BLOOD_COUNT_TEST_ID}`]: () => json(LAB_TESTS[1]),
    });

    await user.click(await screen.findByRole('button', {name: 'Edit Skin biopsy (punch)'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByRole('spinbutton', {name: /Expected turnaround/})).toHaveValue('5');
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    await expectAnnounced('Sample type saved');
    expect(await sent(fetch, 'PUT')[0].json()).toEqual({name: 'Skin biopsy (punch)', expectedTurnaroundDays: 5, snomedCode: '309066003'});

    await user.click(screen.getByRole('button', {name: 'Edit Full blood count'}));
    await waitFor(() => expect(dialog().getByRole('heading', {name: 'Edit Lab Test'})).toBeInTheDocument());
    expect(dialog().getByRole('textbox', {name: /^Name/})).toHaveValue('Full blood count');
    expect(dialog().getByRole('textbox', {name: /Step 1/})).toHaveValue('Analyser run');
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    await expectAnnounced('Lab test saved');
    expect(await sent(fetch, 'PUT')[1].json()).toEqual({
      name: 'Full blood count',
      kind: 'Haematology',
      snomedCode: null,
      targetDays: 1,
      limitDays: 2,
      atRiskDays: 0,
      steps: [{name: 'Analyser run', kind: 'Standard', isOptional: false}],
    });
  });

  it('adds a lab test, showing a refusal in the dialog, and deletes one nothing used', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      'POST /api/lab-tests': () => json({title: "There's already a lab test called 'Skin histology'."}, 409),
      [`DELETE /api/lab-tests/${BLOOD_COUNT_TEST_ID}`]: () => new Response(null, {status: 204}),
    });

    await user.click(await screen.findByRole('button', {name: 'Add Lab Test'}));
    await waitFor(() => expect(dialog().getByRole('heading', {name: 'Add Lab Test'})).toBeInTheDocument());
    await user.type(dialog().getByRole('textbox', {name: /^Name/}), 'Skin histology');
    await user.click(dialog().getByRole('combobox', {name: /Kind/}));
    await user.click(await screen.findByRole('option', {name: 'Histology'}));
    await user.type(dialog().getByRole('textbox', {name: /Step 1/}), 'Cut-up');
    await user.click(dialog().getByRole('button', {name: 'Add Lab Test'}));
    expect(await dialog().findByText("There's already a lab test called 'Skin histology'.")).toBeInTheDocument();
    expect(await sent(fetch, 'POST')[0].clone().json()).toEqual({
      name: 'Skin histology',
      kind: 'Histology',
      snomedCode: null,
      targetDays: 7,
      limitDays: 10,
      atRiskDays: 2,
      steps: [{name: 'Cut-up', kind: 'Standard', isOptional: false}],
    });
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await user.click(screen.getByRole('button', {name: 'Delete Full blood count'}));
    await waitFor(() => expect(dialog().getByRole('heading', {name: 'Confirm Delete'})).toBeInTheDocument());
    await user.click(dialog().getByRole('button', {name: 'Delete Lab Test'}));
    await expectAnnounced('Lab test deleted');
  });

  it('archives, restores and deletes, after asking, showing a refusal on the page', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      [`POST /api/specimen-types/${BIOPSY_TYPE_ID}/archive`]: () => json({...SPECIMEN_TYPES[0], isArchived: true}),
      'POST /api/specimen-types/archived-type/restore': () => json({...ARCHIVED_TYPE, isArchived: false}),
      [`DELETE /api/specimen-types/${SWAB_TYPE_ID}`]: () => new Response(null, {status: 204}),
      [`POST /api/lab-tests/${HISTOLOGY_TEST_ID}/archive`]: () => json({title: 'Synthetic archive refusal.'}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'Archive Skin biopsy (punch)'}));
    await expectAnnounced('Skin biopsy (punch) archived');
    await user.click(screen.getByRole('button', {name: 'Restore Old swab'}));
    await expectAnnounced('Old swab restored');

    await user.click(screen.getByRole('button', {name: 'Delete Wound swab'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByText('Wound swab')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Delete Sample Type'}));
    await expectAnnounced('Sample type deleted');

    await user.click(screen.getByRole('button', {name: 'Archive Skin histology'}));
    expect(await screen.findByText('Synthetic archive refusal.')).toBeInTheDocument();
    expect(sent(fetch, 'DELETE')).toHaveLength(1);
  });

  it("says when the lists couldn't be loaded, or are empty", async () => {
    renderPage({'GET /api/specimen-types': () => json({}, 500), 'GET /api/lab-tests': () => json([])});
    expect(await screen.findByText('The sample types could not be loaded. Please try again.')).toBeInTheDocument();
    expect(await screen.findByText('No lab tests yet. Add the tests the lab does, such as skin histology.')).toBeInTheDocument();
  });

  it('folds the two lists into one card in a narrow window, with both Add buttons', async () => {
    mockApi({'GET /api/specimen-types': () => json(SPECIMEN_TYPES), 'GET /api/lab-tests': () => json(LAB_TESTS)});
    const {user} = renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <SpecimenSetupPage />
      </HalfViewContext.Provider>,
      {user: adminUser},
    );

    expect(await screen.findByRole('list', {name: 'Lab tests'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Sample Type'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Lab Test'})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Hide sample types'}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Sample types'})).not.toBeInTheDocument());
    await user.click(screen.getByRole('button', {name: 'Hide lab tests'}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Lab tests'})).not.toBeInTheDocument());
  });

  it('words the expected days', () => {
    expect(turnaroundWords(1)).toBe('Expected in 1 day');
    expect(turnaroundWords(14)).toBe('Expected in 14 days');
  });
});

describe('SpecimenSetupPage, with entries missing their details', () => {
  it('lists them with what it has, and archives and restores them', async () => {
    withAnnouncements();
    const fetch = mockApi({
      'GET /api/specimen-types': () => json([{id: 'bare-type'}, {id: 'bare-archived-type', isArchived: true}]),
      'GET /api/lab-tests': () => json([{id: 'bare-test'}, {id: 'bare-archived-test', isArchived: true}]),
      'POST /api/specimen-types/bare-type/archive': () => json({id: 'bare-type', isArchived: true}),
      'POST /api/specimen-types/bare-archived-type/restore': () => json({id: 'bare-archived-type'}),
      'POST /api/lab-tests/bare-test/archive': () => json({id: 'bare-test', isArchived: true}),
      'POST /api/lab-tests/bare-archived-test/restore': () => json({title: 'Synthetic restore refusal.', status: 409}, 409),
    });
    const {user} = renderWithApp(<SpecimenSetupPage />, {user: adminUser});

    expect((await screen.findAllByText('Expected in 0 days')).length).toBe(2);
    expect(screen.getAllByText(/Target 0 days, limit 0, at risk 0 days before · 0 steps/).length).toBe(2);
    const archives = screen.getAllByRole('button', {name: /^Archive/});
    await user.click(archives[0]);
    await expectAnnounced('It archived');
    await user.click(archives[1]);
    await waitFor(() => expect(sent(fetch, 'POST').filter((r) => r.url.endsWith('/archive'))).toHaveLength(2));
    const restores = screen.getAllByRole('button', {name: /^Restore/});
    await user.click(restores[0]);
    await expectAnnounced('It restored');
    await user.click(restores[1]);
    expect(await screen.findByText(/Synthetic restore refusal\./)).toBeInTheDocument();
  });
});
