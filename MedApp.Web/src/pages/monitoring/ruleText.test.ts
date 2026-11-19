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
    ['StageFinishedWithoutVisit', 'Notify me when anyone finishes a stage without its required visit on a pathway'],
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

  it.each([
    ['ReferralNotTriaged', 2, 'Notify me when a referral in Dermatology has awaited triage more than 2 days'],
    ['ResultNotSignedOff', 1, 'Notify me when a result on a pathway in Dermatology has awaited sign-off more than 1 day'],
    ['VisitNotBooked', 3, 'Notify me when a visit for a stage on a pathway in Dermatology has awaited booking more than 3 days'],
    ['SpecimenOverdue', 0, 'Notify me when a sample from a stage on a pathway in Dermatology is past its expected turnaround'],
    ['WaitBreached', 7, 'Notify me when the waiting-time target of a referral in Dermatology is less than 7 days away, or passed'],
    ['WaitBreached', 0, 'Notify me when the waiting-time target of a referral in Dermatology has passed'],
    ['PauseReviewPassed', 0, 'Notify me when the review date of a paused clock on a referral in Dermatology has passed'],
  ])('reads the service alert %s (%i days)', (event, thresholdDays, expected) => {
    const rule = {event, thresholdDays, teamId: 's1', teamName: 'Dermatology'};
    expect(text(rule)).toBe(expected);
    expect(strong(rule)).toContain('Dermatology');
  });

  it('names the responsible clinician for results, and a priority on a referral', () => {
    expect(text({event: 'ResultNotSignedOff', thresholdDays: 2, subjectUserId: 'u1', subjectUsername: 'sam'})).toBe(
      'Notify me when a result for sam on a pathway has awaited sign-off more than 2 days',
    );
    expect(text({event: 'ReferralNotTriaged', thresholdDays: 2, minimumPriority: 'Urgent'})).toBe(
      'Notify me when an Urgent priority referral has awaited triage more than 2 days',
    );
  });

  it('names a team on a change too', () => {
    expect(text({event: 'PathwayCreated', teamId: 's1', teamName: 'Podiatry'})).toBe('Notify me when anyone creates a pathway in Podiatry');
  });
});
