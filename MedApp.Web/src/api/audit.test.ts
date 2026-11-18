import {ATTACHMENT_AUDIT_ACTIONS, auditActionLabel, describeAuditDetails, isPatientFileEntry} from './audit';

const describe_ = (action: string, details: unknown) => describeAuditDetails({action, details: JSON.stringify(details)});

describe('auditActionLabel', () => {
  it('puts actions into words', () => {
    expect(auditActionLabel('StageNotesUpdated')).toBe('Stage notes saved');
    expect(auditActionLabel('DetailsUpdated')).toBe('Details edited');
    expect(auditActionLabel('SomethingNew')).toBe('SomethingNew');
    expect(auditActionLabel(null)).toBe('');
  });

  it('names the actions on files', () => {
    expect(ATTACHMENT_AUDIT_ACTIONS.map(auditActionLabel)).toEqual(['File attached', 'File deleted', 'File downloaded']);
  });
});

describe('describeAuditDetails', () => {
  it('says what a pathway was created from', () => {
    expect(describe_('Created', {template: 'Diagnostic procedure', dueDate: '2026-11-07', priority: 'High', stageCount: 7})).toBe(
      'From Diagnostic procedure, 7 stages, due 7 Nov 2026, high priority',
    );
  });

  it('lists the details edited, saying only that notes changed', () => {
    expect(
      describe_('DetailsUpdated', {
        changes: {
          title: {from: 'Scan', to: 'Knee scan'},
          dueDate: {from: '2026-11-07', to: '2026-12-01'},
          priority: {from: 'Low', to: 'Urgent'},
          notes: {from: 'a', to: 'b'},
        },
      }),
    ).toBe('Title "Scan" → "Knee scan"; Due 7 Nov 2026 → 1 Dec 2026; Priority low → urgent; Notes changed');
  });

  it('gives a status change, whether it was automatic, and the reason', () => {
    expect(describe_('StatusChanged', {from: 'InProgress', to: 'InReview', automatic: true})).toBe('In progress → in review (automatic)');
    expect(describe_('StatusChanged', {from: 'InProgress', to: 'Blocked', automatic: false, reason: 'Awaiting results'})).toBe(
      'In progress → blocked: Awaiting results',
    );
  });

  it("gives a deleted note's version and the admin's reason in words, with its detail, never its text", () => {
    expect(auditActionLabel('NoteDeleted')).toBe('Note deleted');
    expect(
      describe_('NoteDeleted', {noteId: 'n1', version: 2, deletionReason: 'WrongPatient', detail: ' Synthetic: the other pathway. ', requestId: 'r1'}),
    ).toBe('Version 2: Wrong patient: Synthetic: the other pathway.');
    expect(describe_('NoteDeleted', {noteId: 'n1', version: 1, deletionReason: 'Duplicate'})).toBe('Version 1: Duplicate');
    expect(describe_('NoteDeleted', {noteId: 'n1', version: 1})).toBe('Version 1');
    expect(describe_('NoteDeleted', {detail: 'Synthetic detail.'})).toBe('Synthetic detail.');
  });

  it('names who was assigned, the notes saved, and what a deleted pathway was', () => {
    expect(describe_('Assigned', {userId: 'u1', username: 'k.ashford'})).toBe('k.ashford');
    expect(describe_('StageNotesUpdated', {from: '', to: 'Sutures removed.'})).toBe('Nothing → "Sutures removed."');
    expect(describe_('Deleted', {status: 'Cancelled'})).toBe('Was cancelled');
  });

  it('says when a stage was finished without a visit, with its requirement and the reason', () => {
    expect(describe_('StageFinished', {withoutVisit: true, visitRequirement: 'Required', reason: 'Seen at another clinic'})).toBe(
      'Finished without a visit (required): Seen at another clinic',
    );
    expect(describe_('StageFinished', {withoutVisit: true, visitRequirement: 'Suggested', reason: null})).toBe('Finished without a visit (suggested)');
    expect(describe_('StageFinished', {withoutVisit: true})).toBe('Finished without a visit');
    expect(describe_('StageFinished', {withoutVisit: false, visitRequirement: 'Required'})).toBe('');
  });

  it('has nothing to add for stage steps, or unreadable details', () => {
    expect(describe_('StageFinished', {})).toBe('');
    expect(describe_('StageStarted', {})).toBe('');
    expect(describeAuditDetails({action: 'Created', details: 'not json'})).toBe('');
  });

  it('copes with details that are missing, or not an object', () => {
    expect(describeAuditDetails({action: 'Deleted', details: null})).toBe('');
    expect(describeAuditDetails({action: 'Assigned', details: '"k.ashford"'})).toBe('');
    expect(describeAuditDetails({action: 'Deleted', details: 'null'})).toBe('');
  });

  it("names a note's version, never its text", () => {
    expect(describe_('NoteAdded', {noteId: 'n1', version: 1})).toBe('Version 1');
    expect(describe_('NoteEdited', {noteId: 'n1', version: 3})).toBe('Version 3');
    expect(describe_('NoteEdited', {noteId: 'n1'})).toBe('');
  });

  it('copes with parts of the details left out', () => {
    expect(describe_('Created', {stageCount: 1})).toBe('1 stage');
    expect(describe_('DetailsUpdated', {})).toBe('');
    expect(describe_('DetailsUpdated', {changes: {dueDate: {from: null, to: '2026-12-01'}}})).toBe('Due none → 1 Dec 2026');
    expect(describe_('Unassigned', {userId: 'u1'})).toBe('');
    expect(describe_('Deleted', {})).toBe('');
  });

  it('says a detail it has no words for changed, by its name', () => {
    expect(describe_('DetailsUpdated', {changes: {location: {from: 'Ward 1', to: 'Ward 2'}}})).toBe('location changed');
  });

  it('puts values that are not words into words as they are', () => {
    expect(describe_('DetailsUpdated', {changes: {priority: {from: null, to: 3}}})).toBe('Priority  → 3');
  });

  it("says which file, how large, and what it's attached to", () => {
    const details = {attachmentId: 'a1', fileName: 'scan.pdf', contentType: 'application/pdf', size: 1258291, attachedTo: 'Stage'};
    expect(describe_('AttachmentAdded', details)).toBe('scan.pdf (1.2 MB), on the stage');
    expect(describe_('AttachmentDeleted', {...details, attachedTo: 'Patient'})).toBe('scan.pdf (1.2 MB), on the patient');
    expect(describe_('AttachmentDownloaded', {...details, attachedTo: 'Pathway'})).toBe('scan.pdf (1.2 MB), on the pathway');
  });

  it('copes with a file entry missing its details', () => {
    expect(describe_('AttachmentAdded', {})).toBe('A file');
    expect(describe_('AttachmentAdded', {fileName: ' ', size: '12', attachedTo: 'Elsewhere'})).toBe('A file');
  });
});

describe('isPatientFileEntry', () => {
  it("knows an entry about a patient's own file, which has no pathway", () => {
    expect(isPatientFileEntry({patientPathwayId: '00000000-0000-0000-0000-000000000000'})).toBe(true);
    expect(isPatientFileEntry({patientPathwayId: '00000034-0000-0000-0000-000000000001'})).toBe(false);
  });
});
