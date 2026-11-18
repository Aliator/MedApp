import {describeNoteDeletionReason, isNoteDeletionReason, needsDetail, NOTE_DELETION_REASONS, noteDeletionReasonLabel} from './noteDeletion';

describe('note deletion reasons', () => {
  it('are the API’s four, by name, in words', () => {
    expect(NOTE_DELETION_REASONS.map((r) => r.value)).toEqual(['WrongPatient', 'WrittenInError', 'Duplicate', 'Other']);
    expect(noteDeletionReasonLabel('WrongPatient')).toBe('Wrong patient');
    expect(noteDeletionReasonLabel('WrittenInError')).toBe('Written in error');
    // Anything unknown, as it came.
    expect(noteDeletionReasonLabel('Retired')).toBe('Retired');
    expect(noteDeletionReasonLabel(null)).toBe('');
  });

  it('tells a reason from anything else', () => {
    expect(isNoteDeletionReason('Duplicate')).toBe(true);
    expect(isNoteDeletionReason('duplicate')).toBe(false);
    expect(isNoteDeletionReason(null)).toBe(false);
  });

  it('describes a reason with its detail, or either alone', () => {
    expect(describeNoteDeletionReason('WrongPatient', ' Synthetic: the other procedure. ')).toBe('Wrong patient: Synthetic: the other procedure.');
    expect(describeNoteDeletionReason('Duplicate', '  ')).toBe('Duplicate');
    expect(describeNoteDeletionReason(null, 'Synthetic detail.')).toBe('Synthetic detail.');
    expect(describeNoteDeletionReason(undefined, undefined)).toBe('');
  });

  it('needs a detail only for Other', () => {
    expect(needsDetail('Other')).toBe(true);
    expect(needsDetail('WrongPatient')).toBe(false);
    expect(needsDetail('')).toBe(false);
  });
});
