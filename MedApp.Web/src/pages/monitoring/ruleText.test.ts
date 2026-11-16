import {describeRule, sentenceText, type RuleDescription} from './ruleText';

const text = (rule: RuleDescription) => sentenceText(describeRule(rule));
const strong = (rule: RuleDescription) => describeRule(rule).filter((p) => p.strong).map((p) => p.text);

describe('describeRule', () => {
  it('reads the example rule, with the choices strong', () => {
    const rule = {
      event: 'StageFinished',
      subjectUserId: 'u1',
      subjectUsername: 'sam',
      stageDefinitionId: 'd1',
      stageName: 'Bloods',
      minimumPriority: 'High',
    };

    expect(text(rule)).toBe('Notify me when sam finishes Bloods on a High or Urgent priority procedure');
    expect(strong(rule)).toEqual(['sam', 'Bloods', 'High or Urgent']);
  });

  it.each([
    ['ProcedureCreated', 'Notify me when anyone creates a procedure'],
    ['StageFinished', 'Notify me when anyone finishes a stage on a procedure'],
    ['ProcedureCompleted', 'Notify me when anyone completes a procedure'],
    ['ProcedureBlocked', 'Notify me when anyone blocks a procedure'],
    ['ProcedureCancelled', 'Notify me when anyone cancels a procedure'],
    ['ProcedureAssigned', 'Notify me when anyone is assigned to a procedure'],
    ['StageUnlocked', 'Notify me when anyone unlocks a stage on a procedure'],
    ['DueDateChanged', 'Notify me when anyone changes the due date of a procedure'],
    ['SomethingNew', 'Notify me when anyone does something to a procedure'],
  ])('reads %s with no conditions', (event, expected) => {
    expect(text({event})).toBe(expected);
    expect(strong({event})).toEqual([]);
  });

  it.each([
    ['Low', 'a procedure'],
    ['Medium', 'a Medium, High or Urgent priority procedure'],
    ['Urgent', 'an Urgent priority procedure'],
  ])('reads a minimum priority of %s', (minimumPriority, expected) => {
    expect(text({event: 'ProcedureCreated', minimumPriority})).toBe(`Notify me when anyone creates ${expected}`);
  });

  it('names the template', () => {
    expect(text({event: 'ProcedureBlocked', templateId: 't1', templateName: 'Knee review'})).toBe(
      'Notify me when anyone blocks a procedure from the Knee review template',
    );
  });

  it('says what has since been deleted', () => {
    expect(
      text({event: 'StageUnlocked', subjectUserId: 'u9', stageDefinitionId: 'd9', templateId: 't9', subjectUsername: null}),
    ).toBe('Notify me when a deleted user unlocks a deleted stage on a procedure from a deleted template');
  });
});
