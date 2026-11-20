import {BACKWARD_LINK_MESSAGE, NO_PREDECESSOR_MESSAGE, templateGraphProblems} from '../api/pathwayRoutes';
import {call, startLightDemo} from '../test/lightDemo';
import {ADMIN, EYE_CLINIC_ADMIN, LAB, STAFF} from './accounts';
import {currentStage} from './handlers/pathways';
import {APPOINTMENTS, id, PATHWAYS, PATIENTS, REFERRALS, TEAMS, TEMPLATES} from './ids';
import {OPHTHALMOLOGY_REVIEWED} from './ophthalmologyReviewed';
import {seed} from './seed';
import {LAB_CASES} from './slices/labCases';
import {LAB_TESTS, SPECIMENS} from './slices/lab';
import {LETTER_TEMPLATES} from './slices/clinical';
import {db} from './store';
import {nowUtc, today} from './time';

const PATHWAY = '/api/pathways/patient-pathways';

describe('the Eye clinic in the light demo', () => {
  describe('its templates', () => {
    const state = seed();
    const eye = state.templates.filter((t) => t.teamId === TEAMS.eyeClinic);
    const named = (name: string) => eye.find((t) => t.name === name)!;

    it('has the four from ophthalmology.yaml, owned by the Eye clinic, with fixed IDs', () => {
      expect(eye.map((t) => t.name)).toEqual(OPHTHALMOLOGY_REVIEWED.map((t) => t.template));
      expect(eye.map((t) => t.id)).toEqual([TEMPLATES.cataract, TEMPLATES.glaucoma, TEMPLATES.redEye, TEMPLATES.macular]);
      expect(eye.every((t) => t.teamName === 'Eye clinic' && t.retentionClass === 'Standard')).toBe(true);
    });

    it('draws only forward routes, so every template is a tree the model can keep', () => {
      for (const template of eye) {
        const problems = [...templateGraphProblems(template.stages!, template.links!).values()];
        expect(problems, template.name!).not.toContain(BACKWARD_LINK_MESSAGE);
        expect(problems, template.name!).not.toContain(NO_PREDECESSOR_MESSAGE);
      }
    });

    it('maps them as Dermatology’s are: choice nodes, the lab’s Microbiology, letters by kind and the end outcome', () => {
      const stages = named('Red eye assessment').stages!;
      const stage = (name: string) => stages.find((s) => s.name === name)!;
      expect(stage('Eye examination')).toMatchObject({choiceKind: 'Assessment', visitRequirement: 'Required'});
      expect(stage('Eye examination').outcomes!.map((o) => o.name)).toEqual(['Settling: advice', 'Swab needed', 'Sight-threatening']);
      expect(stage('Eye examination').outcomes![1].nextStageOrders).toEqual([stage('Take eye swab').stageOrder]);
      expect(stage('Microbiology')).toMatchObject({ownerTeamId: TEAMS.lab, ownerTeamName: 'Lab', choiceKind: null});
      expect(stage('Microbiology result').choiceKind).toBe('LabResult');
      expect(stage('Urgent hospital eye referral').description).toMatch(/Needs a letter: Urgent referral letter to hospital\. Ends with a referral to the hospital\.$/);
      expect(named('Glaucoma assessment').stages!.find((s) => s.name === 'Back to optometrist monitoring')!.description).toMatch(/Ends with monitoring\.$/);

      const definition = (name: string) => state.stageDefinitions.find((d) => d.name === name)!;
      expect(definition('Urgent hospital eye referral')).toMatchObject({needsLetterKind: 'OnwardReferral', snomedCode: '306206005'});
      expect(definition('Back to optometrist monitoring').needsLetterKind).toBe('DischargeSummary');
      expect(definition('Biometry')).toMatchObject({visitRequirement: 'Required', needsLetterKind: null});
    });

    it('shares the library’s stages by name with Dermatology, keeping Dermatology’s IDs', () => {
      for (const name of ['Triage', 'Final letter to GP', 'Microbiology', 'Microbiology result'])
        expect(state.stageDefinitions.filter((d) => d.name === name), name).toHaveLength(1);
      expect(state.stageDefinitions.find((d) => d.id === id(0xd3, 0x20))!.name).toBe('Triage');
      const dermatologyTriage = state.templates.find((t) => t.id === TEMPLATES.suspectedSkinCancer)!.stages![0].stageDefinitionId;
      for (const template of eye) expect(template.stages![0].stageDefinitionId).toBe(dermatologyTriage);
    });

    it('gives the Eye clinic the letter templates its stages need, by name', () => {
      const names = state.letterTemplates.filter((t) => t.teamId === TEAMS.eyeClinic).map((t) => [t.id, t.name, t.kind]);
      expect(names).toEqual([
        [LETTER_TEMPLATES.eyeFinalLetter, 'Final letter to GP', 'DischargeSummary'],
        [LETTER_TEMPLATES.eyeReferral, 'Referral letter to hospital', 'OnwardReferral'],
        [LETTER_TEMPLATES.eyeUrgentReferral, 'Urgent referral letter to hospital', 'OnwardReferral'],
      ]);
      expect(state.letterTemplates.find((t) => t.id === LETTER_TEMPLATES.eyeFinalLetter)!.bodySkeleton).toContain('last seen in the Eye clinic');
    });
  });

  describe('its seed', () => {
    const state = seed();
    const pathway = (pathwayId: string) => state.pathways.find((p) => p.id === pathwayId)!;
    const appointment = (appointmentId: string) => state.appointments.find((a) => a.id === appointmentId)!;

    it('has the four pathways at the spec’s stages, Saira Varma’s but the one at Triage', () => {
      const at = (pathwayId: string) => currentStage(pathway(pathwayId))?.name;
      expect(at(PATHWAYS.eyeCataract)).toBe('Biometry');
      expect(pathway(PATHWAYS.eyeCataract).stages[1].outcome).toBe('Wants surgery and suitable');
      expect(at(PATHWAYS.eyeGlaucoma)).toBe('Glaucoma assessment');
      expect(at(PATHWAYS.eyeRedEye)).toBe('Microbiology');
      expect(pathway(PATHWAYS.eyeRedEye).stages[1].outcome).toBe('Swab needed');
      expect(at(PATHWAYS.eyeMacular)).toBe('Triage');
      expect(pathway(PATHWAYS.eyeMacular)).toMatchObject({status: 'Unassigned', assignments: [], priority: 'High'});
      for (const pathwayId of [PATHWAYS.eyeCataract, PATHWAYS.eyeGlaucoma, PATHWAYS.eyeRedEye])
        expect(pathway(pathwayId).assignments.map((a) => a.userId)).toEqual([EYE_CLINIC_ADMIN.id]);
      expect(state.pathways.filter((p) => p.teamId === TEAMS.eyeClinic)).toHaveLength(4);
      expect(pathway(PATHWAYS.eyeGlaucoma).patientId).toBe(PATIENTS.oliverMaddox);
    });

    it('books Biometry and the glaucoma visit ahead with Saira Varma, after the visits already attended', () => {
      for (const appointmentId of [APPOINTMENTS.edithBiometry, APPOINTMENTS.oliverGlaucoma]) {
        expect(appointment(appointmentId)).toMatchObject({status: 'Booked', staffUserId: EYE_CLINIC_ADMIN.id, teamId: TEAMS.eyeClinic});
        expect(appointment(appointmentId).startUtc! > nowUtc()).toBe(true);
      }
      expect(appointment(APPOINTMENTS.edithBiometry).stageName).toBe('Biometry');
      expect(appointment(APPOINTMENTS.oliverGlaucoma).stageName).toBe('Glaucoma assessment');
      expect(appointment(APPOINTMENTS.edithAssessment)).toMatchObject({status: 'Attended', stageName: 'Cataract assessment'});
      expect(appointment(APPOINTMENTS.freyaExam)).toMatchObject({status: 'Attended', stageName: 'Eye examination'});
      expect(appointment(APPOINTMENTS.freyaExam).startUtc! < nowUtc()).toBe(true);
    });

    it('has Freya Tolland’s swab received by the lab, its case open on the eye swab culture, Microbiology picked up', () => {
      const swab = state.specimens.find((s) => s.id === SPECIMENS.freyaSwab)!;
      const microbiology = currentStage(pathway(PATHWAYS.eyeRedEye))!;
      expect(swab).toMatchObject({status: 'Received', labTestId: LAB_TESTS.eyeSwabCulture, requestingTeamId: TEAMS.eyeClinic, labCaseId: LAB_CASES.freyaSwab});
      expect(swab.receivedAtUtc! < nowUtc()).toBe(true);
      expect(microbiology).toMatchObject({ownerTeamId: TEAMS.lab, handedOverFromTeamId: TEAMS.eyeClinic, pickedUpByUserId: LAB.id, pickedUpAtUtc: swab.receivedAtUtc});
      expect(state.labCases.find((c) => c.id === LAB_CASES.freyaSwab)).toMatchObject({status: 'Open', stageId: microbiology.id, requestingTeamId: TEAMS.eyeClinic});
      expect(state.pathwayAccess.some((a) => a.pathwayId === PATHWAYS.eyeRedEye && a.teamId === TEAMS.lab && a.reason === 'StageOwner')).toBe(true);
    });

    it('has Ruth Penhaligon’s referral to the Eye clinic awaiting triage', () => {
      expect(state.referrals.find((r) => r.id === REFERRALS.eyeToTriage)).toMatchObject({
        patientId: PATIENTS.ruthPenhaligon,
        teamId: TEAMS.eyeClinic,
        status: 'AwaitingTriage',
        requestedPriority: 'High',
      });
    });
  });

  describe('through the stand-in API', () => {
    it('gives Saira Varma a referral to triage on Home, and her pathways to open', async () => {
      startLightDemo(EYE_CLINIC_ADMIN);
      expect((await call('GET', '/api/referrals/awaiting-triage/count')).body).toEqual({count: 1, inYourTeams: true});
      const opened = await call('GET', `${PATHWAY}/${PATHWAYS.eyeCataract}`);
      expect(opened.status).toBe(200);
      expect(opened.body).toMatchObject({canWorkOnStages: true, readOnlyReason: null});
      const mine = (await call('GET', `${PATHWAY}/search?AssignedToMe=true&PageSize=50`)).body;
      expect(mine.items.map((p: {id: string}) => p.id).sort()).toEqual([PATHWAYS.eyeCataract, PATHWAYS.eyeGlaucoma, PATHWAYS.eyeRedEye].sort());
      const calendar = (await call('GET', `/api/calendar?From=${today()}&To=${today()}`)).body;
      expect(calendar.blocks.length).toBeGreaterThan(0);
    });

    it('shows Ravi Aldous the Eye clinic’s swab: its case open, next the culture', async () => {
      startLightDemo(LAB);
      const swabCase = await call('GET', `/api/lab-cases/${LAB_CASES.freyaSwab}`);
      expect(swabCase.status).toBe(200);
      expect(swabCase.body).toMatchObject({status: 'Open', next: {kind: 'Step', name: 'Culture'}, canRecord: true});
      expect((await call('GET', `${PATHWAY}/${PATHWAYS.eyeRedEye}`)).body).toMatchObject({canWorkOnStages: true});
      expect((await call('GET', `${PATHWAY}/${PATHWAYS.eyeCataract}`)).status).toBe(404);
    });

    it('lets the admin see all three teams, the Eye clinic’s pathways too', async () => {
      startLightDemo(ADMIN);
      const teams = (await call('GET', '/api/teams')).body;
      expect(teams.map((t: {name: string}) => t.name)).toEqual(expect.arrayContaining(['Dermatology', 'Eye clinic', 'Lab']));
      expect((await call('GET', `${PATHWAY}/${PATHWAYS.eyeRedEye}`)).status).toBe(200);
    });

    it('keeps each clinic’s pathways behind the wall: the other’s a 404 and a summary line on Oliver Maddox', async () => {
      startLightDemo(STAFF);
      expect((await call('GET', `${PATHWAY}/${PATHWAYS.eyeGlaucoma}`)).status).toBe(404);
      expect((await call('GET', `/api/lab-cases/${LAB_CASES.freyaSwab}`)).status).toBe(404);
      const oliver = (await call('GET', `/api/patients/${PATIENTS.oliverMaddox}`)).body;
      expect(oliver.otherTeamPathways).toEqual([expect.objectContaining({teamName: 'Eye clinic', state: 'InProgress'})]);
      expect((await call('GET', `/api/referrals/${REFERRALS.eyeToTriage}`)).status).toBe(404);
      expect(db().activityLog.some((e) => e.action === 'TeamBoundaryRefused' && e.details?.includes(PATHWAYS.eyeGlaucoma))).toBe(true);

      startLightDemo(EYE_CLINIC_ADMIN);
      expect((await call('GET', `${PATHWAY}/${PATHWAYS.biopsyToBook}`)).status).toBe(404);
      expect((await call('GET', `/api/referrals/${REFERRALS.dermatologyToTriage}`)).status).toBe(404);
      const search = (await call('GET', `${PATHWAY}/search?PatientId=${PATIENTS.oliverMaddox}&IncludeClosed=true`)).body;
      expect(search.items.map((p: {id: string}) => p.id)).toEqual([PATHWAYS.eyeGlaucoma]);
      const patient = (await call('GET', `/api/patients/${PATIENTS.ellisVarden}`)).body;
      expect(patient.otherTeamPathways).toEqual([expect.objectContaining({teamName: 'Dermatology'})]);
    });
  });
});
