import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, expectAnnounced, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {BIOPSY_TYPE_ID, LABORATORIES, NORTHGATE_LAB_ID, RIVERSIDE_LAB_ID, SPECIMEN_TYPES, SWAB_TYPE_ID} from '../../test/specimens';
import {SpecimenSetupPage, turnaroundWords} from './SpecimenSetupPage';

const ARCHIVED_TYPE = {id: 'archived-type', name: 'Old swab', expectedTurnaroundDays: 2, isArchived: true, canBeDeleted: false};

function renderPage(handlers: Record<string, (request: Request) => Response> = {}) {
  const fetch = mockApi({
    'GET /api/specimen-types': () => json([...SPECIMEN_TYPES, ARCHIVED_TYPE]),
    'GET /api/laboratories': () => json(LABORATORIES),
    ...handlers,
  });
  return {...renderWithApp(<SpecimenSetupPage />, {user: adminUser}), fetch};
}

const dialog = () => within(openDialog()!);
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method);

describe('SpecimenSetupPage', () => {
  it('lists the types with their expected days and the labs, active first, each with its actions', async () => {
    renderPage();

    expect(await screen.findByRole('heading', {name: 'Specimen Types and Labs', level: 1})).toBeInTheDocument();
    const types = within(await screen.findByRole('list', {name: 'Specimen types'}));
    expect(types.getByText('Skin biopsy (punch)')).toBeInTheDocument();
    expect(types.getByText('Expected in 5 days')).toBeInTheDocument();
    expect(types.getByText('Expected in 1 day')).toBeInTheDocument();
    expect(types.getByText('Archived')).toBeInTheDocument();
    expect(types.getByRole('button', {name: 'Restore Old swab'})).toBeInTheDocument();
    const labs = within(screen.getByRole('list', {name: 'Labs'}));
    expect(labs.getByText('Uses MedApp')).toBeInTheDocument();
    expect(labs.getByText("Doesn't use MedApp")).toBeInTheDocument();
    expect(screen.getByText('2 active types, 2 active labs')).toBeInTheDocument();
    // One a specimen has can be archived, not deleted.
    expect(types.getByRole('button', {name: /Skin biopsy \(punch\) has been used/})).toBeInTheDocument();
  });

  it('adds a type with its expected days, showing a 409 in the dialog', async () => {
    withAnnouncements();
    let answer = json({title: 'A specimen type with that name already exists.'}, 409);
    const {user, fetch} = renderPage({'POST /api/specimen-types': () => answer});

    await user.click(await screen.findByRole('button', {name: 'Add Specimen Type'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Blood culture');
    const days = dialog().getByRole('spinbutton', {name: /Expected turnaround/});
    await user.clear(days);
    await user.type(days, '3');
    await user.click(dialog().getByRole('button', {name: 'Add Specimen Type'}));

    expect(await dialog().findByText('A specimen type with that name already exists.')).toBeInTheDocument();
    expect(await sent(fetch, 'POST')[0].clone().json()).toEqual({name: 'Blood culture', expectedTurnaroundDays: 3});

    answer = json({id: 'new', name: 'Blood culture', expectedTurnaroundDays: 3}, 201);
    await user.click(dialog().getByRole('button', {name: 'Add Specimen Type'}));
    await expectAnnounced('Specimen type added');
  });

  it('edits a lab, saying whether it uses MedApp', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({[`PUT /api/laboratories/${RIVERSIDE_LAB_ID}`]: () => json(LABORATORIES[1])});

    await user.click(await screen.findByRole('button', {name: 'Edit Riverside lab'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByRole('textbox', {name: /Name/})).toHaveValue('Riverside lab');
    await user.click(dialog().getByRole('switch', {name: /Uses MedApp/}));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Lab saved');
    expect(await sent(fetch, 'PUT')[0].json()).toEqual({name: 'Riverside lab', usesMedApp: true});
  });

  it('edits a type, adds and deletes a lab, showing a refusal in the dialog', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      [`PUT /api/specimen-types/${BIOPSY_TYPE_ID}`]: () => json(SPECIMEN_TYPES[0]),
      'POST /api/laboratories': () => json({title: 'A lab with that name already exists.'}, 409),
      [`DELETE /api/laboratories/${RIVERSIDE_LAB_ID}`]: () => new Response(null, {status: 204}),
    });

    await user.click(await screen.findByRole('button', {name: 'Edit Skin biopsy (punch)'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByRole('spinbutton', {name: /Expected turnaround/})).toHaveValue('5');
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    await expectAnnounced('Specimen type saved');
    expect(await sent(fetch, 'PUT')[0].json()).toEqual({name: 'Skin biopsy (punch)', expectedTurnaroundDays: 5});

    await user.click(screen.getByRole('button', {name: 'Add Lab'}));
    await waitFor(() => expect(dialog().getByRole('heading', {name: 'Add Lab'})).toBeInTheDocument());
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Northgate pathology');
    await user.click(dialog().getByRole('button', {name: 'Add Lab'}));
    expect(await dialog().findByText('A lab with that name already exists.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    await user.click(screen.getByRole('button', {name: 'Delete Riverside lab'}));
    await waitFor(() => expect(dialog().getByRole('heading', {name: 'Confirm Delete'})).toBeInTheDocument());
    await user.click(dialog().getByRole('button', {name: 'Delete Lab'}));
    await expectAnnounced('Lab deleted');
  });

  it('archives, restores and deletes, after asking, showing a refusal on the page', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({
      [`POST /api/specimen-types/${BIOPSY_TYPE_ID}/archive`]: () => json({...SPECIMEN_TYPES[0], isArchived: true}),
      'POST /api/specimen-types/archived-type/restore': () => json({...ARCHIVED_TYPE, isArchived: false}),
      [`DELETE /api/specimen-types/${SWAB_TYPE_ID}`]: () => new Response(null, {status: 204}),
      [`POST /api/laboratories/${NORTHGATE_LAB_ID}/archive`]: () => json({title: 'Specimens are on their way to this lab.'}, 409),
    });

    await user.click(await screen.findByRole('button', {name: 'Archive Skin biopsy (punch)'}));
    await expectAnnounced('Skin biopsy (punch) archived');
    await user.click(screen.getByRole('button', {name: 'Restore Old swab'}));
    await expectAnnounced('Old swab restored');

    await user.click(screen.getByRole('button', {name: 'Delete Wound swab'}));
    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(dialog().getByText('Wound swab')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Delete Specimen Type'}));
    await expectAnnounced('Specimen type deleted');

    await user.click(screen.getByRole('button', {name: 'Archive Northgate pathology'}));
    expect(await screen.findByText('Specimens are on their way to this lab.')).toBeInTheDocument();
    expect(sent(fetch, 'DELETE')).toHaveLength(1);
  });

  it("says when the lists couldn't be loaded, or are empty", async () => {
    renderPage({'GET /api/specimen-types': () => json({}, 500), 'GET /api/laboratories': () => json([])});
    expect(await screen.findByText('The specimen types could not be loaded. Please try again.')).toBeInTheDocument();
    expect(await screen.findByText('No labs yet. Add the labs specimens are sent to.')).toBeInTheDocument();
  });

  it('folds the two lists into one card in a narrow window, with both Add buttons', async () => {
    mockApi({'GET /api/specimen-types': () => json(SPECIMEN_TYPES), 'GET /api/laboratories': () => json(LABORATORIES)});
    const {user} = renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <SpecimenSetupPage />
      </HalfViewContext.Provider>,
      {user: adminUser},
    );

    expect(await screen.findByRole('list', {name: 'Labs'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Specimen Type'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Lab'})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Hide specimen types'}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Specimen types'})).not.toBeInTheDocument());
    await user.click(screen.getByRole('button', {name: 'Hide labs'}));
    await waitFor(() => expect(screen.queryByRole('list', {name: 'Labs'})).not.toBeInTheDocument());
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
      'GET /api/laboratories': () => json([{id: 'bare-lab'}, {id: 'bare-archived-lab', isArchived: true}]),
      'POST /api/specimen-types/bare-type/archive': () => json({id: 'bare-type', isArchived: true}),
      'POST /api/specimen-types/bare-archived-type/restore': () => json({id: 'bare-archived-type'}),
      'POST /api/laboratories/bare-lab/archive': () => json({id: 'bare-lab', isArchived: true}),
      'POST /api/laboratories/bare-archived-lab/restore': () => json({title: 'Synthetic restore refusal.', status: 409}, 409),
    });
    const {user} = renderWithApp(<SpecimenSetupPage />, {user: adminUser});

    expect((await screen.findAllByText('Expected in 0 days')).length).toBe(2);
    expect(screen.getAllByText("Doesn't use MedApp").length).toBe(2);
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
