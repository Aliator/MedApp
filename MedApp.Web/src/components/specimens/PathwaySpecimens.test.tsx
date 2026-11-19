import {screen, waitFor, within} from '@testing-library/react';
import type {Pathway} from '../../api/pathways';
import {json, mockApi, nurseUser, openDialog, renderWithApp, staffUser} from '../../test/render';
import {
  REQUESTERS,
  SPECIMEN_ID,
  SPECIMEN_PATHWAY_ID,
  SPECIMEN_PATIENT,
  SPECIMEN_STAGE_ID,
  SPECIMEN_TYPES,
  testSpecimen,
  testSummary,
} from '../../test/specimens';
import {PathwaySpecimens, specimensByStage} from './PathwaySpecimens';

const CONSULT_ID = '00000051-0000-0000-0000-0000000000c1';
const FOLLOW_UP_ID = '00000051-0000-0000-0000-0000000000c2';

const stage = (id: string, name: string, canRecordSpecimen: boolean) => ({id, name, canRecordSpecimen, stageOrder: 1, isCurrent: canRecordSpecimen});

const pathway: Pathway = {
  id: SPECIMEN_PATHWAY_ID,
  title: 'Skin lesion review',
  patientId: SPECIMEN_PATIENT.id,
  patient: SPECIMEN_PATIENT,
  stages: [stage(CONSULT_ID, 'Consultation', false), stage(SPECIMEN_STAGE_ID, 'Biopsy', true), stage(FOLLOW_UP_ID, 'Follow-up', false)],
};

const OLDER = '00000051-0000-0000-0000-0000000000d1';

function renderSection(items = [testSummary(), testSummary({id: OLDER, number: 'MA26-000100', status: 'RejectedByLab', takenAtUtc: '2026-10-30T09:00:00Z'})], user = nurseUser) {
  const fetch = mockApi({
    'GET /api/specimens/search': () => json({items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1}),
    'GET /api/specimen-types': () => json(SPECIMEN_TYPES),
    'GET /api/calendar/staff': () => json(REQUESTERS),
    [`POST /api/pathways/${SPECIMEN_PATHWAY_ID}/specimens`]: () => json(testSpecimen({number: 'MA26-000124'}), 201),
  });
  return {...renderWithApp(<PathwaySpecimens pathway={pathway} />, {user}), fetch};
}

describe('PathwaySpecimens', () => {
  it("lists the pathway's samples by the stage that took them, oldest first, each opening its page", async () => {
    const {fetch} = renderSection([testSummary({isOverdueAtLab: true}), testSummary({id: OLDER, number: 'MA26-000100', status: 'RejectedByLab', takenAtUtc: '2026-10-30T09:00:00Z'})]);

    const list = within(await screen.findByRole('list', {name: 'Samples taken at Biopsy'}));
    const links = list.getAllByRole('link');
    expect(links.map((link) => link.textContent)).toEqual(['MA26-000100', 'MA26-000123']);
    expect(links[1]).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(list.getByText('Rejected by the lab')).toBeInTheDocument();
    expect(list.getByText('Sent')).toBeInTheDocument();
    expect(list.getByText('Overdue at lab')).toBeInTheDocument();
    expect(list.getAllByText('Skin biopsy (punch)')).toHaveLength(2);
    expect(list.getByText('Taken 2 Nov 2026, 09:00')).toBeInTheDocument();
    // Stages that took none and can't take one now aren't listed.
    expect(screen.queryByRole('heading', {name: 'Consultation'})).not.toBeInTheDocument();
    expect(fetch.mock.calls.map(([r]) => new URL((r as Request).url).search)[0]).toBe(`?View=All&PathwayId=${SPECIMEN_PATHWAY_ID}&PageSize=100`);
  });

  it('records one at a stage that can take it, then says so with links to it and its label', async () => {
    const {user} = renderSection([]);

    expect(await screen.findByText('None taken yet.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Record Sample'}));
    const dialog = within(await waitFor(() => openDialog()!));
    expect(dialog.getByText('Biopsy')).toBeInTheDocument();
    await user.click(dialog.getByRole('combobox', {name: /Sample type/}));
    await user.click(await screen.findByRole('option', {name: 'Skin biopsy (punch)'}));
    await waitFor(() => expect(dialog.getByRole('combobox', {name: /Requesting clinician/})).toHaveTextContent('test-nurse'));
    await user.click(dialog.getByRole('button', {name: 'Record Sample'}));

    expect(await screen.findByText('Sample MA26-000124 recorded')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open the sample'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getByRole('link', {name: 'Print its label'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}/print?sheet=label`);
    await user.click(screen.getByRole('button', {name: /Dismiss/}));
    expect(screen.queryByText('Sample MA26-000124 recorded')).not.toBeInTheDocument();
  });

  it("offers Record Sample only to holders of RecordSpecimens, and says when there are none", async () => {
    renderSection([], staffUser);
    expect(await screen.findByText('None taken yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Record Sample'})).not.toBeInTheDocument();
  });

  it("says when the samples couldn't be loaded", async () => {
    mockApi({'GET /api/specimens/search': () => json({}, 500)});
    renderWithApp(<PathwaySpecimens pathway={pathway} />, {user: nurseUser});
    expect(await screen.findByText('Samples not loaded')).toBeInTheDocument();
  });

  it('groups by stage in the pathway\'s order, leaving out stages with none that can\'t take one', () => {
    const groups = specimensByStage(pathway.stages ?? [], [testSummary({stageId: FOLLOW_UP_ID})]);
    expect(groups.map((g) => g.stage.name)).toEqual(['Biopsy', 'Follow-up']);
    expect(specimensByStage([stage(CONSULT_ID, 'Consultation', false)], [])).toEqual([]);
  });

  it('puts one with no time taken first', () => {
    const untimed = testSummary({id: 'untimed', stageId: FOLLOW_UP_ID, takenAtUtc: undefined});
    const timed = testSummary({id: 'timed', stageId: FOLLOW_UP_ID});
    const later = testSummary({id: 'later', stageId: FOLLOW_UP_ID, takenAtUtc: undefined});
    const [group] = specimensByStage([stage(FOLLOW_UP_ID, 'Follow-up', false)], [timed, untimed, later]);
    expect(group.specimens.map((s) => s.id)).toEqual(['untimed', 'later', 'timed']);
  });
});
