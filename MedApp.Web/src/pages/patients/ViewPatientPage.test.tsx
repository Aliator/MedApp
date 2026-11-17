import {screen, waitFor, within} from '@testing-library/react';
import type userEvent from '@testing-library/user-event';
import {adminUser, calls, isDisabled, json, openAlertDetails, mockApi, openDialog, pageOf, renderWithApp, staffUser} from '../../test/render';
import {ViewPatientPage} from './ViewPatientPage';

const id = 'c184a7b8-2585-4437-bcd4-e1700be38299';
const oriel = {id, firstName: 'Oriel', lastName: 'Penhallow', dateOfBirth: '1951-03-14', email: 'oriel.penhallow@example.com'};
const ACCESS = '/api/activity/search';
const CHANGED = "Someone else changed this patient's record while you were working on it. Reload it to see their change, then try again.";

type User = ReturnType<typeof userEvent.setup>;

/** Opens the Privacy menu and returns its item whose name starts with `name`. */
async function privacyItem(user: User, name: string) {
  await user.click(await screen.findByRole('button', {name: 'Privacy'}));
  // The page has the one menu; closed, its items stay in the page, so they're found hidden.
  return screen.findByRole('menuitem', {name: (accessible) => accessible.startsWith(name), hidden: true});
}

/** The patient the API has now: a change's response becomes what it returns, as the server would. */
let current: object = oriel;
const changesTo = (patient: object) => () => {
  current = patient;
  return json(patient);
};

function renderPage(patient: object = oriel, user = adminUser, extra: Record<string, (request: Request) => Response> = {}) {
  current = patient;
  const fetch = mockApi({
    [`GET /api/patients/${id}`]: () => json(current),
    'GET /api/procedures/patient-procedures/search': () => json(pageOf([], 0)),
    [`GET ${ACCESS}`]: () =>
      json(pageOf([{id: 'e1', action: 'PatientViewed', username: 'k.ashford', atUtc: '2026-11-15T10:00:00Z', patientId: id, details: '{}'}])),
    ...extra,
  });
  return {fetch, ...renderWithApp(<ViewPatientPage id={id} />, {user, path: `/patients/${id}`})};
}

describe('ViewPatientPage, privacy', () => {
  it('shows staff no Privacy menu and no access report, and never asks for one', async () => {
    const {fetch} = renderPage(oriel, staffUser);

    expect(await screen.findByText('Oriel')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Privacy'})).not.toBeInTheDocument();
    expect(screen.queryByText('Access Report')).not.toBeInTheDocument();
    // The patient and their timeline, but not the activity log.
    expect(calls(fetch).some((call) => call.startsWith(`GET ${ACCESS}`))).toBe(false);
  });

  it('shows admins the access report and the Privacy menu, with the export page', async () => {
    const {user} = renderPage();

    expect(await screen.findByText('Access Report')).toBeInTheDocument();
    expect(await screen.findByText('k.ashford')).toBeInTheDocument();
    const exportItem = await privacyItem(user, 'Export Record');
    expect(exportItem.closest('a') ?? exportItem).toHaveAttribute('href', `/patients/${id}/export`);
    expect(isDisabled(await privacyItem(user, 'Anonymise Record'))).toBe(false);
  });

  it('restricts the record with a reason, then shows its banner and hides Edit', async () => {
    const restricted = {...oriel, isRestricted: true, restrictionReason: 'Synthetic accuracy check', restrictedAtUtc: '2026-11-16T09:00:00Z'};
    const {user, fetch} = renderPage(oriel, adminUser, {[`PUT /api/patients/${id}/restriction`]: changesTo(restricted)});

    expect(await screen.findByRole('link', {name: 'Edit Patient'})).toBeInTheDocument();
    await user.click(await privacyItem(user, 'Restrict Record'));
    const dialog = await waitFor(() => openDialog()!);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    await user.click(within(dialog).getByRole('button', {name: 'Restrict Record'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(within(await openAlertDetails(user, 'Restricted record')).getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Edit Patient'})).not.toBeInTheDocument();
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(new URL(put.url).pathname).toBe(`/api/patients/${id}/restriction`);
    expect(await put.json()).toEqual({set: true, reason: 'Synthetic accuracy check', confirmOpenProcedures: false});
  });

  it('keeps the flag dialog open, with the reason typed, when someone else changed the record meanwhile', async () => {
    const {user} = renderPage(oriel, adminUser, {[`PUT /api/patients/${id}/restriction`]: () => json({title: CHANGED}, 409)});

    await user.click(await privacyItem(user, 'Restrict Record'));
    const dialog = await waitFor(() => openDialog()!);
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Synthetic accuracy check');
    await user.click(within(dialog).getByRole('button', {name: 'Restrict Record'}));

    expect(await within(dialog).findByText(CHANGED)).toBeInTheDocument();
    expect(openDialog()).not.toBeNull();
    expect(within(dialog).getByRole('textbox', {name: /reason/i})).toHaveValue('Synthetic accuracy check');
  });

  it('lifts a restriction without a reason (sent as none), and says why the API refused', async () => {
    const restricted = {...oriel, isRestricted: true, restrictionReason: 'Synthetic accuracy check'};
    const {user, fetch} = renderPage(restricted, adminUser, {
      [`PUT /api/patients/${id}/restriction`]: () => json({title: "This patient's record has been anonymised, so it can't be changed."}, 409),
    });

    expect(await screen.findByRole('button', {name: 'Restricted record, show details'})).toBeInTheDocument();
    expect(isDisabled(await privacyItem(user, 'Anonymise Record'))).toBe(true);
    await user.click(await privacyItem(user, 'Lift Restriction'));
    await user.click(within(await waitFor(() => openDialog()!)).getByRole('button', {name: 'Lift Restriction'}));

    expect(await screen.findByText("This patient's record has been anonymised, so it can't be changed.")).toBeInTheDocument();
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await put.json()).toEqual({set: false, reason: null, confirmOpenProcedures: false});
  });

  it('marks the record as disputed, and clears a dispute', async () => {
    const disputed = {...oriel, isDisputed: true, disputeReason: 'Synthetic date of birth'};
    const {user, fetch} = renderPage(oriel, adminUser, {[`PUT /api/patients/${id}/dispute`]: changesTo(disputed)});

    await user.click(await privacyItem(user, 'Mark as Disputed'));
    const dialog = await waitFor(() => openDialog()!);
    await user.type(within(dialog).getByRole('textbox', {name: /what's disputed/i}), 'Synthetic date of birth');
    await user.click(within(dialog).getByRole('button', {name: 'Mark Disputed'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(within(await openAlertDetails(user, 'Disputed record')).getByText("What's disputed: Synthetic date of birth")).toBeInTheDocument();
    expect(calls(fetch)).toContain(`PUT /api/patients/${id}/dispute`);
    await user.click(await privacyItem(user, 'Clear Dispute'));
    await user.click(within(await waitFor(() => openDialog()!)).getByRole('button', {name: 'Clear Dispute'}));

    await waitFor(() => expect(calls(fetch).filter((c) => c === `PUT /api/patients/${id}/dispute`)).toHaveLength(2));
    const last = fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === 'PUT').pop()!;
    expect(await last.json()).toEqual({set: false, reason: null, confirmOpenProcedures: false});
  });

  it('cancels a flag change and anonymising without asking the API', async () => {
    const {user, fetch} = renderPage();

    await user.click(await privacyItem(user, 'Mark as Disputed'));
    await user.click(within(await waitFor(() => openDialog()!)).getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await user.click(await privacyItem(user, 'Anonymise Record'));
    expect(await screen.findByRole('textbox', {name: /type oriel penhallow to confirm/i})).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(calls(fetch).some((c) => c.startsWith('PUT') || c.startsWith('POST'))).toBe(false);
  });

  it('anonymises the record, then shows it as anonymised, with nothing left to change', async () => {
    const anonymised = {...oriel, firstName: 'Anonymised', lastName: 'Patient', isAnonymised: true, anonymisedAtUtc: '2026-11-16T09:00:00Z'};
    const {user} = renderPage(oriel, adminUser, {[`POST /api/patients/${id}/anonymise`]: changesTo(anonymised)});

    await user.click(await privacyItem(user, 'Anonymise Record'));
    const dialog = await waitFor(() => openDialog()!);
    await user.click(within(dialog).getByRole('radio', {name: /past its retention period/i}));
    await user.type(within(dialog).getByRole('textbox', {name: /to confirm/i}), 'Oriel Penhallow');
    await user.click(within(dialog).getByRole('button', {name: 'Anonymise Record'}));

    expect(await screen.findByRole('button', {name: 'Anonymised record, show details'})).toBeInTheDocument();
    await waitFor(() => expect(openDialog()).toBeNull());
    expect(screen.queryByRole('link', {name: 'Edit Patient'})).not.toBeInTheDocument();
    expect(isDisabled(await privacyItem(user, 'Restrict Record'))).toBe(true);
    expect(isDisabled(await privacyItem(user, 'Mark as Disputed'))).toBe(true);
  });

  it("lists and downloads a restricted record's files, offering nothing to add or delete, to admins too", async () => {
    const restricted = {...oriel, isRestricted: true, restrictionReason: 'Synthetic accuracy check', restrictedAtUtc: '2026-11-16T09:00:00Z'};
    renderPage(restricted, adminUser, {
      'GET /api/attachments': () =>
        json({
          items: [
            {
              id: 'f1',
              patientId: id,
              attachedTo: 'Patient',
              fileName: 'synthetic-letter.pdf',
              typeName: 'PDF',
              sizeBytes: 10,
              scanStatus: 'NotScanned',
              uploadedByUsername: 'k.ashford',
              uploadedAtUtc: '2026-11-15T10:00:00Z',
              canDelete: true,
              isDeleted: false,
            },
          ],
          totalCount: 1,
        }),
    });

    expect(await screen.findByText('synthetic-letter.pdf')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Download synthetic-letter.pdf'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add files'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete synthetic-letter.pdf'})).not.toBeInTheDocument();
  });
});
