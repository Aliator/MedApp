import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, json, mockApi, openDialog, renderWithApp} from '../../test/render';
import {CONSENT_FORM_ID, CONSENT_FORMS} from '../../test/consent';
import {ConsentFormsPage} from './ConsentFormsPage';

function renderPage() {
  const fetch = mockApi({
    'GET /api/consent-forms': () => json(CONSENT_FORMS),
    'POST /api/consent-forms': () => json(CONSENT_FORMS[0], 201),
    [`POST /api/consent-forms/${CONSENT_FORM_ID}/archive`]: () => json({...CONSENT_FORMS[0], isArchived: true}),
  });
  return {...renderWithApp(<ConsentFormsPage />, {user: adminUser}), fetch};
}

describe('ConsentFormsPage', () => {
  it('lists the forms, active first, each with its version', async () => {
    renderPage();
    const list = within(await screen.findByRole('list', {name: 'Consent forms'}));
    expect(list.getByText('Consent for a skin procedure (synthetic)')).toBeInTheDocument();
    expect(list.getByText('Version 3')).toBeInTheDocument();
    expect(list.getByText('Archived')).toBeInTheDocument();
    expect(screen.getByText('1 active forms')).toBeInTheDocument();
  });

  it('adds a form with its name and version', async () => {
    const {user, fetch} = renderPage();
    await user.click(await screen.findByRole('button', {name: 'Add Consent Form'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.type(dialog.getByRole('textbox', {name: /Name/}), 'Synthetic form');
    await user.type(dialog.getByRole('textbox', {name: /Version/}), '2');
    await user.click(dialog.getByRole('button', {name: 'Add Consent Form'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST' && (r as Request).url.endsWith('/api/consent-forms'))).toBe(true));
    const request = fetch.mock.calls.find(([r]) => (r as Request).method === 'POST' && (r as Request).url.endsWith('/api/consent-forms'))![0] as Request;
    expect(await request.json()).toEqual({name: 'Synthetic form', formVersion: '2'});
  });

  it('archives a form', async () => {
    const {user, fetch} = renderPage();
    await user.click(await screen.findByRole('button', {name: /Archive Consent for a skin procedure/}));
    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).url.endsWith('/archive'))).toBe(true));
  });
});

describe('ConsentFormsPage, editing and failing', () => {
  const OLD = CONSENT_FORMS[1];
  const posts = (fetch: ReturnType<typeof mockApi>) => fetch.mock.calls.map(([r]) => r as Request);

  it('edits a form, showing a 400 on its field and a refusal in the API words', async () => {
    let answer = json({title: 'One or more validation errors occurred.', status: 400, errors: {Name: ['Synthetic name problem.']}}, 400);
    const fetch = mockApi({
      'GET /api/consent-forms': () => json(CONSENT_FORMS),
      [`PUT /api/consent-forms/${CONSENT_FORM_ID}`]: () => answer,
    });
    const {user} = renderWithApp(<ConsentFormsPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^Edit Consent for a skin procedure/}));
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByRole('textbox', {name: /Name/})).toHaveValue(CONSENT_FORMS[0].name);
    await user.clear(dialog.getByRole('textbox', {name: /Version/}));
    await user.type(dialog.getByRole('textbox', {name: /Version/}), '4');
    await user.click(dialog.getByRole('button', {name: 'Save'}));
    expect((await dialog.findAllByText('Synthetic name problem.')).length).toBeGreaterThan(0);

    answer = json({title: 'Synthetic: that name and version are taken.', status: 409}, 409);
    await user.click(dialog.getByRole('button', {name: 'Save'}));
    expect(await dialog.findByText(/Synthetic: that name and version are taken\./)).toBeInTheDocument();
    const put = posts(fetch).find((r) => r.method === 'PUT')!;
    expect(await put.clone().json()).toEqual({name: CONSENT_FORMS[0].name, formVersion: '4'});

    answer = json(CONSENT_FORMS[0]);
    await user.click(dialog.getByRole('button', {name: 'Save'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('restores an archived form, and deletes one never used', async () => {
    const fetch = mockApi({
      'GET /api/consent-forms': () => json(CONSENT_FORMS),
      [`POST /api/consent-forms/${OLD.id}/restore`]: () => json({...OLD, isArchived: false}),
      [`DELETE /api/consent-forms/${OLD.id}`]: () => new Response(null, {status: 204}),
    });
    const {user} = renderWithApp(<ConsentFormsPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^Restore Old consent form/}));
    await waitFor(() => expect(posts(fetch).some((r) => r.url.endsWith('/restore'))).toBe(true));

    await user.click(screen.getByRole('button', {name: /^Delete Old consent form/}));
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByText('Old consent form (synthetic)')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Delete Consent Form'}));
    await waitFor(() => expect(posts(fetch).some((r) => r.method === 'DELETE')).toBe(true));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('shows a refusal to archive or delete above the list', async () => {
    mockApi({
      'GET /api/consent-forms': () => json(CONSENT_FORMS),
      [`POST /api/consent-forms/${CONSENT_FORM_ID}/archive`]: () => json({title: 'Synthetic archive refusal.', status: 409}, 409),
      [`DELETE /api/consent-forms/${OLD.id}`]: () => json({title: 'Synthetic delete refusal.', status: 409}, 409),
    });
    const {user} = renderWithApp(<ConsentFormsPage />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^Archive Consent for a skin procedure/}));
    expect(await screen.findByText(/Synthetic archive refusal\./)).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: /^Delete Old consent form/}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('button', {name: 'Delete Consent Form'}));
    expect(await screen.findByText(/Synthetic delete refusal\./)).toBeInTheDocument();
  });

  it('says when there are no forms, or they could not be loaded', async () => {
    mockApi({'GET /api/consent-forms': () => json([])});
    const first = renderWithApp(<ConsentFormsPage />, {user: adminUser});
    expect(await screen.findByText(/No consent forms yet/)).toBeInTheDocument();
    first.unmount();

    mockApi({'GET /api/consent-forms': () => json({title: 'Synthetic failure.', status: 500}, 500)});
    renderWithApp(<ConsentFormsPage />, {user: adminUser});
    expect(await screen.findByText('The consent forms could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
