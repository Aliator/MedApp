import {screen, waitFor, within} from '@testing-library/react';
import type {PathwayTemplate} from '../../api/pathways';
import {calls, isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {setRichText} from '../../test/richText';
import {DERMATOLOGY_ID, REFERRAL_ID, testReferral, UROLOGY_ID} from '../../test/referrals';
import {AcceptReferralDialog, TARGET_FROM_CLOCK, templatesFor} from './AcceptReferralDialog';

const ACCEPT = `POST /api/referrals/${REFERRAL_ID}/accept`;
const stages = [{stageOrder: 1, name: 'Review'}];
const TEMPLATES: PathwayTemplate[] = [
  {id: 't-skin', name: 'Skin check', isArchived: false, teamId: DERMATOLOGY_ID, stages},
  {id: 't-urology', name: 'Bladder review', isArchived: false, teamId: UROLOGY_ID, stages},
  {id: 't-old', name: 'Acne (old)', isArchived: true, teamId: DERMATOLOGY_ID, stages},
  {id: 't-empty', name: 'Empty', isArchived: false, teamId: DERMATOLOGY_ID, stages: []},
  {id: 't-biopsy', name: 'biopsy pathway', isArchived: false, teamId: DERMATOLOGY_ID, stages},
];
const STAFF = [
  {userId: 'u-sam', username: 's.nurse'},
  {userId: 'u-kim', username: 'k.ashford'},
];
const PATHWAY = {id: 'new-pathway', patientId: 'p', title: 'Skin check'};

const clinician = userWithRoles('d.clinician', 'Clinician');
const assigningClinician = userWithRoles('d.lead', 'Clinician', 'Manager');

function render(user = clinician, handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'GET /api/pathways/patient-pathway-stage/templates': () => json(TEMPLATES),
    'GET /api/calendar/staff': () => json(STAFF),
    [ACCEPT]: () => json(PATHWAY, 201),
    [`GET /api/referrals/${REFERRAL_ID}`]: () => json(testReferral()),
    ...handlers,
  });
  const onCancel = vi.fn();
  const onAccepted = vi.fn();
  const result = renderWithApp(<AcceptReferralDialog isOpen referral={testReferral({canTriage: true})} onCancel={onCancel} onAccepted={onAccepted} />, {user});
  return {...result, fetch, onCancel, onAccepted};
}

const dialog = () => within(openDialog()!);
const posted = async (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!).json();

async function choose(user: ReturnType<typeof render>['user'], field: RegExp, option: string) {
  await user.click(dialog().getByRole('combobox', {name: field}));
  await user.click(await screen.findByRole('option', {name: option}));
}

/** Fills in what accepting needs: a template and a priority (the target date is the waiting-time clock's). */
async function fillRequired(user: ReturnType<typeof render>['user']) {
  await choose(user, /Template/, 'Skin check');
  await choose(user, /Priority/, 'Medium');
}

describe('templatesFor', () => {
  it("lists only the referral's team's templates in use, by name, ignoring case", () => {
    expect(templatesFor(TEMPLATES, DERMATOLOGY_ID).map((t) => t.name)).toEqual(['biopsy pathway', 'Skin check']);
    expect(templatesFor(TEMPLATES, UROLOGY_ID).map((t) => t.name)).toEqual(['Bladder review']);
    expect(templatesFor(TEMPLATES, null)).toEqual([]);
  });
});

describe('AcceptReferralDialog', () => {
  it("prefills nothing: the team's templates by name, and an empty priority beside the one asked for", async () => {
    const {user} = render();

    expect(await screen.findByRole('heading', {name: 'Accept Referral'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('The referrer asked for: High')).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Priority/})).toHaveTextContent('Choose a priority');
    expect(dialog().getByRole('textbox', {name: 'Title'})).toHaveValue('');
    // No target date to choose: the waiting-time clock sets it, for the priority chosen.
    expect(dialog().queryByRole('combobox', {name: /Target date/})).not.toBeInTheDocument();
    expect(dialog().getByText(TARGET_FROM_CLOCK)).toBeInTheDocument();
    // Assigning needs AssignPathways, which a clinician doesn't have.
    expect(dialog().queryByRole('combobox', {name: /Assign to/})).not.toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);

    await waitFor(() => expect(isDisabled(dialog().getByRole('combobox', {name: /Template/}))).toBe(false));
    expect(dialog().getByRole('combobox', {name: /Template/})).toHaveTextContent('Choose a template');
    await user.click(dialog().getByRole('combobox', {name: /Template/}));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['biopsy pathway', 'Skin check']);
  });

  it('checks the choices with the patient shown, goes back with them kept, then accepts', async () => {
    const {user, fetch, onAccepted} = render();

    await screen.findByRole('heading', {name: 'Accept Referral'});
    await fillRequired(user);
    await user.click(dialog().getByRole('button', {name: 'Continue'}));

    expect(await screen.findByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('Dermatology')).toBeInTheDocument();
    expect(dialog().getByText('Skin check')).toBeInTheDocument();
    expect(dialog().getByText('Medium')).toBeInTheDocument();
    expect(dialog().getByText('From the waiting-time clock, for this priority')).toBeInTheDocument();
    expect(dialog().getByText("Skin check (the template's name)")).toBeInTheDocument();
    expect(dialog().getByText('None')).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(await screen.findByRole('heading', {name: 'Accept Referral'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Priority/})).toHaveTextContent('Medium');
    await user.type(dialog().getByRole('textbox', {name: 'Title'}), 'Skin check for the rash');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Accept Referral'}));

    await waitFor(() => expect(onAccepted).toHaveBeenCalledWith(PATHWAY));
    expect(await posted(fetch)).toEqual({
      templateId: 't-skin',
      priority: 'Medium',
      title: 'Skin check for the rash',
      assigneeUserId: null,
      notes: null,
      expectedVersion: 2,
    });
  }, 15_000);

  it('assigns someone and adds a first note for a holder of AssignPathways', async () => {
    const {user, fetch} = render(assigningClinician);

    await screen.findByRole('heading', {name: 'Accept Referral'});
    await fillRequired(user);
    await waitFor(() => expect(isDisabled(dialog().getByRole('combobox', {name: /Assign to/}))).toBe(false));
    await user.click(dialog().getByRole('combobox', {name: /Assign to/}));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['Nobody yet', 'k.ashford', 's.nurse']);
    await user.click(screen.getByRole('option', {name: 's.nurse'}));
    await setRichText(dialog().getByRole('textbox', {name: /First note/}), 'Synthetic: seen in clinic soon.');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));

    expect(await dialog().findByText('s.nurse')).toBeInTheDocument();
    expect(dialog().getByText('Synthetic: seen in clinic soon.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Accept Referral'}));
    await waitFor(() => expect(calls(fetch)).toContain(ACCEPT));
    expect(await posted(fetch)).toMatchObject({assigneeUserId: 'u-sam', notes: 'Synthetic: seen in clinic soon.'});
    expect(calls(fetch)).toContain('GET /api/calendar/staff?permission=WorkPathways');
  }, 15_000);

  it('goes back to the form with a 400 on its fields', async () => {
    const {user} = render(clinician, {
      [ACCEPT]: () => json({title: 'Validation failed', status: 400, errors: {Priority: ['Choose a priority for the pathway.']}}, 400),
    });

    await screen.findByRole('heading', {name: 'Accept Referral'});
    await fillRequired(user);
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Accept Referral'}));

    expect(await screen.findByRole('heading', {name: 'Accept Referral'})).toBeInTheDocument();
    expect((await dialog().findAllByText('Choose a priority for the pathway.')).length).toBeGreaterThan(0);
  });

  it("shows a 409 in the API's words, staying on the check", async () => {
    const {user} = render(clinician, {
      [ACCEPT]: () => json({title: 'k.ashford has already rejected this referral.', status: 409}, 409),
    });

    await screen.findByRole('heading', {name: 'Accept Referral'});
    await fillRequired(user);
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Accept Referral'}));

    expect(await dialog().findByText('k.ashford has already rejected this referral.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
  });

  it("says when the team has no templates, and cancels", async () => {
    const {user, onCancel} = render(clinician, {'GET /api/pathways/patient-pathway-stage/templates': () => json([TEMPLATES[1]])});

    expect(await screen.findByText(/^Dermatology has no pathway templates yet/)).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });

  it("says when the templates can't be loaded", async () => {
    render(clinician, {'GET /api/pathways/patient-pathway-stage/templates': () => json({title: 'Down'}, 500)});

    expect(await screen.findByText('The templates could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
