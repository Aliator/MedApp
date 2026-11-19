import {BACKWARD_LINK_MESSAGE, NO_PREDECESSOR_MESSAGE, templateGraphProblems} from '../api/pathwayRoutes';
import {DERMATOLOGY_REVIEWED} from './dermatologyReviewed';
import {TEAMS, TEMPLATE_BRANCHES, TEMPLATES} from './ids';
import {seed} from './seed';

describe('Dermatology’s reviewed templates in the light demo', () => {
  const state = seed();
  const dermatology = state.templates.filter((t) => t.teamId === TEAMS.dermatology);
  const named = (name: string) => dermatology.find((t) => t.name === name)!;

  it('replaces Dermatology’s templates with the ten reviewed ones, each with its retention class', () => {
    expect(dermatology.map((t) => t.name)).toEqual(DERMATOLOGY_REVIEWED.map((t) => t.template));
    expect(named('Suspected skin cancer')).toMatchObject({id: TEMPLATES.suspectedSkinCancer, retentionClass: 'Oncology', teamName: 'Dermatology'});
    expect(named('Eczema assessment').retentionClass).toBe('Standard');
  });

  it('draws only forward routes from the first stage, so every template is a tree the model can keep', () => {
    for (const template of dermatology) {
      const problems = [...templateGraphProblems(template.stages!, template.links!).values()];
      expect(problems, template.name!).not.toContain(BACKWARD_LINK_MESSAGE);
      expect(problems, template.name!).not.toContain(NO_PREDECESSOR_MESSAGE);
    }
  });

  it('shares one stage definition per name across the templates, with its SNOMED code, visit and gates', () => {
    const triage = state.stageDefinitions.filter((d) => d.name === 'Triage');
    expect(triage).toHaveLength(1);
    for (const template of dermatology) expect(template.stages![0].stageDefinitionId).toBe(triage[0].id);
    expect(triage[0]).toMatchObject({snomedCode: '225390008', snomedTerm: 'Triage', visitRequirement: 'None'});
    expect(state.stageDefinitions.find((d) => d.name === 'Take biopsy')).toMatchObject({needsConsent: true, visitRequirement: 'Required'});
    expect(state.stageDefinitions.find((d) => d.name === 'Letter to GP and discharge')?.needsLetterKind).toBe('DischargeSummary');
    // A referral letter to the hospital is an onward referral (MC28); one to the GP stays a clinic letter.
    expect(state.stageDefinitions.find((d) => d.name === 'Urgent hospital referral')?.needsLetterKind).toBe('OnwardReferral');
    expect(state.stageDefinitions.find((d) => d.name === 'Refer to hospital')?.needsLetterKind).toBe('OnwardReferral');
    expect(state.stageDefinitions.find((d) => d.name === 'Refer to GP')?.needsLetterKind).toBe('ClinicLetter');
    expect(state.stageDefinitions.find((d) => d.name === 'Histology result')?.waitsForResults).toBe(true);
    // An existing library stage of the same name is reused.
    expect(state.stageDefinitions.filter((d) => d.name === 'Agree care plan')).toHaveLength(1);
  });

  it('maps kinds to choice nodes, the lab’s stages to the lab, and the rest into the description', () => {
    const stages = named('Suspected skin cancer').stages!;
    const stage = (name: string) => stages.find((s) => s.name === name)!;
    expect(stage('Dermoscopy assessment')).toMatchObject({choiceKind: 'Assessment', visitRequirement: 'Required'});
    expect(stage('Histology result').choiceKind).toBe('LabResult');
    expect(stage('Histology')).toMatchObject({ownerTeamId: TEAMS.lab, choiceKind: null});
    expect(stage('Refer to hospital').description).toMatch(/Needs a letter: Referral letter to hospital\. Ends in a referral to the hospital\.$/);
    expect(stage('Dermoscopy assessment').outcomes!.find((o) => o.name === 'Uncertain: biopsy')!.nextStageOrders).toEqual([stage('Take biopsy').stageOrder]);
  });

  it('turns a route back into a repeatable route, a repeat, or an end, and says so', () => {
    const cancer = named('Suspected skin cancer');
    const result = cancer.stages!.find((s) => s.name === 'Histology result')!;
    // Not diagnostic went back to the biopsy: now the Repeat biopsy route, added after Histology.
    expect(result.outcomes!.map((o) => o.name)).not.toContain('Not diagnostic');
    expect(result.description).toContain('Not diagnostic: add the Repeat biopsy route');
    expect(cancer.addableBranches!.map((b) => [b.id === TEMPLATE_BRANCHES.biopsyAnother, b.name, b.isRepeatable])).toEqual([
      [true, 'Biopsy another lesion', false],
      [false, 'Repeat biopsy', true],
    ]);
    expect(cancer.addableBranches![0].stages!.map((s) => s.name)).toEqual(['Take biopsy', 'Histology']);

    // Keep monitoring went back to the review itself: it stays current while it repeats.
    const review = named('Changing mole or lesion check').stages!.find((s) => s.name === 'Mole review')!;
    expect(review.outcomes!.map((o) => o.name)).toEqual(['No change', 'Mole has changed']);
    expect(review.description).toMatch(/Repeats: .* Keep monitoring: stays at this stage/);

    // Benign went back to surveillance's skin check: still recorded, ending the route.
    const surveillance = named('Skin cancer surveillance').stages!.find((s) => s.name === 'Histology result')!;
    expect(surveillance.outcomes!.find((o) => o.name === 'Benign')!.nextStageOrders).toEqual([]);
    expect(surveillance.description).toContain('Benign: back to Skin check in the reviewed pathway');
  });
});
