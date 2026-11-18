import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {adminUser, calls, findAlert, json, managerUser, mockApi, nurseUser, openAlertDetails, openDialog, pageOf, renderWithApp, staffUser, expectAnnounced, withAnnouncements} from '../../test/render';
import {NOT_ASSIGNED_MESSAGE, PathwayPage, PathwayView, finishedMessage} from './PathwayPage';
import {richText, typeRichText} from '../../test/richText';
import {testAppointment, testRequested} from '../../test/calendar';

const id = 'proc-1';
const pathway = {
  id,
  patientId: 'patient-1',
  patientName: 'Test Patient',
  templateId: 'tpl-1',
  templateName: 'Diagnostic procedure',
  title: 'Colonoscopy',
  canAddNotes: true,
  noteList: [{id: 'n1', authorUserId: 'u1', authorUsername: 'a.user', text: 'Bowel preparation pack posted.', createdAtUtc: '2026-11-01T10:30:00Z', version: 1, canEdit: false}],
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

describe('PathwayPage', () => {
  it("shows the patient's banner: who the pathway is for", async () => {
    const patient = {id: 'patient-1', firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: '9990000026', isRestricted: true};
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({...pathway, patient})});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    const banner = await screen.findByRole('region', {name: 'Patient: Patient, Test'});
    expect(within(banner).getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(within(banner).getByText('Restricted')).toBeInTheDocument();
  });

  it('opens a request to delete a note from the address for an admin, and closes it', async () => {
    const requestId = '0000000d-0000-0000-0000-000000000001';
    const fetch = mockApi({
      [`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway),
      [`GET /api/pathways/note-deletion-requests/${requestId}`]: () =>
        json({id: requestId, status: 'Declined', reason: 'Synthetic reason.', requestedByUsername: 's.nurse', requestedAtUtc: '2026-11-17T10:00:00Z', pathwayId: id, pathwayTitle: 'Colonoscopy', decidedByUsername: 'other.admin', decidedAtUtc: '2026-11-17T11:00:00Z', note: null, canDecide: false}),
    });
    const onClose = vi.fn();
    const {user} = renderWithApp(<PathwayPage id={id} deletionRequest={requestId} onCloseDeletionRequest={onClose} />, {user: adminUser});

    expect(await screen.findByText(/^other\.admin kept this note/)).toBeInTheDocument();
    // The footer's Close (the header has its own).
    await user.click(within(openDialog()!).getAllByRole('button', {name: 'Close'}).at(-1)!);
    expect(onClose).toHaveBeenCalled();
    expect(calls(fetch)).toContain(`GET /api/pathways/note-deletion-requests/${requestId}`);
  });

  it("goes to the request's own pathway when the address names another pathway's request", async () => {
    const requestId = '0000000d-0000-0000-0000-000000000001';
    const otherPathway = '0000000d-0000-0000-0000-0000000000aa';
    mockApi({
      [`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway),
      [`GET /api/pathways/note-deletion-requests/${requestId}`]: () =>
        json({id: requestId, status: 'Pending', reason: 'Duplicate', requestedByUsername: 's.nurse', requestedAtUtc: '2026-11-17T10:00:00Z', pathwayId: otherPathway, canDecide: true}),
    });
    const elsewhere = vi.fn();
    renderWithApp(<PathwayPage id={id} deletionRequest={requestId} onDeletionRequestElsewhere={elsewhere} />, {user: adminUser});

    await waitFor(() => expect(elsewhere).toHaveBeenCalledWith(otherPathway, requestId));
    expect(openDialog()).toBeNull();
  });

  it("never opens a request to delete a note for staff, who can't read it", async () => {
    const fetch = mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway)});
    renderWithApp(<PathwayPage id={id} deletionRequest="0000000d-0000-0000-0000-000000000001" />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colonoscopy', level: 1});
    expect(openDialog()).toBeNull();
    expect(calls(fetch).some((call) => call.includes('note-deletion-requests'))).toBe(false);
  });

  it('has no banner when the pathway comes without its patient', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway)});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colonoscopy', level: 1});
    expect(screen.queryByRole('region', {name: /^Patient: /})).not.toBeInTheDocument();
  });

  it("says why nothing can be changed while the patient's record is restricted", async () => {
    const reason = "This patient's record is restricted, so it can't be changed until an admin lifts the restriction.";
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({...pathway, readOnlyReason: reason})});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    // A short label, read in full as an alert, with the whole reason over the page.
    expect(await findAlert(reason)).toBeInTheDocument();
    expect(within(await openAlertDetails(user, 'Read-only pathway')).getByText(reason)).toBeInTheDocument();
  });

  it("shows the patient's banners, and still offers blocking and cancelling while the record is restricted", async () => {
    mockApi({
      [`GET /api/pathways/patient-pathways/${id}`]: () =>
        json({
          ...pathway,
          readOnlyReason: 'Restricted.',
          allowedStatusChanges: ['Blocked', 'Cancelled'],
          canWorkOnStages: false,
          canEditDetails: false,
          patientFlags: {
            isRestricted: true,
            restrictionReason: 'Synthetic accuracy check',
            isDisputed: true,
            disputeReason: 'Synthetic allergy',
            isAnonymised: false,
          },
        }),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await findAlert(/Restricted record: .* can be put on hold or cancelled, with a reason/)).toHaveTextContent('Reason: Synthetic accuracy check');
    expect(within(await openAlertDetails(user, 'Disputed record')).getByText("What's disputed: Synthetic allergy")).toBeInTheDocument();
    // The patient's banner says it all: the bare reason isn't repeated.
    expect(screen.queryByText('Restricted.')).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Put on Hold'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Cancel Pathway'})).toBeInTheDocument();
  });

  it('shows the pathway, its details and its stages', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway)});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Colonoscopy', level: 1})).toBeInTheDocument();
    expect(screen.getByText('Diagnostic procedure')).toBeInTheDocument();
    expect(screen.getByText('On hold')).toBeInTheDocument();
    expect(screen.getByText('Was in progress before it was put on hold')).toBeInTheDocument();
    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getByText('1 of 2')).toBeInTheDocument();
    expect(screen.getByText('a.user')).toBeInTheDocument();
    expect(screen.getByText('Bowel preparation pack posted.')).toBeInTheDocument();
    // In the Notes section, with who wrote it, and Add Note (only your own notes can be edited).
    expect(screen.getByText(/^a\.user, /)).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Add Note'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/patient-1');
    expect(screen.getByRole('link', {name: 'View Patient'})).toHaveAttribute('href', '/patients/patient-1');
    expect(screen.getByRole('button', {name: /^1\. Consultation/})).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByRole('button', {name: /^2\. Procedure/})).toHaveAttribute('aria-expanded', 'false');
  });

  it('starts with Details (Patient info and Assigned staff open) and the Current stage open, beside Stages', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway)});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Current stage', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Hide current stage'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Hide notes'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Patient info'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('button', {name: 'Assigned staff'})).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByRole('link', {name: 'Test Patient'})).toHaveAttribute('href', '/patients/patient-1');
    // The current stage's title shows twice: in its section, and as its (closed) header in the stage list.
    expect(screen.getAllByText('2. Procedure')).toHaveLength(2);

    await user.click(screen.getByRole('button', {name: 'Patient info'}));

    expect(screen.getByRole('button', {name: 'Patient info'})).toHaveAttribute('aria-expanded', 'false');
  });

  it('folds the notes away and opens them again', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json(pathway)});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Hide notes'}));

    const show = await screen.findByRole('button', {name: /notes/i, expanded: false});
    expect(screen.queryByRole('button', {name: 'Add Note'})).not.toBeInTheDocument();
    await user.click(show);
    expect(await screen.findByRole('button', {name: 'Add Note'})).toBeInTheDocument();
  });

  it('says when every stage is finished', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({...pathway, currentStageId: null})});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByText('Every stage is finished.')).toBeInTheDocument();
  });

  it('shows a dash for the template when it has no name', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({...pathway, templateName: ''})});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByText('—')).toBeInTheDocument();
  });

  it('says when nobody is assigned', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({...pathway, assignments: []})});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByText('Nobody is assigned yet.')).toBeInTheDocument();
  });

  it('says when the pathway does not exist', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({title: 'Not found'}, 404)});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await findAlert('Pathway not found. It may have been deleted.');
  });

  it('says when the pathway cannot be loaded', async () => {
    mockApi({[`GET /api/pathways/patient-pathways/${id}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await findAlert('The pathway could not be loaded. Please try again.');
  });
});

// An in-progress pathway being worked on: stage 1 finished, stage 2 current and editable.
const PATH = `/api/pathways/patient-pathways/${id}`;
const working = (overrides: Record<string, unknown> = {}) => ({
  ...pathway,
  status: 'InProgress',
  blockedFromStatus: null,
  allowedStatusChanges: ['Blocked', 'Cancelled'],
  canWorkOnStages: true,
  assignments: [{userId: 'u1', username: 'test-user', assignedByUserId: 'u2', assignedByUsername: 'admin', assignedAtUtc: '2026-11-01T11:00:00Z'}],
  stages: [
    {...pathway.stages[0], canUnlock: true},
    {...pathway.stages[1], state: 'InProgress', notes: 'Prep done.', instructions: 'Confirm consent first.', canEdit: true},
  ],
  ...overrides,
});
const bodyOf = async (fetch: ReturnType<typeof mockApi>, call: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => `${r.method} ${new URL(r.url).pathname}` === call);
  return request ? request.clone().json() : undefined;
};

describe('PathwayPage: working on it', () => {
  it('saves the current stage notes and shows the saved pathway', async () => {
    withAnnouncements();
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working({lastUpdated: '2026-11-05T09:00:00Z'})),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    const box = await screen.findByRole('textbox', {name: 'Notes'});
    await typeRichText(box, ' Consent signed.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    await waitFor(() => expect(calls(fetch)).toContain(`PUT ${PATH}/stages/s2/notes`));
    expect(await bodyOf(fetch, `PUT ${PATH}/stages/s2/notes`)).toEqual({notes: 'Prep done. Consent signed.'});
    expect(openDialog()).toBeNull();
    await expectAnnounced('Notes saved');
  });

  it('finishes the current stage after confirming, saving unsaved notes first', async () => {
    withAnnouncements();
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working()),
      [`POST ${PATH}/stages/s2/finish`]: () => json(working()),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' All done.');
    await user.click(screen.getByRole('button', {name: 'Finish Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Finish Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s2/finish`));
    const writes = calls(fetch).filter((c) => !c.startsWith('GET'));
    expect(writes).toEqual([`PUT ${PATH}/stages/s2/notes`, `POST ${PATH}/stages/s2/finish`]);
    await expectAnnounced(/finished\. Next stage: /);
  });

  it('puts the pathway on hold with a reason', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/status`]: () => json(working({status: 'Blocked'})),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Put on Hold'}));
    const dialog = openDialog()!;
    await user.type(within(dialog).getByRole('textbox', {name: /reason/i}), 'Waiting for bloods.');
    await user.click(within(dialog).getByRole('button', {name: 'Put on Hold'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/status`));
    expect(await bodyOf(fetch, `POST ${PATH}/status`)).toEqual({status: 'Blocked', reason: 'Waiting for bloods.'});
  });

  it("asks once per visit before changing a pathway that isn't assigned to you", async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working({assignments: []})),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working({assignments: []})),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' More.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    expect(within(openDialog()!).getByText(NOT_ASSIGNED_MESSAGE)).toBeInTheDocument();
    expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toEqual([]);
    await user.click(within(openDialog()!).getByRole('button', {name: 'Continue'}));
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toHaveLength(1));

    // The second change in the same visit goes straight through.
    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' Even more.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));
    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toHaveLength(2));
    expect(openDialog()).toBeNull();
  });

  it("never asks an admin whether it's assigned to them", async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working({assignments: []})),
      [`PUT ${PATH}/stages/s2/notes`]: () => json(working({assignments: []})),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: adminUser});

    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' More.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));

    await waitFor(() => expect(calls(fetch).filter((c) => c.startsWith('PUT'))).toHaveLength(1));
    expect(openDialog()).toBeNull();
  });

  it("asks only for a status change's own confirmation, not whether it's assigned to you", async () => {
    mockApi({[`GET ${PATH}`]: () => json(working({assignments: []}))});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Put on Hold'}));

    expect(within(openDialog()!).queryByText(NOT_ASSIGNED_MESSAGE)).not.toBeInTheDocument();
    expect(within(openDialog()!).getByRole('button', {name: 'Put on Hold'})).toBeInTheDocument();
  });

  it('does not ask when you back out, and makes no change', async () => {
    const fetch = mockApi({[`GET ${PATH}`]: () => json(working({assignments: []}))});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Put on Hold'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch).filter((c) => !c.startsWith('GET'))).toEqual([]);
  });

  it("shows the API's reason when a change is refused, and doesn't announce it as finished", async () => {
    withAnnouncements();
    mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/stages/s2/finish`]: () => json({title: "The 'Procedure' stage is finished and locked."}, 409),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Finish Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Finish Stage'}));

    await findAlert("The 'Procedure' stage is finished and locked.");
    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(screen.getByTestId('announcer')).not.toHaveTextContent(/finished/);
  });

  it('keeps assigning and unlocking for admins', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByRole('textbox', {name: 'Notes'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Assign Staff'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^unassign/i})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Unlock Stage'})).not.toBeInTheDocument();
  });

  it('lets a manager (AssignPathways) assign staff, offering only those whose roles let them work on pathways', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      'GET /api/calendar/staff': () => json([{userId: 'u1', username: 'test-user'}, {userId: 'u3', username: 'k.other'}]),
      [`POST ${PATH}/assignments/u3`]: () => json(working()),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: managerUser});

    await user.click(await screen.findByRole('button', {name: 'Assign Staff'}));
    const dialog = openDialog()!;
    // Only staff not already assigned are offered.
    await user.click(await within(dialog).findByRole('checkbox', {name: 'k.other'}));
    expect(within(dialog).queryByRole('checkbox', {name: 'test-user'})).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', {name: 'Assign'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/assignments/u3`));
    // Holders of WorkPathways only, from the staff list (a manager can't read the Users page's).
    expect(calls(fetch)).toContain('GET /api/calendar/staff?permission=WorkPathways');
  });

  it("shows a manager what their roles allow: assigning, but no work on the stages, no booking, no deleting", async () => {
    // The API's flags say the stages can't be worked on (no WorkPathways): the page says why, by roles, not status.
    mockApi({[`GET ${PATH}`]: () => json(working({canWorkOnStages: false, allowedStatusChanges: [], canChangeAssignments: true, canBeDeleted: false}))});
    renderWithApp(<PathwayPage id={id} />, {user: managerUser});

    expect(await screen.findByText("Your roles don't allow working on pathways, so this is read only.")).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Assign Staff'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book Appointment'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete Pathway'})).not.toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Audit Log'})).not.toBeInTheDocument();
  });

  it("offers a nurse no Assign Staff or Unlock Stage, whatever the pathway's flags say", async () => {
    mockApi({[`GET ${PATH}`]: () => json(working({canChangeAssignments: true}))});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: nurseUser});

    expect(await screen.findByRole('textbox', {name: 'Notes'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Book Appointment'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Assign Staff'})).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: /^1\. Consultation/}));
    expect(screen.queryByRole('button', {name: 'Unlock Stage'})).not.toBeInTheDocument();
  });

  it('lets an admin unlock a finished stage', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`POST ${PATH}/stages/s1/unlock`]: () => json(working()),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /^1\. Consultation/}));
    await user.click(screen.getByRole('button', {name: 'Unlock Stage'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Unlock Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s1/unlock`));
  });

  it('starts a stage not started yet', async () => {
    const notStarted = working({stages: [working().stages[0], {...working().stages[1], state: 'NotStarted'}]});
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(notStarted),
      [`POST ${PATH}/stages/s2/start`]: () => json(working()),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Start Stage'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s2/start`));
  });

  it("makes no change when the not-assigned question is answered with Cancel", async () => {
    const fetch = mockApi({[`GET ${PATH}`]: () => json(working({assignments: []}))});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await typeRichText(await screen.findByRole('textbox', {name: 'Notes'}), ' More.');
    await user.click(screen.getByRole('button', {name: 'Save Notes'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    expect(openDialog()).toBeNull();
    expect(calls(fetch).filter((c) => !c.startsWith('GET'))).toEqual([]);
  });

  it('says why staff could not be assigned', async () => {
    mockApi({
      [`GET ${PATH}`]: () => json(working()),
      'GET /api/calendar/staff': () => json([{userId: 'u3', username: 'k.other'}]),
      [`POST ${PATH}/assignments/u3`]: () => json({title: 'k.other can no longer be assigned.'}, 409),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Assign Staff'}));
    await user.click(await within(openDialog()!).findByRole('checkbox', {name: 'k.other'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Assign'}));

    expect(await findAlert('k.other can no longer be assigned.')).toBeInTheDocument();
    expect(openDialog()).toBeNull();
  });

  it('assigns nobody when Assign Staff is cancelled, and starts afresh next time', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      'GET /api/calendar/staff': () => json([{userId: 'u3', username: 'k.other'}]),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Assign Staff'}));
    await user.click(await within(openDialog()!).findByRole('checkbox', {name: 'k.other'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));
    expect(openDialog()).toBeNull();

    await user.click(screen.getByRole('button', {name: 'Assign Staff'}));
    expect(await within(openDialog()!).findByRole('checkbox', {name: 'k.other'})).not.toBeChecked();
    expect(calls(fetch).filter((c) => !c.startsWith('GET'))).toEqual([]);
  });

  it('folds the details, and opens and closes a stage in the list', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Hide details'}));
    expect(screen.getByRole('button', {name: 'Show details'})).toHaveAttribute('aria-expanded', 'false');

    const stage = screen.getByRole('button', {name: /^1\. Consultation/});
    await user.click(stage);
    expect(stage).toHaveAttribute('aria-expanded', 'true');
    await user.click(stage);
    expect(stage).toHaveAttribute('aria-expanded', 'false');
  });

  it('lets an admin unassign staff', async () => {
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working()),
      [`DELETE ${PATH}/assignments/u1`]: () => json(working({assignments: []})),
    });
    const {user} = renderWithApp(<PathwayPage id={id} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Unassign test-user'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Unassign'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE ${PATH}/assignments/u1`));
  });

  it('explains why stages are read-only while on hold', async () => {
    mockApi({[`GET ${PATH}`]: () => json(pathway)});
    renderWithApp(<PathwayPage id={id} />, {user: staffUser});

    expect(await screen.findByText("Stages can't be worked on while the pathway is on hold.")).toBeInTheDocument();
    expect(screen.queryByRole('textbox', {name: 'Notes'})).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Take off Hold'})).toBeInTheDocument();
  });
});

describe('PathwayPage instructions', () => {
  // The Current stage section's comes first; the stage list has the same stage's, folded away with it.
  const instructionsToggle = () => screen.getAllByRole('button', {name: 'Instructions'})[0];

  it.each([
    ['full', false],
    ['half', true],
  ])('starts with the instructions folded in %s view, opening when their heading is clicked', async (_, isNarrow) => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    const {user} = renderWithApp(<PathwayView id={id} isNarrow={isNarrow} />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Current stage', level: 2});
    expect(instructionsToggle()).toHaveAttribute('aria-expanded', 'false');

    await user.click(instructionsToggle());

    expect(instructionsToggle()).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getAllByText('Confirm consent first.')[0]).toBeVisible();
  });
});

describe('PathwayPage on a narrow window', () => {
  function renderNarrow() {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    return renderWithApp(<PathwayView id={id} isNarrow />, {user: staffUser});
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
    expect(richText(screen.getByRole('textbox', {name: 'Notes'}))).toBe('Prep done.');
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

    expect(richText(screen.getByRole('textbox', {name: 'Notes'}))).toBe('Prep done.');
  });

  it('keeps what is open and folded when the window switches between half and full view', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    function Switchable() {
      const [isNarrow, setIsNarrow] = useState(true);
      return (
        <>
          <button onClick={() => setIsNarrow(!isNarrow)}>Switch layout</button>
          <PathwayView id={id} isNarrow={isNarrow} />
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

describe('PathwayPage details and deleting', () => {
  it('links to Edit Details while the details can be edited', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});

    expect(await screen.findByRole('link', {name: 'Edit Details'})).toHaveAttribute('href', `/pathways/${id}/edit`);
  });

  it("links admins, and only admins, to the pathway's audit log", async () => {
    mockApi({[`GET ${PATH}`]: () => json(working())});
    const {unmount} = renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: adminUser});
    expect(await screen.findByRole('link', {name: 'Audit Log'})).toHaveAttribute('href', `/admin/audit?pathwayId=${id}`);
    unmount();

    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});
    await screen.findByRole('link', {name: 'Edit Details'});
    expect(screen.queryByRole('link', {name: 'Audit Log'})).not.toBeInTheDocument();
  });

  it('has no Edit Details once the details are closed to editing', async () => {
    mockApi({[`GET ${PATH}`]: () => json(working({status: 'Cancelled', canEditDetails: false}))});
    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Details', level: 2});
    expect(screen.queryByRole('link', {name: 'Edit Details'})).not.toBeInTheDocument();
  });

  it("offers Delete only to admins, and only when the API allows it", async () => {
    mockApi({[`GET ${PATH}`]: () => json(working({status: 'Cancelled', canBeDeleted: true}))});
    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});
    await screen.findByRole('heading', {name: 'Details', level: 2});
    expect(screen.queryByRole('button', {name: 'Delete Pathway'})).not.toBeInTheDocument();
  });

  it("deletes a cancelled pathway after a confirmation, then shows the patient's pathways", async () => {
    withAnnouncements();
    const fetch = mockApi({
      [`GET ${PATH}`]: () => json(working({status: 'Cancelled', canBeDeleted: true})),
      [`DELETE ${PATH}`]: () => new Response(null, {status: 204}),
    });
    const {user, router} = renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Pathway'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText('Colonoscopy')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Delete Pathway'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients/patient-1/pathways'));
    expect(calls(fetch)).toContain(`DELETE ${PATH}`);
    await expectAnnounced('Pathway deleted');
  });

  it('explains a failed delete', async () => {
    mockApi({
      [`GET ${PATH}`]: () => json(working({status: 'Cancelled', canBeDeleted: true})),
      [`DELETE ${PATH}`]: () => json({title: 'Only cancelled or archived pathways can be deleted.'}, 409),
    });
    const {user} = renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: 'Delete Pathway'}));
    await user.click(within(openDialog()!).getByRole('button', {name: 'Delete Pathway'}));

    expect(await findAlert('Only cancelled or archived pathways can be deleted.')).toBeInTheDocument();
  });
});

describe('PathwayPage: visits and appointments', () => {
  const APPOINTMENTS = '/api/appointments/search';
  /** Stage 2 involves a visit (Required unless said otherwise). */
  const visiting = (visitRequirement = 'Required', overrides: Record<string, unknown> = {}) => {
    const base = working();
    return {...base, stages: [base.stages[0], {...base.stages[1], visitRequirement}], ...overrides};
  };
  const forStage = {pathwayId: id, pathwayTitle: 'Colonoscopy', stageId: 's2', stageName: 'Procedure'};
  const requested = testRequested({id: 'requested', ...forStage});
  // 23 November 2026: UK time is UTC.
  const booked = testAppointment({id: 'booked', date: '2026-11-23', start: '10:00', end: '10:30', ...forStage});
  const attended = testAppointment({id: 'attended', status: 'Attended', date: '2026-11-16', ...forStage});

  function renderVisit(
    pathway: ReturnType<typeof working>,
    appointments: unknown[],
    user = staffUser,
    extra: Parameters<typeof mockApi>[0] = {},
  ) {
    const fetch = mockApi({[`GET ${PATH}`]: () => json(pathway), [`GET ${APPOINTMENTS}`]: () => json(pageOf(appointments)), ...extra});
    return {fetch, ...renderWithApp(<PathwayPage id={id} />, {user})};
  }

  it("asks for the pathway's appointments, and lists them in the details, each opening on the calendar", async () => {
    const {fetch} = renderVisit(visiting(), [booked, requested]);

    const list = within(await screen.findByRole('list', {name: 'Appointments'}));
    expect(list.getByRole('link', {name: 'Mon 23 Nov 2026, 10:00 to 10:30 with test-user'})).toHaveAttribute('href', '/calendar?appointment=booked');
    expect(list.getByText('Requires appointment')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${APPOINTMENTS}?PathwayId=${id}&PageSize=100`);
  });

  it('says when there are none', async () => {
    renderVisit(working(), []);
    expect(await screen.findByText('No appointments yet.')).toBeInTheDocument();
  });

  it("says when the appointments, and so the stage's visit, could not be loaded", async () => {
    renderVisit(visiting(), [], staffUser, {[`GET ${APPOINTMENTS}`]: () => json({title: 'Down'}, 500)});

    expect(await findAlert('The appointments could not be loaded. Please try again.')).toBeInTheDocument();
    expect(await findAlert("The stage's visit could not be loaded. Please try again.")).toBeInTheDocument();
  });

  it('shows each stage’s visit in the stage list', async () => {
    renderVisit(visiting(), [booked]);

    expect(await screen.findByRole('button', {name: /^2\. Procedure.*Visit required.*Booked/})).toBeInTheDocument();
  });

  it("won't let staff finish a stage that requires a visit until one is attended, and books it from the stage", async () => {
    const {user} = renderVisit(visiting(), [requested]);

    const visit = within(await screen.findByRole('group', {name: 'Visit'}));
    expect(visit.getByText('Requires appointment')).toBeInTheDocument();
    expect(screen.getAllByText('Needs an attended visit first').length).toBeGreaterThan(0);
    expect(screen.getByRole('button', {name: 'Finish Stage'})).toHaveAttribute('aria-disabled', 'true');

    await user.click(visit.getByRole('button', {name: 'Book Visit'}));
    expect(within(openDialog()!).getByText('Book Visit')).toBeInTheDocument();
  });

  it('records a past visit from the stage, for this patient, pathway and stage, which it shows', async () => {
    const {user} = renderVisit(visiting(), [{...requested, notes: 'Needs an interpreter.'}]);

    const visit = within(await screen.findByRole('group', {name: 'Visit'}));
    await user.click(visit.getByRole('button', {name: 'Record Past Visit'}));

    const dialog = within(openDialog()!);
    expect(dialog.getByRole('heading', {name: 'Record Past Visit'})).toBeInTheDocument();
    expect(dialog.getByText(/: Procedure$/)).toBeInTheDocument();
    // The patient and what it's for are fixed, so there's no picker.
    expect(dialog.queryByRole('combobox', {name: /Patient/})).not.toBeInTheDocument();
    // The visit waiting in Requires Appointment is the one recorded, and its notes are kept: they're shown.
    expect(dialog.getByText('Its notes, which are kept')).toBeInTheDocument();
    expect(dialog.getByText('Needs an interpreter.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('lets an admin finish a required stage without a visit, giving a reason', async () => {
    withAnnouncements();
    const {user, fetch} = renderVisit(visiting(), [requested], adminUser, {[`POST ${PATH}/stages/s2/finish`]: () => json(working())});

    await user.click(await screen.findByRole('button', {name: 'Finish Stage'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText('Finish without a visit?')).toBeInTheDocument();
    expect(dialog.getByText(/Procedure requires a visit, and none is marked Attended/)).toBeInTheDocument();
    expect(dialog.getByText('Its visit comes off Requires Appointment.')).toBeInTheDocument();
    expect(dialog.getByRole('button', {name: 'Finish Without Visit'})).toHaveAttribute('aria-disabled', 'true');
    await user.type(dialog.getByRole('textbox', {name: /Reason/}), ' Seen at another clinic ');
    await user.click(dialog.getByRole('button', {name: 'Finish Without Visit'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s2/finish`));
    expect(await bodyOf(fetch, `POST ${PATH}/stages/s2/finish`)).toEqual({withoutVisit: true, reason: 'Seen at another clinic'});
    await expectAnnounced(/finished\. Next stage: /);
  });

  it('asks anyone to confirm finishing a stage that suggests a visit without one, saying a booked one stays', async () => {
    const {user, fetch} = renderVisit(visiting('Suggested'), [booked], staffUser, {[`POST ${PATH}/stages/s2/finish`]: () => json(working())});

    await user.click(await screen.findByRole('button', {name: 'Finish Stage'}));
    const dialog = within(openDialog()!);
    expect(dialog.getByText('Procedure suggests a visit, and none is marked Attended. Finish it anyway?')).toBeInTheDocument();
    expect(dialog.getByText(/stays booked: cancel it on the calendar/)).toBeInTheDocument();
    expect(dialog.queryByRole('textbox', {name: /Reason/})).not.toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Finish Without Visit'}));

    await waitFor(() => expect(calls(fetch)).toContain(`POST ${PATH}/stages/s2/finish`));
    expect(await bodyOf(fetch, `POST ${PATH}/stages/s2/finish`)).toEqual({withoutVisit: true});
  });

  it('finishes as usual once the visit is attended', async () => {
    const {user} = renderVisit(visiting(), [attended]);

    await user.click(await screen.findByRole('button', {name: 'Finish Stage'}));

    expect(within(openDialog()!).getByText('Finish Procedure?')).toBeInTheDocument();
  });

  it('records that the patient came to a visit that has started, and reschedules one', async () => {
    withAnnouncements();
    const started = {...booked, canRecordOutcome: true};
    const {user, fetch} = renderVisit(visiting(), [started], staffUser, {
      [`POST /api/appointments/booked/outcome`]: () => json({...started, status: 'Attended'}),
    });

    const visit = within(await screen.findByRole('group', {name: 'Visit'}));
    await user.click(visit.getByRole('button', {name: 'Attended'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/appointments/booked/outcome'));
    expect(await bodyOf(fetch, 'POST /api/appointments/booked/outcome')).toEqual({outcome: 'Attended'});
    await expectAnnounced('Visit marked Attended');

    await user.click(visit.getByRole('button', {name: 'Reschedule'}));
    expect(within(openDialog()!).getByText('Reschedule Appointment')).toBeInTheDocument();
  });

  it('says why an outcome could not be recorded', async () => {
    withAnnouncements();
    const started = {...booked, canRecordOutcome: true};
    const {user} = renderVisit(visiting(), [started], staffUser, {
      [`POST /api/appointments/booked/outcome`]: () => json({title: 'Too early.'}, 409),
    });

    await user.click(within(await screen.findByRole('group', {name: 'Visit'})).getByRole('button', {name: 'Did Not Attend'}));

    expect(await findAlert('Too early.')).toBeInTheDocument();
  });

  it('books an appointment for the pathway from its Appointments, and closes the booking', async () => {
    const {user} = renderVisit(working(), []);

    await user.click(await screen.findByRole('button', {name: 'Book Appointment'}));
    expect(within(openDialog()!).getByText('Book Appointment')).toBeInTheDocument();
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('folds the Appointments, and says while they load', async () => {
    const {user} = renderVisit(working(), [], staffUser, {[`GET ${APPOINTMENTS}`]: () => new Promise<Response>(() => {})});

    expect(await screen.findByText('Loading appointments...')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Appointments'}));

    expect(screen.getByRole('button', {name: 'Appointments'})).toHaveAttribute('aria-expanded', 'false');
  });

  it('points out appointments still booked for a cancelled pathway, and books nothing more for it', async () => {
    renderVisit(working({status: 'Cancelled', allowedStatusChanges: [], canWorkOnStages: false}), [booked, {...booked, id: 'booked-2'}]);

    expect(
      await screen.findByText('The pathway is cancelled, but 2 appointments are still booked. Tell the patient, then cancel them on the calendar.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Book Appointment'})).not.toBeInTheDocument();
  });

  it("names a cancelled pathway's one booked appointment, and shows the visit beside a stage that can't be worked on", async () => {
    const blocked = visiting('Required', {status: 'Cancelled', allowedStatusChanges: [], canWorkOnStages: false});
    renderVisit({...blocked, stages: [blocked.stages[0], {...blocked.stages[1], canEdit: false}]}, [booked]);

    expect(await screen.findByText(/but an appointment is still booked\. Tell the patient, then cancel it/)).toBeInTheDocument();
    expect(screen.getAllByText('Visit required').length).toBeGreaterThan(1);
  });
});

describe('finishedMessage', () => {
  const consent = {id: 's1', name: 'Consent'};
  it('names the stage finished, then the next one or that every stage is', () => {
    expect(finishedMessage(consent, {currentStageId: 's2', stages: [consent, {id: 's2', name: 'Assessment'}]})).toBe(
      'Consent finished. Next stage: Assessment',
    );
    expect(finishedMessage(consent, {currentStageId: null, stages: [consent]})).toBe('Consent finished. Every stage is finished');
  });
});

describe('PathwayPage: files', () => {
  const ATTACHMENTS = '/api/attachments';
  const file = (fileName: string, fields: Record<string, unknown> = {}) => ({
    id: `file-${fileName}`,
    patientId: 'patient-1',
    attachedTo: 'Pathway',
    pathwayId: id,
    pathwayTitle: 'Colonoscopy',
    fileName,
    contentType: 'application/pdf',
    typeName: 'PDF',
    sizeBytes: 2048,
    scanStatus: 'NotScanned',
    hasThumbnail: false,
    uploadedByUsername: 'test-user',
    uploadedAtUtc: '2026-11-03T14:05:00Z',
    canDelete: true,
    isDeleted: false,
    ...fields,
  });
  /** The pathway, and its files and its stages' files by what's asked for. */
  const mockFiles = (pathwayFields: Record<string, unknown> = {}) =>
    mockApi({
      [`GET ${PATH}`]: () => json(working(pathwayFields)),
      [`GET ${ATTACHMENTS}`]: (request) => {
        const query = new URL(request.url).searchParams;
        const items = query.has('StageId')
          ? [file(`stage-${query.get('StageId')}.pdf`, {attachedTo: 'Stage', stageId: query.get('StageId'), stageName: 'Procedure'})]
          : [file('referral.pdf')];
        return json({items, totalCount: items.length, maxFileBytes: 20 * 1024 * 1024, allowedExtensions: ['.pdf']});
      },
    });

  it("shows the pathway's own files in a section of their own, open beside the work, where files can be added", async () => {
    const fetch = mockFiles();
    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Hide files'})).toHaveAttribute('aria-expanded', 'true');
    expect(calls(fetch)).toContain(`GET ${ATTACHMENTS}?PathwayId=${id}`);
    expect(screen.getByRole('button', {name: 'Add files'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Delete referral.pdf'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Files', level: 2})).toBeInTheDocument();
  });

  it("hides Add files, and staff's Delete, once the pathway is cancelled or archived", async () => {
    mockFiles({status: 'Cancelled', canEditDetails: false, canWorkOnStages: false});
    renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add files'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete referral.pdf'})).not.toBeInTheDocument();
  });

  it('folds the files away in a narrow window, fetching them when opened', async () => {
    mockFiles();
    const {user} = renderWithApp(<PathwayView id={id} isNarrow />, {user: staffUser});

    await user.click(await screen.findByRole('button', {name: 'Show files'}));

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
  });

  it("folds each stage's files under its notes; they can be added only while the stage can be edited", async () => {
    const fetch = mockFiles();
    const {user} = renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: staffUser});
    await screen.findByText('referral.pdf');
    expect(calls(fetch).some((call) => call.includes('StageId'))).toBe(false);

    // The current stage, in its section (the first of its "Files"): editable, so files can be added.
    await user.click(screen.getAllByRole('button', {name: 'Files'})[0]);

    expect(await screen.findAllByText('stage-s2.pdf')).not.toHaveLength(0);
    expect(calls(fetch)).toContain(`GET ${ATTACHMENTS}?StageId=s2`);
    // The pathway's, and the stage's: in its section, and in the list (whose copy folds with it).
    expect(screen.getAllByRole('button', {name: 'Add files'})).toHaveLength(3);

    // The finished, locked stage, in the list (its "Files" comes after the current stage's): its files, but nothing
    // to add.
    await user.click(screen.getByRole('button', {name: /^1\. Consultation/}));
    await user.click(screen.getAllByRole('button', {name: 'Files'})[1]);

    expect(await screen.findByText('stage-s1.pdf')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${ATTACHMENTS}?StageId=s1`);
    expect(screen.getAllByRole('button', {name: 'Add files'})).toHaveLength(3);
    expect(screen.queryByRole('button', {name: 'Delete stage-s1.pdf'})).not.toBeInTheDocument();
  });

  it("offers nothing to add or delete, to admins too, while the patient's record is restricted", async () => {
    const reason = "This patient's record is restricted, so it can't be changed until an admin lifts the restriction.";
    mockFiles({readOnlyReason: reason, canEditDetails: false, canWorkOnStages: false, stages: working().stages.map((stage) => ({...stage, canEdit: false}))});
    const {user} = renderWithApp(<PathwayView id={id} isNarrow={false} />, {user: adminUser});

    expect(await screen.findByText('referral.pdf')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Download referral.pdf'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add files'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete referral.pdf'})).not.toBeInTheDocument();

    await user.click(screen.getAllByRole('button', {name: 'Files'})[0]);
    expect(await screen.findAllByText('stage-s2.pdf')).not.toHaveLength(0);
    expect(screen.queryByRole('button', {name: /^Delete stage-s2/})).not.toBeInTheDocument();
  });
});
