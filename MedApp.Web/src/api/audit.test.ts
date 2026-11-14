import {auditActionLabel, describeAuditDetails} from './audit';

const describe_ = (action: string, details: unknown) => describeAuditDetails({action, details: JSON.stringify(details)});

describe('auditActionLabel', () => {
  it('puts actions into words', () => {
    expect(auditActionLabel('StageNotesUpdated')).toBe('Stage notes saved');
    expect(auditActionLabel('DetailsUpdated')).toBe('Details edited');
    expect(auditActionLabel('SomethingNew')).toBe('SomethingNew');
  });
});

describe('describeAuditDetails', () => {
  it('says what a procedure was created from', () => {
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

  it('names who was assigned, the notes saved, and what a deleted procedure was', () => {
    expect(describe_('Assigned', {userId: 'u1', username: 'k.ashford'})).toBe('k.ashford');
    expect(describe_('StageNotesUpdated', {from: '', to: 'Sutures removed.'})).toBe('Nothing → "Sutures removed."');
    expect(describe_('Deleted', {status: 'Cancelled'})).toBe('Was cancelled');
  });

  it('has nothing to add for stage steps, or unreadable details', () => {
    expect(describe_('StageFinished', {})).toBe('');
    expect(describeAuditDetails({action: 'Created', details: 'not json'})).toBe('');
  });
});
