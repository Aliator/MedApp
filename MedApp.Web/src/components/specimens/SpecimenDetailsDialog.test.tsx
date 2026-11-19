import {screen, waitFor, within} from '@testing-library/react';
import {expectAnnounced, isDisabled, json, mockApi, nurseUser, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {
  BIOPSY_TYPE_ID,
  DOCTOR_USER_ID,
  NURSE_USER_ID,
  REQUESTERS,
  SPECIMEN_ID,
  SPECIMEN_PATHWAY_ID,
  SPECIMEN_PATIENT,
  SPECIMEN_STAGE_ID,
  SPECIMEN_TYPES,
  SWAB_TYPE_ID,
  testSpecimen,
} from '../../test/specimens';
import {SpecimenDetailsDialog, type SpecimenDetailsDialogProps} from './SpecimenDetailsDialog';

const RECORD = `POST /api/pathways/${SPECIMEN_PATHWAY_ID}/specimens`;
const EDIT = `PUT /api/specimens/${SPECIMEN_ID}`;

function renderDialog(props: Partial<SpecimenDetailsDialogProps> = {}, handlers: Record<string, (request: Request) => Response> = {}, user = nurseUser) {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const fetch = mockApi({
    'GET /api/specimen-types': () => json(SPECIMEN_TYPES),
    'GET /api/calendar/staff': () => json(REQUESTERS),
    ...handlers,
  });
  const result = renderWithApp(
    <SpecimenDetailsDialog
      isOpen
      onClose={onClose}
      onSaved={onSaved}
      patient={SPECIMEN_PATIENT}
      record={{pathwayId: SPECIMEN_PATHWAY_ID, pathwayTitle: 'Skin lesion review', stageId: SPECIMEN_STAGE_ID, stageName: 'Biopsy'}}
      {...props}
    />,
    {user},
  );
  return {...result, fetch, onClose, onSaved};
}

const dialog = () => within(openDialog()!);
const sentBody = (fetch: ReturnType<typeof mockApi>, method: string) =>
  (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method) as Request).clone().json();

async function choose(user: ReturnType<typeof renderDialog>['user'], field: RegExp, option: string) {
  await user.click(dialog().getByRole('combobox', {name: field}));
  await user.click(await screen.findByRole('option', {name: option}));
}

describe('SpecimenDetailsDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T14:05:00Z')});
  });

  it('records a sample: the patient, stage and pathway shown, you as the requester, taken now', async () => {
    withAnnouncements();
    const recorded = testSpecimen();
    const {user, fetch, onSaved, onClose} = renderDialog({}, {[RECORD]: () => json(recorded, 201)});

    expect(await screen.findByRole('heading', {name: 'Record Sample'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('Skin lesion review')).toBeInTheDocument();
    expect(dialog().getByText('Biopsy')).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Requesting clinician/})).toHaveTextContent('test-nurse'));
    expect(dialog().getByRole('combobox', {name: /Taken on/})).toHaveValue('18 November 2026');
    expect(dialog().getByRole('textbox', {name: /Time taken/})).toHaveValue('14:05');
    expect(isDisabled(dialog().getByRole('button', {name: 'Record Sample'}))).toBe(true);

    await choose(user, /Sample type/, 'Skin biopsy (punch)');
    await user.type(dialog().getByRole('textbox', {name: /Site/}), 'Left forearm');
    await user.type(dialog().getByRole('textbox', {name: /Notes for the lab/}), 'Please process today');
    await user.click(dialog().getByRole('button', {name: 'Record Sample'}));

    await expectAnnounced('Sample MA26-000123 recorded');
    expect(await sentBody(fetch, 'POST')).toEqual({
      stageId: SPECIMEN_STAGE_ID,
      specimenTypeId: BIOPSY_TYPE_ID,
      site: 'Left forearm',
      requestNotes: 'Please process today',
      requestingUserId: NURSE_USER_ID,
      labTestId: null,
      // Left at now: the API takes now.
      takenAtUtc: null,
    });
    expect(onSaved).toHaveBeenCalledWith(recorded);
    expect(onClose).toHaveBeenCalled();
  });

  it('sends an earlier time and another requester, and shows a 400 on the fields and a 409 in its words', async () => {
    let answer = json({title: 'Invalid', errors: {TakenAtUtc: ["It can't be more than 7 days ago."]}}, 400);
    const {user, fetch} = renderDialog({}, {[RECORD]: () => answer});

    await screen.findByRole('heading', {name: 'Record Sample'});
    await choose(user, /Sample type/, 'Wound swab');
    await choose(user, /Requesting clinician/, 'dr.synthetic');
    const time = dialog().getByRole('textbox', {name: /Time taken/});
    await user.clear(time);
    await user.type(time, '09:30');
    await user.tab();
    await user.click(dialog().getByRole('button', {name: 'Record Sample'}));

    expect(await dialog().findByText("It can't be more than 7 days ago.")).toBeInTheDocument();
    expect(await sentBody(fetch, 'POST')).toEqual(
      expect.objectContaining({specimenTypeId: SWAB_TYPE_ID, requestingUserId: DOCTOR_USER_ID, takenAtUtc: '2026-11-18T09:30:00.000Z', site: null}),
    );

    answer = json({title: "This stage can't take a sample now: the pathway is on hold."}, 409);
    await user.click(dialog().getByRole('button', {name: 'Record Sample'}));
    expect(await dialog().findByText("This stage can't take a sample now: the pathway is on hold.")).toBeInTheDocument();
  });

  it("starts with nobody as the requester when you can't be one", async () => {
    renderDialog({}, {}, staffUser);
    await screen.findByRole('heading', {name: 'Record Sample'});
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Requesting clinician/})).toHaveTextContent('Choose who asked for it'));
  });

  it('corrects a sample from the version shown, keeping its time and an archived type or deleted requester', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog(
      {
        record: undefined,
        specimen: testSpecimen({requestingUserId: 'gone', requestingUsername: 'Deleted user', specimenTypeId: 'old', specimenTypeName: 'Old type'}),
      },
      {
        'GET /api/specimen-types': () => json([...SPECIMEN_TYPES, {id: 'old', name: 'Old type', expectedTurnaroundDays: 3, isArchived: true}]),
        [EDIT]: () => json(testSpecimen({version: 4})),
      },
    );

    expect(await screen.findByRole('heading', {name: 'Edit Sample MA26-000123'})).toBeInTheDocument();
    await waitFor(() => expect(dialog().getByRole('combobox', {name: /Sample type/})).toHaveTextContent('Old type (archived)'));
    expect(dialog().getByRole('combobox', {name: /Requesting clinician/})).toHaveTextContent('Deleted user');
    expect(dialog().getByRole('textbox', {name: /Site/})).toHaveValue('Left forearm');
    expect(dialog().getByRole('combobox', {name: /Taken on/})).toHaveValue('2 November 2026');

    await choose(user, /Sample type/, 'Skin biopsy (punch)');
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    await expectAnnounced('Sample details saved');
    expect(await sentBody(fetch, 'PUT')).toEqual({
      specimenTypeId: BIOPSY_TYPE_ID,
      site: 'Left forearm',
      requestNotes: 'Synthetic notes for the lab.',
      requestingUserId: 'gone',
      labTestId: 'ed000000-0000-0000-0000-000000000001',
      takenAtUtc: '2026-11-02T09:00:00Z',
      expectedVersion: 3,
    });
    expect(onClose).toHaveBeenCalled();
  });

  it("says when the lists couldn't be loaded", async () => {
    renderDialog(
      {},
      {
        'GET /api/specimen-types': () => json({}, 500),
        'GET /api/calendar/staff': () => json({}, 500),
      },
    );
    expect(await screen.findByText('The sample types could not be loaded.')).toBeInTheDocument();
    expect(await screen.findByText('The staff could not be loaded.')).toBeInTheDocument();
  });
});
