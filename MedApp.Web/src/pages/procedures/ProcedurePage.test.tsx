import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {adminUser, calls, findAlert, json, mockApi, openDialog, pageOf, renderWithApp, staffUser} from '../../test/render';
import {NOT_ASSIGNED_MESSAGE, ProcedurePage, ProcedureView} from './ProcedurePage';

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

  it('starts with Details (Patient info and Assigned staff open) and the Current stage open, beside Stages', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json(procedure)});
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Current stage', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Hide current stage'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Patient info'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Assigned staff'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/patient-1');
    // The current stage's title shows twice: in its section, and as its (closed) header in the stage list.
    expect(screen.getAllByText('2. Procedure')).toHaveLength(2);

    await user.click(screen.getByRole('button', {name: 'Patient info'}));

    expect(screen.getByRole('button', {name: 'Patient info'})).toHaveAttribute('aria-expanded', 'false');
  });

  it('says when every stage is finished', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({...procedure, currentStageId: null})});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByText('Every stage is finished.')).toBeInTheDocument();
  });

  it('says when nobody is assigned', async () => {
    mockApi({[`GET /api/procedures/patient-procedures/${id}`]: () => json({...procedure, assignments: []})});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByText('Nobody is assigned yet.')).toBeInTheDocument();
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

// An in-progress procedure being worked on: stage 1 finished, stage 2 current and editable.
const PATH = `/api/procedures/patient-procedures/${id}`;
const working = (overrides: Record<string, unknown> = {}) => ({
  ...procedure,
  status: 'InProgress',
  blockedFromStatus: null,
  allowedStatusChanges: ['Blocked', 'Cancelled'],
  canWorkOnStages: true,
  assignments: [{userId: 'u1', username: 'test-user', assignedByUserId: 'u2', assignedByUsername: 'admin', assignedAtUtc: '2026-11-01T11:00:00Z'}],
  stages: [
    {...procedure.stages[0], canUnlock: true},
    {...procedure.stages[1], state: 'InProgress', notes: 'Prep done.', instructions: 'Confirm consent first.', canEdit: true},
  ],
  ...overrides,
});
const bodyOf = async (fetch: ReturnType<typeof mockApi>, call: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => `${r.method} ${new URL(r.url).pathname}` === call);
  return request ? request.clone().json() : undefined;
};

describe('ProcedurePage: working on it', () => {
  it('saves the current stage notes and shows the saved procedure', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working({lastUpdated: '2026-11-05T09:00:00Z'})),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    const box = await screen.findByRole('textbox', {name: 'Notes'});
    await user.type(box, ' Consent signed.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    await waitFor(() => expect(calls(fetch)).toContain(`PUT ${PATH}/stages/s2/notes`));
    expect(await bodyOf(fetch, `PUT ${PATH}/stages/s2/notes`)).toEqual({notes: 'Prep done. Consent signed.'});
    expect(openDialog()).toBeNull();
  });

  it('finishes the current stage after confirming, saving unsaved notes first', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working()),
      [`POST ${PATH}/stages/s2/finish`]: () => json(working()),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await user.type(await screen.findByRole('textbox', {name: 'Notes'}), ' All done.');
    await user.click(screen.getByRole('button', {name: 'Finish Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Finish Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s2/finish`));
    const writes = calls(fetch).filter((c) => !c.startsWith('GET'));
    expect(writes).toEqual([`PUT ${PATH}/stages/s2/notes`, `POST ${PATH}/stages/s2/finish`]);
  });

  it('blocks the procedure with a reason', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/status`]: () => json(working({status: 'Blocked'})),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Block'}));
    const dialog = openDialog()!;
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Waiting for bloods.');
    await user.click(within(dialog).getByRole('button', {name: 'Block'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/status`));
    expect(await bodyOf(fetch, `POST ${PATH}/status`)).toEqual({status: 'Blocked', reason: 'Waiting for bloods.'});
  });

  it("asks once per visit before changing a procedure that isn't assigned to you", async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working({assignments: []})),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working({assignments: []})),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await user.type(await screen.findByRole('textbox', {name: 'Notes'}), ' More.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    expect(within(openDialog()!).getByText(NOT_ASSIGNED_MESSAGE)).toBeInTheDocument();
    expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toEqual([]);
    await user.click(within(openDialog()!).getByRole('button', {name: 'Continue'}));
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toHaveLength(1));

    // The second change in the same visit goes straight through.
    await user.type(await screen.findByRole('textbox', {name: 'Notes'}), ' Even more.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toHaveLength(2));
    expect(openDialog()).toBeNull();
  });

  it('does not ask when you back out, and makes no change', async () => {
    const fetch = mockApi({[`GET ${PATH}`]: () => json(working({assignments: []}))});
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Block'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch).filter((c) => !c.startsWith('GET'))).toEqual([]);
  });

  it("shows the API's reason when a change is refused", async () => {
    mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/stages/s2/finish`]: () => json({title: "The 'Procedure' stage is finished and locked."}, 409),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Finish Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Finish Stage'}));

    await findAlert("The 'Procedure' stage is finished and locked.");
  });

  it('keeps assigning and unlocking for admins', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByRole('textbox', {name: 'Notes'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Assign Staff'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^unassign/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Unlock Stage'})).not.toBeInTheDocument();
  });

  it('lets an admin assign staff', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      'GET /api/auth/users/search': () =>
        json(pageOf([{userId: 'u1', username: 'test-user', roles: ['User']}, {userId: 'u3', username: 'k.other', roles: ['User']}])),
      [`POST ${PATH}/assignments/u3`]: () => json(working()),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Assign Staff'}));
    const dialog = openDialog()!;
    // Only staff not already assigned are offered.
    await user.click(await within(dialog).findByRole('checkbox', {name: 'k.other'}));
    expect(within(dialog).queryByRole('checkbox', {name: 'test-user'})).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', {name: 'Assign'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/assignments/u3`));
  });

  it('lets an admin unlock a finished stage', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/stages/s1/unlock`]: () => json(working()),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^1\. Consultation/}));
    await user.click(screen.getByRole('button', {name: 'Unlock Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Unlock Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s1/unlock`));
  });

  it('lets an admin unassign staff', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`DELETE ${PATH}/assignments/u1`]: () => json(working({assignments: []})),
    });
    const {user} = renderWithApp(<ProcedurePage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Unassign test-user'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Unassign'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${PATH}/assignments/u1`));
  });

  it('explains why stages are read-only while blocked', async () => {
    mockApi({[`GET ${PATH}`]: () => json(procedure)});
    renderWithApp(<ProcedurePage id={id} />, {user: staffUser});

    expect(await screen.findByText("Stages can't be worked on while the procedure is blocked.")).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: 'Notes'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Unblock'})).toBeInTheDocument();
  });
});

describe('ProcedurePage instructions', () => {
  // The Current stage section's comes first; the stage list has the same stage's, folded away with it.
  const instructionsToggle = () => screen.getAllByRole('button', {name: 'Instructions'})[0];

  it.each([
    ['full', false],
    ['half', true],
  ])('starts with the instructions folded in %s view, opening when their heading is clicked', async (_, isNarrow) => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    const {user} = renderWithApp(<ProcedureView id={id} isNarrow={isNarrow} />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Current stage', level: 2});
    expect(instructionsToggle()).toHaveAttribute('aria-expanded', 'false');

    await user.click(instructionsToggle());

    expect(instructionsToggle()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getAllByText('Confirm consent first.')[0]).toBeVisible();
  });
});

describe('ProcedurePage on a narrow window', () => {
  function renderNarrow() {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    return renderWithApp(<ProcedureView id={id} isNarrow />, {user: staffUser});
  }

  it('puts everything in one card: Details and Current stage open, Stages folded', async () => {
    renderNarrow();

    expect(await screen.findByRole('heading', {name: 'Details', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Current stage', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Stages', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'View Patient'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Hide current stage'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Show stages'})).toHaveAttribute('aria-expanded', 'false');
    // Patient info starts open; Assigned staff starts folded here, to save room.
    expect(screen.getByRole('button', {name: 'Patient info'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Assigned staff'})).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('textbox', {name: 'Notes'})).toHaveValue('Prep done.');
  });

  it('opens Stages alongside the others, with every stage closed until clicked', async () => {
    const {user} = renderNarrow();

    await user.click(await screen.findByRole('button', {name: 'Show stages'}));

    expect(screen.getByRole('button', {name: 'Hide stages'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: /^1\. Consultation/})).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', {name: /^2\. Procedure/})).toHaveAttribute('aria-expanded', 'false');
  });

  it('works on the current stage in the list once its own section is folded', async () => {
    const {user} = renderNarrow();

    await user.click(await screen.findByRole('button', {name: 'Hide current stage'}));
    await user.click(screen.getByRole('button', {name: 'Show stages'}));
    await user.click(screen.getByRole('button', {name: /^2\. Procedure/}));

    expect(screen.getByRole('textbox', {name: 'Notes'})).toHaveValue('Prep done.');
  });

  it('keeps what is open and folded when the window switches between half and full view', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    function Switchable() {
      const [isNarrow, setIsNarrow] = useState(true);
      return (
        <>
          <button onClick={() => setIsNarrow(!isNarrow)}>Switch layout</button>
          <ProcedureView id={id} isNarrow={isNarrow} />
        </>
      );
    }
    const {user} = renderWithApp(<Switchable />, {user: staffUser});

    // Half view: open Assigned staff, fold the Current stage, open the first stage in the list.
    await user.click(await screen.findByRole('button', {name: 'Assigned staff'}));
    await user.click(screen.getByRole('button', {name: 'Hide current stage'}));
    await user.click(screen.getByRole('button', {name: 'Show stages'}));
    await user.click(screen.getByRole('button', {name: /^1\. Consultation/}));
    await user.click(screen.getAllByRole('button', {name: 'Instructions'})[0]);

    await user.click(screen.getByRole('button', {name: 'Switch layout'}));

    // Full view: the same things open and folded, not the full view's own defaults.
    expect(screen.getByRole('button', {name: 'Assigned staff'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Show current stage'})).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', {name: /^1\. Consultation/})).toHaveAttribute('aria-expanded', 'true');
    // The instructions were opened in half view, and stay open.
    expect(screen.getAllByRole('button', {name: 'Instructions'})[0]).toHaveAttribute('aria-expanded', 'true');
  });
});
