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

    expect(text(rule)).toBe('Notify me when sam finishes Bloods on a High or Urgent priority pathway');
    expect(strong(rule)).toEqual(['sam', 'Bloods', 'High or Urgent']);
  });

  it.each([
    ['PathwayCreated', 'Notify me when anyone creates a pathway'],
    ['StageFinished', 'Notify me when anyone finishes a stage on a pathway'],
    ['PathwayCompleted', 'Notify me when anyone discharges a pathway'],
    ['PathwayBlocked', 'Notify me when anyone puts a pathway on hold'],
    ['PathwayCancelled', 'Notify me when anyone cancels a pathway'],
    ['PathwayAssigned', 'Notify me when anyone is assigned to a pathway'],
    ['StageUnlocked', 'Notify me when anyone unlocks a stage on a pathway'],
    ['DueDateChanged', 'Notify me when anyone changes the target date of a pathway'],
    ['SomethingNew', 'Notify me when anyone does something to a pathway'],
  ])('reads %s with no conditions', (event, expected) => {
    expect(text({event})).toBe(expected);
    expect(strong({event})).toEqual([]);
  });

  it.each([
    ['Low', 'a pathway'],
    ['Medium', 'a Medium, High or Urgent priority pathway'],
    ['Urgent', 'an Urgent priority pathway'],
  ])('reads a minimum priority of %s', (minimumPriority, expected) => {
    expect(text({event: 'PathwayCreated', minimumPriority})).toBe(`Notify me when anyone creates ${expected}`);
  });

  it('names the template', () => {
    expect(text({event: 'PathwayBlocked', templateId: 't1', templateName: 'Knee review'})).toBe(
      'Notify me when anyone puts a pathway from the Knee review template on hold',
    );
  });

  it('says what has since been deleted', () => {
    expect(
      text({event: 'StageUnlocked', subjectUserId: 'u9', stageDefinitionId: 'd9', templateId: 't9', subjectUsername: null}),
    ).toBe('Notify me when a deleted user unlocks a deleted stage on a pathway from a deleted template');
  });
});
