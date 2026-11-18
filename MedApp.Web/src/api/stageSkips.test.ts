import {describeSkipReason, MAX_SKIP_DETAIL_LENGTH, skipFieldsComplete, skipNeedsDetail, skipReasonLabel} from './stageSkips';

describe('stage skips', () => {
  it('words each reason, the app’s own too, and anything unknown as it came', () => {
    expect(skipReasonLabel('NotNeeded')).toBe('Not needed');
    expect(skipReasonLabel('PatientDeclined')).toBe('Patient declined');
    expect(skipReasonLabel('Other')).toBe('Other');
    expect(skipReasonLabel('BranchNotChosen')).toBe('Another branch was chosen');
    expect(skipReasonLabel('Later')).toBe('Later');
    expect(skipReasonLabel(null)).toBe('');
  });

  it('adds the detail after the reason, when there is one', () => {
    expect(describeSkipReason('Other', ' Seen at another hospital ')).toBe('Other: Seen at another hospital');
    expect(describeSkipReason('NotNeeded')).toBe('Not needed');
    expect(describeSkipReason(null, 'Only a detail')).toBe('Only a detail');
    expect(describeSkipReason(null, null)).toBe('');
  });

  it('needs a reason, and a detail only for Other, never too long', () => {
    expect(skipNeedsDetail('Other')).toBe(true);
    expect(skipNeedsDetail('NotNeeded')).toBe(false);
    expect(skipFieldsComplete('', '')).toBe(false);
    expect(skipFieldsComplete('NotNeeded', '')).toBe(true);
    expect(skipFieldsComplete('Other', '  ')).toBe(false);
    expect(skipFieldsComplete('Other', 'Why')).toBe(true);
    expect(skipFieldsComplete('NotNeeded', 'x'.repeat(MAX_SKIP_DETAIL_LENGTH + 1))).toBe(false);
  });
});
