import {hasPerson, hasStage, hasTemplate, inputOf, isServiceAlert, monitoringEventOf} from './monitoring';

describe('monitoring', () => {
  it('knows which events can name a stage', () => {
    expect(hasStage('StageFinished')).toBe(true);
    expect(hasStage('StageUnlocked')).toBe(true);
    expect(hasStage('StageFinishedWithoutVisit')).toBe(true);
    expect(hasStage('PathwayCreated')).toBe(false);
    expect(hasStage(null)).toBe(false);
    expect(hasStage('VisitNotBooked')).toBe(true);
    expect(hasStage('SpecimenOverdue')).toBe(true);
    expect(hasStage('WaitBreached')).toBe(false);
  });

  it('knows the service alerts, and which name a person or a template', () => {
    expect(isServiceAlert('ReferralNotTriaged')).toBe(true);
    expect(isServiceAlert('StageFinished')).toBe(false);
    expect(hasPerson('StageFinished')).toBe(true);
    expect(hasPerson('ResultNotSignedOff')).toBe(true);
    expect(hasPerson('VisitNotBooked')).toBe(false);
    expect(hasTemplate('ReferralNotTriaged')).toBe(false);
    expect(hasTemplate('WaitBreached')).toBe(true);
  });

  it('finds an event by its value', () => {
    expect(monitoringEventOf('PathwayAssigned')?.label).toBe('Is assigned to a pathway');
    expect(monitoringEventOf('Nope')).toBeUndefined();
  });

  it('turns a rule from the API into the fields a save sends', () => {
    expect(
      inputOf({id: 'r1', event: 'StageFinished', subjectUserId: 'u1', minimumPriority: 'High', templateId: 't1', stageDefinitionId: 'd1', isEnabled: false}),
    ).toEqual({
      subjectUserId: 'u1',
      event: 'StageFinished',
      minimumPriority: 'High',
      templateId: 't1',
      stageDefinitionId: 'd1',
      thresholdDays: null,
      teamId: null,
      isEnabled: false,
    });
    expect(inputOf({id: 'r3', event: 'ReferralNotTriaged', thresholdDays: 2, teamId: 's1'})).toMatchObject({
      event: 'ReferralNotTriaged',
      thresholdDays: 2,
      teamId: 's1',
    });
    // What the API leaves out is "any", and on; an event it doesn't know falls back to the first.
    expect(inputOf({id: 'r2', event: 'Unknown'})).toEqual({
      subjectUserId: null,
      event: 'PathwayCreated',
      minimumPriority: null,
      templateId: null,
      stageDefinitionId: null,
      thresholdDays: null,
      teamId: null,
      isEnabled: true,
    });
  });
});
