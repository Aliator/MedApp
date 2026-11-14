import {screen, within} from '@testing-library/react';
import {findAlert, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {ProcedurePage} from './ProcedurePage';

const id = 'proc-1';
const procedure = {
  id,
  patientId: 'patient-1',
  patientName: 'Test Patient',
  templateId: 'tpl-1',
  templateName: 'Diagnostic procedure',
  title: 'Colonoscopy',
  notes: 'Bowel preparation pack posted.',
  dueDate: '2026-12-01',
  dueDateUtc: '2026-12-01T23:59:59Z',
  priority: 'High',
  status: 'Blocked',
  blockedFromStatus: 'InProgress',
  isOverdue: false,
  createdAt: '2026-11-01T10:00:00Z',
  lastUpdated: '2026-11-03T14:30:00Z',
  currentStageId: 's2',
  completedStageCount: 1,
  stageCount: 2,
  allowedStatusChanges: ['InProgress', 'Cancelled'],
  canWorkOnStages: false,
  canEditDetails: true,
  canChangeAssignments: true,
  canBeDeleted: false,
  assignments: [{userId: 'u1', username: 'a.user', assignedByUserId: 'u2', assignedByUsername: 'admin', assignedAtUtc: '2026-11-01T11:00:00Z'}],
  stages: [
    {id: 's1', stageDefinitionId: 'd1', stageOrder: 1, name: 'Consultation', description: '', instructions: '', notes: 'Seen.', state: 'Completed', isCurrent: false, isLocked: true, canEdit: false, canUnlock: false},
    {id: 's2', stageDefinitionId: 'd2', stageOrder: 2, name: 'Procedure', description: '', instructions: '', notes: '', state: 'NotStarted', isCurrent: true, isLocked: false, canEdit: false, canUnlock: false},
  ],
};

describe('ProcedurePage', () => {
  it('shows the procedure, its details and its stages', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json(procedure)});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Colonoscopy', level: 1})).toBeInTheDocument();
    expect(screen.getByText('Diagnostic procedure')).toBeInTheDocument();
    expect(screen.getByText('Blocked')).toBeInTheDocument();
    expect(screen.getByText('Was in progress before it was blocked')).toBeInTheDocument();
    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getByText('1 of 2')).toBeInTheDocument();
    expect(screen.getByText('a.user')).toBeInTheDocument();
    expect(screen.getByText('Bowel preparation pack posted.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/patient-1');
    expect(screen.getByRole('link', {name: 'View Patient'})).toHaveAttribute('href', '/patients/patient-1');
    expect(screen.getByRole('button', {name: /^1\. Consultation/})).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', {name: /^2\. Procedure/})).toHaveAttribute('aria-expanded', 'false');
  });

  it('shows the current stage in full under the details', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json(procedure)});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Current stage', level: 2})).toBeInTheDocument();
    // Its title shows twice: in the details card, and as its (closed) header in the stage list.
    expect(screen.getAllByText('2. Procedure')).toHaveLength(2);
    const current = screen.getByRole('heading', {name: 'Current stage'}).parentElement!;
    expect(within(current).getByText('2. Procedure')).toBeInTheDocument();
    expect(within(current).getByText('No notes yet.')).toBeInTheDocument();
  });

  it('says when every stage is finished', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({...procedure, currentStageId: null})});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByText('Every stage is finished.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Current stage'})).toBeInTheDocument();
  });

  it('says when nobody is assigned', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({...procedure, assignments: []})});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByText('Nobody yet')).toBeInTheDocument();
  });

  it('says when the procedure does not exist', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({title: 'Not found'}, 404)});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await findAlert('Procedure not found. It may have been deleted.');
  });

  it('says when the procedure cannot be loaded', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await findAlert('The procedure could not be loaded. Please try again.');
  });
});
