import {hasStage, inputOf, monitoringEventOf} from './monitoring';

describe('monitoring', () => {
  it('knows which events can name a stage', () => {
    expect(hasStage('StageFinished')).toBe(true);
    expect(hasStage('StageUnlocked')).toBe(true);
    expect(hasStage('PathwayCreated')).toBe(false);
    expect(hasStage(null)).toBe(false);
  });

  it('finds an event by its value', () => {
    expect(monitoringEventOf('PathwayAssigned')?.label).toBe('Is assigned to a pathway');
    expect(monitoringEventOf('Nope')).toBeUndefined();
  });

  it('turns a rule from the API into the fields a save sends', () => {
    expect(
      inputOf({id: 'r1', event: 'StageFinished', subjectUserId: 'u1', minimumPriority: 'High', templateId: 't1', stageDefinitionId: 'd1', isEnabled: false}),
    ).toEqual({subjectUserId: 'u1', event: 'StageFinished', minimumPriority: 'High', templateId: 't1', stageDefinitionId: 'd1', isEnabled: false});
    // What the API leaves out is "any", and on; an event it doesn't know falls back to the first.
    expect(inputOf({id: 'r2', event: 'Unknown'})).toEqual({
      subjectUserId: null,
      event: 'PathwayCreated',
      minimumPriority: null,
      templateId: null,
      stageDefinitionId: null,
      isEnabled: true,
    });
  });
});
