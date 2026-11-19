import {screen, waitFor, within} from '@testing-library/react';
import type {LetterTemplate} from '../../api/letterTemplates';
import {isDisabled, json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {LETTER_PATIENT, LETTER_PATHWAY_ID, testLetter} from '../../test/letters';
import {templatesForLetter, WriteLetterDialog, type LetterAbout} from './WriteLetterDialog';

const DERMATOLOGY = '00000061-0000-0000-0000-0000000000d1';
const UROLOGY = '00000061-0000-0000-0000-0000000000d2';

const template = (fields: Partial<LetterTemplate>): LetterTemplate => ({
  id: `t-${fields.name}`,
  kind: 'ClinicLetter',
  teamId: DERMATOLOGY,
  teamName: 'Dermatology',
  isArchived: false,
  ...fields,
});

const templates = [
  template({name: 'Skin follow-up'}),
  template({name: 'Any discharge', kind: 'DischargeSummary', teamId: null, teamName: null}),
  template({name: 'Referral reply', kind: 'ReferralResponse'}),
  template({name: 'More please', kind: 'InformationRequest', teamId: null, teamName: null}),
  template({name: 'Old letter', isArchived: true}),
  template({name: 'Urology letter', teamId: UROLOGY, teamName: 'Urology'}),
];

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function render(about: LetterAbout, handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({'GET /api/letter-templates': () => json(templates), ...handlers});
  const onCreated = vi.fn();
  const onCancel = vi.fn();
  const result = renderWithApp(
    <WriteLetterDialog about={about} teamId={DERMATOLOGY} patient={LETTER_PATIENT} onCancel={onCancel} onCreated={onCreated} />,
    {user: staffUser},
  );
  return {...result, fetch, onCreated, onCancel};
}

describe('WriteLetterDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("offers the team's and every team's templates (not archived), every kind from a pathway, only replies from a referral", () => {
    const names = (about: LetterAbout) => templatesForLetter(templates, DERMATOLOGY, about).map((t) => t.name);
    expect(names({pathwayId: 'p'})).toEqual(['Any discharge', 'More please', 'Referral reply', 'Skin follow-up']);
    expect(names({referralId: 'r'})).toEqual(['More please', 'Referral reply']);
  });

  it('starts a letter from the template chosen (nothing chosen to begin with), showing the patient', async () => {
    const {user, fetch, onCreated} = render({pathwayId: LETTER_PATHWAY_ID}, {'POST /api/letters': () => json(testLetter(), 201)});

    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Start Letter'}))).toBe(true);
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Template/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: /Skin follow-up/}));
    await user.click(dialog.getByRole('button', {name: 'Start Letter'}));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
    expect(onCreated.mock.calls[0][0]).toMatchObject({id: testLetter().id});
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({templateId: 't-Skin follow-up', pathwayId: LETTER_PATHWAY_ID});
  });

  it("sends a referral's letter with its referral, and shows the API's refusal", async () => {
    const {user, fetch} = render({referralId: 'r1'}, {'POST /api/letters': () => json({title: 'The record is restricted.'}, 409)});

    const dialog = within(await dialogOpen());
    expect(dialog.getByText(/a reply to the referral/)).toBeInTheDocument();
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Template/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: /Referral reply/}));
    await user.click(dialog.getByRole('button', {name: 'Start Letter'}));

    expect(await dialog.findByText('The record is restricted.')).toBeInTheDocument();
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.json()).toEqual({templateId: 't-Referral reply', referralId: 'r1'});
  });

  it('shows a 400 on the template, and any other message', async () => {
    const {user} = render(
      {pathwayId: LETTER_PATHWAY_ID},
      {'POST /api/letters': () => json({title: 'Invalid', status: 400, errors: {TemplateId: ['That template is archived.'], '': ['Something else.']}}, 400)},
    );
    const dialog = within(await dialogOpen());
    await waitFor(() => expect(isDisabled(dialog.getByRole('combobox', {name: /Template/}))).toBe(false));
    await user.click(dialog.getByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: /Skin follow-up/}));
    await user.click(dialog.getByRole('button', {name: 'Start Letter'}));
    expect((await dialog.findAllByText('That template is archived.')).length).toBeGreaterThan(0);
    expect(dialog.getByText('Something else.')).toBeInTheDocument();
  });

  it('says when there are no templates, for a pathway or a referral', async () => {
    mockApi({'GET /api/letter-templates': () => json([])});
    const {unmount} = renderWithApp(<WriteLetterDialog about={{pathwayId: 'p'}} teamId={UROLOGY} onCancel={vi.fn()} onCreated={vi.fn()} />, {
      user: staffUser,
    });
    expect(await screen.findByText(/no letter templates for this team/)).toBeInTheDocument();
    unmount();
    renderWithApp(<WriteLetterDialog about={{referralId: 'r'}} teamId={UROLOGY} onCancel={vi.fn()} onCreated={vi.fn()} />, {user: staffUser});
    expect(await screen.findByText(/no templates for replies to referrals/)).toBeInTheDocument();
  });

  it("says when the templates can't be loaded", async () => {
    mockApi({'GET /api/letter-templates': () => json({title: 'Boom'}, 500)});
    renderWithApp(<WriteLetterDialog about={{pathwayId: 'p'}} teamId={UROLOGY} onCancel={vi.fn()} onCreated={vi.fn()} />, {user: staffUser});
    expect(await screen.findByText('Templates not loaded')).toBeInTheDocument();
  });
});
