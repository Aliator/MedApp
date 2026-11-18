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
