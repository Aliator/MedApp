import {screen, waitFor} from '@testing-library/react';
import type {PatientExport} from '../../api/patients';
import {adminUser, calls, findAlert, json, mockApi, renderWithApp} from '../../test/render';
import {
  ANOTHER_PATIENTS_DETAILS,
  appointmentChangeLabel,
  appointmentHistoryWords,
  deletedNoteText,
  deletionRequestOutcome,
  describeAppointmentChange,
  describeExportedAppointment,
  describeReferralChange,
  exportFileName,
  PatientExportPage,
  referralChangeLabel,
  referralName,
} from './PatientExportPage';

const id = 'd184a7b8-2585-4437-bcd4-e1700be38211';
const pathwayId = 'e184a7b8-2585-4437-bcd4-e1700be38212';
const appointmentId = 'f184a7b8-2585-4437-bcd4-e1700be38213';
const EXPORT = `/api/patients/${id}/export`;

const data: PatientExport = {
  exportedAtUtc: '2026-11-16T09:30:00Z',
  exportedBy: 'test-admin',
  patient: {
    id,
    firstName: 'Oriel',
    lastName: 'Penhallow',
    dateOfBirth: '1951-03-14',
    email: 'oriel.penhallow@example.com',
    nhsNumber: '9990000034',
    createdAt: '2020-01-01T09:00:00Z',
    lastUpdated: '2026-01-01T09:00:00Z',
    restrictedAtUtc: '2026-11-01T09:00:00Z',
    restrictionReason: 'Synthetic accuracy check',
    disputedAtUtc: '2026-11-02T09:00:00Z',
    disputeReason: 'Synthetic date of birth',
    anonymisedAtUtc: null,
    retentionKeptUntil: '2027-11-16',
    retentionKeptReason: 'Synthetic legal hold',
  },
  pathways: [
    {
      id: pathwayId,
      title: 'Synthetic knee review',
      templateName: 'Knee review',
      status: 'Completed',
      priority: 'High',
      dueDate: '2026-10-01',
      createdAt: '2026-09-01T09:00:00Z',
      lastUpdated: '2026-10-02T09:00:00Z',
      notes: [
        {
          id: 'note-1',
          author: 'k.ashford',
          text: 'Synthetic pathway note',
          createdAtUtc: '2026-09-01T10:00:00Z',
          lastEditedAtUtc: '2026-09-02T10:00:00Z',
          version: 2,
        },
      ],
      assignedTo: ['k.ashford'],
      stages: [
        {
          order: 1,
          name: 'Consent',
          notes: 'Synthetic stage note',
          startedAtUtc: '2026-09-02T09:00:00Z',
          startedBy: 'k.ashford',
          finishedAtUtc: '2026-09-03T09:00:00Z',
          finishedBy: 'l.northcott',
        },
        {order: 2, name: 'Scan', notes: ''},
      ],
    },
  ],
  noteHistory: [
    {noteId: 'note-1', pathwayId, version: 1, text: 'Synthetic first version', username: 'k.ashford', atUtc: '2026-09-01T10:00:00Z'},
    {noteId: 'note-2', pathwayId: 'deleted-pathway', version: 1, text: 'Synthetic deleted note', username: 'Unknown', atUtc: '2026-08-01T10:00:00Z'},
  ],
  pathwayHistory: [
    {
      pathwayId,
      pathwayTitle: 'Synthetic knee review',
      stageName: 'Consent',
      action: 'StageFinished',
      username: 'l.northcott',
      atUtc: '2026-09-03T09:00:00Z',
      details: '{}',
    },
  ],
  appointments: [
    {
      id: appointmentId,
      pathwayId,
      stageName: 'Scan',
      status: 'Cancelled',
      staffUsername: 'k.ashford',
      startUtc: '2026-09-21T09:00:00Z',
      endUtc: '2026-09-21T09:30:00Z',
      notes: 'Synthetic appointment note',
      cancelReason: 'Synthetic cancellation reason',
      isAutomatic: false,
      createdAtUtc: '2026-09-10T09:00:00Z',
      createdBy: 'l.northcott',
      bookedAtUtc: '2026-09-11T09:00:00Z',
      bookedBy: 'k.ashford',
      outcomeAtUtc: null,
      outcomeBy: null,
    },
  ],
  appointmentHistory: [
    {
      atUtc: '2026-09-12T09:00:00Z',
      action: 'Cancelled',
      username: 'k.ashford',
      appointmentId,
      pathwayId,
      pathwayTitle: 'Synthetic knee review',
      stageName: 'Scan',
      staffUsername: 'k.ashford',
      startUtc: '2026-09-21T09:00:00Z',
      endUtc: '2026-09-21T09:30:00Z',
      details: JSON.stringify({reason: 'Synthetic cancellation reason', automatic: false}),
    },
  ],
  accessLog: [
    {action: 'PatientExported', username: 'test-admin', atUtc: '2026-11-16T09:30:00Z', details: '{}'},
    {action: 'PatientRestricted', username: 'test-admin', atUtc: '2026-11-01T09:00:00Z', details: JSON.stringify({reason: 'Synthetic accuracy check'})},
  ],
  notifications: [
    {kind: 'PathwayAssigned', username: 'k.ashford', title: 'Synthetic knee review', message: 'test-admin assigned you to this pathway.', createdAtUtc: '2026-09-01T09:00:00Z'},
    {
      kind: 'AppointmentCancelled',
      username: 'k.ashford',
      title: 'Appointment cancelled',
      message: 'l.northcott cancelled an appointment.',
      createdAtUtc: '2026-09-12T09:00:00Z',
      pathwayId,
      appointmentId,
    },
  ],
};

function renderExport(body: unknown = data, status = 200, strict = false) {
  const fetch = mockApi({[`GET ${EXPORT}`]: () => json(body, status)});
  return {fetch, ...renderWithApp(<PatientExportPage id={id} />, {user: adminUser, strict})};
}

describe('PatientExportPage', () => {
  it("lists the files' details, deleted ones marked, and says the files themselves aren't included", async () => {
    const file = {
      typeName: 'PDF',
      contentType: 'application/pdf',
      sizeBytes: 2048,
      sha256: 'a1b2c3',
      scanStatus: 'NotScanned',
      uploadedBy: 'k.ashford',
      uploadedAtUtc: '2026-09-02T09:00:00Z',
    };
    renderExport({
      ...data,
      attachments: [
        {...file, id: 'f1', attachedTo: 'Patient', fileName: 'synthetic-letter.pdf'},
        {...file, id: 'f2', attachedTo: 'Stage', pathwayId, stageName: 'Consent', fileName: 'synthetic-consent.pdf', scanStatus: 'Clean'},
        {
          ...file,
          id: 'f3',
          attachedTo: 'Pathway',
          pathwayId,
          fileName: 'synthetic-old.pdf',
          deletedAtUtc: '2026-10-01T09:00:00Z',
          deletedBy: 'test-admin',
        },
        {...file, id: 'f4', attachedTo: 'Stage', pathwayId: 'gone', stageName: null, fileName: 'synthetic-orphan.pdf', scanStatus: 'Odd'},
      ],
    });

    expect(await screen.findByText('Files (4)')).toBeInTheDocument();
    expect(screen.getByText(/The files themselves aren't included in this export\./)).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to the patient.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to Consent, in Synthetic knee review.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to Synthetic knee review.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to a stage, in a pathway.')).toBeInTheDocument();
    expect(screen.getAllByText(/^Added .* by k\.ashford$/)).toHaveLength(3);
    expect(screen.getByText(/^Added .* by k\.ashford; deleted .* by test-admin$/)).toBeInTheDocument();
    expect(screen.getByText('Deleted')).toBeInTheDocument();
    expect(screen.getAllByText('Malware scan: not scanned. SHA-256: a1b2c3')).toHaveLength(2);
    expect(screen.getByText('Malware scan: clean. SHA-256: a1b2c3')).toBeInTheDocument();
    expect(screen.getByText('Malware scan: Odd. SHA-256: a1b2c3')).toBeInTheDocument();
  });

  it('shows everything held about the patient, once fetched, with a reminder to check it before it goes out', async () => {
    const {fetch} = renderExport();

    expect(await screen.findByText('Oriel Penhallow')).toBeInTheDocument();
    expect(screen.getByText(/Exported .* by test-admin/)).toBeInTheDocument();
    expect(screen.getByText(/have a clinician check it/)).toBeInTheDocument();
    expect(screen.getByText('oriel.penhallow@example.com')).toBeInTheDocument();
    expect(screen.getByText('999 000 0034')).toBeInTheDocument();
    // In the patient's details, and in the access log's entry for the restriction.
    expect(screen.getAllByText(/Synthetic accuracy check$/)).toHaveLength(2);
    expect(screen.getByText(/Synthetic date of birth$/)).toBeInTheDocument();
    expect(screen.getByText(/Synthetic legal hold$/)).toBeInTheDocument();
    // Pathways, with their stages and notes.
    expect(screen.getByText('Pathways (1)')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Synthetic knee review', level: 3})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Notes (1)', level: 4})).toBeInTheDocument();
    expect(screen.getByText('Synthetic pathway note')).toBeInTheDocument();
    expect(screen.getByText(/^Note by k\.ashford, .*, edited /)).toBeInTheDocument();
    expect(screen.getByText('Note History (2)')).toBeInTheDocument();
    expect(screen.getByText('Synthetic first version')).toBeInTheDocument();
    expect(screen.getByText('A deleted pathway')).toBeInTheDocument();
    expect(screen.getByText(/: Note added by Unknown$/)).toBeInTheDocument();
    expect(screen.getByText('1. Consent')).toBeInTheDocument();
    expect(screen.getByText(/Started .* by k\.ashford, finished .* by l\.northcott/)).toBeInTheDocument();
    expect(screen.getByText('Synthetic stage note')).toBeInTheDocument();
    expect(screen.getByText('Not started')).toBeInTheDocument();
    expect(screen.getByText('k.ashford', {selector: '*'})).toBeInTheDocument();
    // The history, the access log and the notifications.
    expect(screen.getByText(/Stage finished by l\.northcott/)).toBeInTheDocument();
    expect(screen.getByText(/Record exported by test-admin/)).toBeInTheDocument();
    expect(screen.getByText(/Record restricted by test-admin/)).toBeInTheDocument();
    expect(screen.getAllByText(/: to k\.ashford$/)).toHaveLength(2);
    expect(screen.getByText('Synthetic knee review: test-admin assigned you to this pathway.')).toBeInTheDocument();
    // The appointments, with everything the export holds about each, and the history of changes to them.
    expect(screen.getByText('Appointments (1)')).toBeInTheDocument();
    expect(screen.getByText('Cancelled: Mon 21 Sept 2026, 10:00 to 10:30 with k.ashford')).toBeInTheDocument();
    expect(screen.getByText(/^Synthetic knee review: Scan, requested .* by l\.northcott, booked .* by k\.ashford$/)).toBeInTheDocument();
    expect(screen.getByText('Why cancelled: Synthetic cancellation reason')).toBeInTheDocument();
    expect(screen.getByText('Notes: Synthetic appointment note')).toBeInTheDocument();
    expect(screen.getByText('Appointment History (1)')).toBeInTheDocument();
    expect(screen.getByText(/Appointment cancelled by k\.ashford/)).toBeInTheDocument();
    expect(
      screen.getByText('Synthetic knee review · Scan · Mon 21 Sept 2026, 10:00 to 10:30 with k.ashford · Synthetic cancellation reason'),
    ).toBeInTheDocument();
    // A notification about an appointment says which.
    expect(screen.getByText('About the appointment: Cancelled: Mon 21 Sept 2026, 10:00 to 10:30 with k.ashford')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${EXPORT}`]);
  });

  it('says when there is nothing in a part', async () => {
    renderExport({exportedAtUtc: '2026-11-16T09:30:00Z', exportedBy: 'test-admin', patient: {id, firstName: 'Oriel'}});

    expect(await screen.findByText('No pathways.')).toBeInTheDocument();
    expect(screen.getByText('No changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('No access recorded.')).toBeInTheDocument();
    expect(screen.getByText('No notifications.')).toBeInTheDocument();
    expect(screen.getByText('No appointments.')).toBeInTheDocument();
    expect(screen.getByText('No appointment changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('No files.')).toBeInTheDocument();
    expect(screen.getByText('No requests to delete notes.')).toBeInTheDocument();
    expect(screen.getByText('No referrals.')).toBeInTheDocument();
    expect(screen.getByText('No referral changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('Not given')).toBeInTheDocument();
  });

  it('lists every referral with what the referrer sent and what was decided, its history, and its letters', async () => {
    const referralId = 'a184a7b8-2585-4437-bcd4-e1700be38214';
    renderExport({
      ...data,
      referrals: [
        {
          id: referralId,
          specialtyName: 'Dermatology',
          receivedOn: '2026-11-02',
          referrerPractice: 'Synthetic Street Surgery',
          referrerClinician: 'Dr Test',
          referrerOdsCode: 'A12345',
          requestedPriority: 'High',
          reason: 'A synthetic rash.',
          status: 'Rejected',
          registeredAtUtc: '2026-11-02T10:00:00Z',
          registeredBy: 'k.ashford',
          decidedAtUtc: '2026-11-04T09:00:00Z',
          decidedBy: 'd.clinician',
          rejectionReason: 'Duplicate',
          decisionDetail: 'Already referred.',
          redirectedTo: 'Another provider',
          pathwayId,
        },
        {id: 'b', specialtyName: 'Urology', receivedOn: null, referrerPractice: '', requestedPriority: 'Low', reason: null, status: 'AwaitingTriage', registeredBy: 'k.ashford'},
      ],
      referralHistory: [
        {atUtc: '2026-11-02T10:00:00Z', action: 'Registered', username: 'k.ashford', referralId, details: '{"specialtyName":"Dermatology","requestedPriority":"High"}'},
        {atUtc: '2026-11-03T10:00:00Z', action: 'SpecialtyChanged', username: 'k.ashford', referralId: 'unknown', details: '{}'},
      ],
      attachments: [
        {id: 'letter', attachedTo: 'Referral', referralId, fileName: 'letter.pdf', typeName: 'PDF', contentType: 'application/pdf', sizeBytes: 10, uploadedBy: 'k.ashford', uploadedAtUtc: '2026-11-02T11:00:00Z', scanStatus: 'NotScanned', sha256: 'x'},
        {id: 'lost', attachedTo: 'Referral', referralId: 'unknown', fileName: 'other.pdf', typeName: 'PDF', contentType: 'application/pdf', sizeBytes: 10, uploadedBy: 'k.ashford', uploadedAtUtc: '2026-11-02T11:00:00Z', scanStatus: 'NotScanned', sha256: 'y'},
      ],
    });

    expect(await screen.findByRole('heading', {name: 'Referral to Dermatology'})).toBeInTheDocument();
    expect(screen.getByText('Referrals (2)')).toBeInTheDocument();
    expect(screen.getByText('Synthetic Street Surgery')).toBeInTheDocument();
    expect(screen.getByText('A12345')).toBeInTheDocument();
    expect(screen.getByText('Reason for referral: A synthetic rash.')).toBeInTheDocument();
    expect(screen.getByText('A duplicate')).toBeInTheDocument();
    expect(screen.getByText('Already referred.')).toBeInTheDocument();
    expect(screen.getByText('Another provider')).toBeInTheDocument();
    expect(screen.getByText('Not yet')).toBeInTheDocument();
    expect(screen.getByText(/Referral registered by k\.ashford/)).toBeInTheDocument();
    expect(screen.getByText('The referral to Dermatology received 2 November 2026 · Specialty: Dermatology, Priority asked for: High')).toBeInTheDocument();
    expect(screen.getByText(/Referral: SpecialtyChanged by k\.ashford/)).toBeInTheDocument();
    expect(screen.getByText(/Attached to the referral to Dermatology received 2 November 2026\./)).toBeInTheDocument();
    expect(screen.getByText(/Attached to a referral\./)).toBeInTheDocument();
  });

  it('marks a deleted note, saying when, by whom and why, and lists the requests to delete notes', async () => {
    const pathway = data.pathways![0];
    renderExport({
      ...data,
      pathways: [
        {
          ...pathway,
          notes: [
            {
              id: 'note-2',
              author: 'k.ashford',
              text: 'Synthetic note an admin deleted',
              createdAtUtc: '2026-09-01T10:00:00Z',
              lastEditedAtUtc: null,
              version: 1,
              deletedAtUtc: '2026-09-05T10:00:00Z',
              deletedBy: 'test-admin',
              deletionReason: 'WrongPatient',
              deletionDetail: 'Synthetic: the other pathway.',
              isAboutAnotherPatient: true,
            },
          ],
        },
      ],
      noteDeletionRequests: [
        {
          id: 'request-1',
          noteId: 'note-2',
          pathwayId,
          requestedBy: 's.nurse',
          requestedAtUtc: '2026-09-04T10:00:00Z',
          reason: 'Duplicate',
          detail: 'Synthetic request detail',
          status: 'Approved',
          decidedBy: 'test-admin',
          decidedAtUtc: '2026-09-05T10:00:00Z',
        },
        {
          id: 'request-2',
          noteId: 'note-3',
          pathwayId: 'a184a7b8-2585-4437-bcd4-e1700be38219',
          requestedBy: 'l.northcott',
          requestedAtUtc: '2026-09-06T10:00:00Z',
          reason: 'Other',
          detail: 'Synthetic second detail',
          status: 'Pending',
          decidedBy: null,
          decidedAtUtc: null,
        },
      ],
    });

    // The deleted note is kept in the export, marked, with its text.
    expect(await screen.findByText('Synthetic note an admin deleted')).toBeInTheDocument();
    expect(screen.getByText('Deleted')).toBeInTheDocument();
    expect(screen.getByText(/^Deleted by test-admin, .*\. Reason: Wrong patient: Synthetic: the other pathway\.$/)).toBeInTheDocument();
    // Flagged for whoever checks the export before it's released: another patient's details.
    expect(screen.getByText(ANOTHER_PATIENTS_DETAILS)).toBeInTheDocument();
    // The requests, with their reasons and where each stands.
    expect(screen.getByText('Note Deletion Requests (2)')).toBeInTheDocument();
    expect(screen.getByText(/: s\.nurse asked for a note to be deleted$/)).toBeInTheDocument();
    expect(screen.getByText('Reason: Duplicate: Synthetic request detail')).toBeInTheDocument();
    expect(screen.getByText('Reason: Other: Synthetic second detail')).toBeInTheDocument();
    expect(screen.getByText(/^Note deleted by test-admin, /)).toBeInTheDocument();
    expect(screen.getByText('Waiting for an admin to decide.')).toBeInTheDocument();
  });

  it("says what an appointment was for when it's no longer held, or not for a pathway", async () => {
    renderExport({
      ...data,
      appointments: [
        {id: 'a1', pathwayId: 'gone', stageName: null, status: 'Requested', isAutomatic: true, createdAtUtc: '2026-09-10T09:00:00Z', createdBy: ''},
        {id: 'a2', pathwayId: null, status: 'Attended', startUtc: '2026-09-21T09:00:00Z', endUtc: '2026-09-21T09:30:00Z', staffUsername: 'k.ashford',
          createdAtUtc: '2026-09-10T09:00:00Z', createdBy: 'k.ashford', bookedAtUtc: '2026-09-11T09:00:00Z', bookedBy: 'k.ashford',
          outcomeAtUtc: '2026-09-21T10:00:00Z', outcomeBy: 'k.ashford'},
      ],
      appointmentHistory: [{atUtc: '2026-09-12T09:00:00Z', action: 'Booked', username: 'k.ashford', appointmentId: 'a2', pathwayTitle: '', details: '{}'}],
      notifications: [{kind: 'AppointmentBooked', username: 'k.ashford', title: 'Booked', message: 'Synthetic', createdAtUtc: '2026-09-12T09:00:00Z', appointmentId: 'deleted'}],
    });

    expect(await screen.findByText('Requires appointment')).toBeInTheDocument();
    expect(screen.getByText(/^A deleted pathway, requested .* automatically$/)).toBeInTheDocument();
    expect(screen.getByText(/^Not for a pathway, requested .* by k\.ashford, booked .*, outcome recorded .* by k\.ashford$/)).toBeInTheDocument();
    expect(screen.getByText('Not for a pathway')).toBeInTheDocument();
    expect(screen.getByText('About the appointment: no longer held')).toBeInTheDocument();
  });

  it('says a visit was recorded after the event, not booked, and one marked entered in error with why', async () => {
    renderExport({
      ...data,
      appointments: [
        {id: 'a3', pathwayId: null, status: 'EnteredInError', startUtc: '2026-11-12T10:00:00Z', endUtc: '2026-11-12T10:30:00Z', staffUsername: 'k.ashford',
          createdAtUtc: '2026-11-18T09:00:00Z', createdBy: 'j.smith', bookedAtUtc: '2026-11-18T09:00:00Z', bookedBy: 'j.smith',
          outcomeAtUtc: '2026-11-18T09:00:00Z', outcomeBy: 'j.smith', isRecordedAfterEvent: true, cancelReason: 'Synthetic: on the wrong patient'},
      ],
      appointmentHistory: [
        {atUtc: '2026-11-18T09:00:00Z', action: 'RecordedAfterEvent', username: 'j.smith', appointmentId: 'a3', pathwayTitle: '', details: '{"outcome":"Attended"}'},
        {atUtc: '2026-11-19T09:00:00Z', action: 'EnteredInError', username: 'j.smith', appointmentId: 'a3', pathwayTitle: '', details: '{"reason":"Synthetic: on the wrong patient"}'},
      ],
    });

    expect(await screen.findByText(/^Entered in error: /)).toBeInTheDocument();
    expect(screen.getByText(/^Not for a pathway, recorded after the event .* by j\.smith, outcome recorded .* by j\.smith$/)).toBeInTheDocument();
    expect(screen.queryByText(/requested .* by j\.smith/)).not.toBeInTheDocument();
    expect(screen.getByText('Why entered in error: Synthetic: on the wrong patient')).toBeInTheDocument();
    expect(screen.getByText(/: Recorded after the event by j\.smith$/)).toBeInTheDocument();
    expect(screen.getByText(/: Marked entered in error by j\.smith$/)).toBeInTheDocument();
    expect(screen.getByText(/· Attended$/)).toBeInTheDocument();
    expect(screen.getByText(/· Synthetic: on the wrong patient$/)).toBeInTheDocument();
  });

  it('downloads the export as a JSON file named by the patient ID and the day, never their name', async () => {
    const createObjectURL = vi.fn((blob: Blob) => {
      void blob;
      return 'blob:synthetic';
    });
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', Object.assign(URL, {createObjectURL, revokeObjectURL}));
    let downloaded = '';
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      downloaded = this.download;
    });
    const {user} = renderExport();

    await user.click(await screen.findByRole('button', {name: 'Download JSON'}));

    expect(downloaded).toBe(`patient-${id}-2026-11-16.json`);
    expect(downloaded).not.toContain('Penhallow');
    expect(JSON.parse(await (createObjectURL.mock.calls[0][0] as Blob).text())).toEqual(data);
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:synthetic');
  });

  it('prints', async () => {
    const print = vi.fn();
    vi.stubGlobal('print', print);
    const {user} = renderExport();

    await user.click(await screen.findByRole('button', {name: 'Print'}));

    expect(print).toHaveBeenCalled();
  });

  it('drops the export from the cache when the page closes, having asked for it once', async () => {
    const {fetch, queryClient, unmount} = renderExport();

    await screen.findByText('Oriel Penhallow');
    expect(queryClient.getQueryData(['patient-export', id])).toEqual(data);
    unmount();

    await waitFor(() => expect(queryClient.getQueryData(['patient-export', id])).toBeUndefined());
    expect(calls(fetch)).toEqual([`GET ${EXPORT}`]);
  });

  it('asks for the export once in development too, where StrictMode runs its effects twice', async () => {
    // Each request records the export in the activity log, so a second one would log a second export.
    const {fetch, queryClient, unmount} = renderExport(data, 200, true);

    await screen.findByText('Oriel Penhallow');
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(calls(fetch)).toEqual([`GET ${EXPORT}`]);
    expect(queryClient.getQueryData(['patient-export', id])).toEqual(data);

    unmount();
    await waitFor(() => expect(queryClient.getQueryData(['patient-export', id])).toBeUndefined());
  });

  it('names the file even without an ID or date', () => {
    expect(exportFileName({})).toBe('patient-record-export.json');
  });

  it('says when the patient is not found, or the export failed', async () => {
    renderExport({title: 'Not found'}, 404);
    expect(await findAlert('Patient not found. They may have been deleted.')).toBeInTheDocument();
  });

  it('says when the export could not be made', async () => {
    renderExport({title: 'Server error'}, 500);
    expect(await findAlert('The export could not be made. Please try again.')).toBeInTheDocument();
  });
});

describe('note deletion in words', () => {
  it("gives a deleted note's line, with or without a reason", () => {
    expect(deletedNoteText({deletedAtUtc: '2026-09-05T10:00:00Z', deletedBy: 'test-admin', deletionReason: 'WrittenInError', deletionDetail: ' Synthetic. '})).toMatch(
      /^Deleted by test-admin, .+\. Reason: Written in error: Synthetic\.$/,
    );
    expect(deletedNoteText({deletedAtUtc: '2026-09-05T10:00:00Z', deletedBy: null, deletionReason: null, deletionDetail: null})).toMatch(
      /^Deleted by an admin, .+\.$/,
    );
  });

  it("says where each request stands", () => {
    const at = {decidedBy: 'test-admin', decidedAtUtc: '2026-09-05T10:00:00Z'};
    expect(deletionRequestOutcome({status: 'Approved', ...at})).toMatch(/^Note deleted by test-admin, .+\.$/);
    expect(deletionRequestOutcome({status: 'Declined', ...at})).toMatch(/^Note kept by test-admin, .+\.$/);
    expect(deletionRequestOutcome({status: 'Closed', ...at})).toMatch(/^Closed without a decision, .+\.$/);
    expect(deletionRequestOutcome({status: 'Declined', decidedBy: null, decidedAtUtc: null})).toBe('Note kept by an admin.');
    expect(deletionRequestOutcome({status: 'Pending', decidedBy: null, decidedAtUtc: null})).toBe('Waiting for an admin to decide.');
  });
});

describe('appointment history in words', () => {
  it('names each action', () => {
    expect(
      ['Requested', 'Booked', 'Rescheduled', 'Attended', 'DidNotAttend', 'Cancelled', 'NotesUpdated', 'RecordedAfterEvent', 'EnteredInError', 'Other', null].map(
        appointmentChangeLabel,
      ),
    ).toEqual([
      'Appointment requested',
      'Appointment booked',
      'Appointment rescheduled',
      'Marked Attended',
      'Marked Did not attend',
      'Appointment cancelled',
      'Appointment notes changed',
      'Recorded after the event',
      'Marked entered in error',
      'Other',
      '',
    ]);
  });

  it('says what each change did', () => {
    const say = (action: string, details: unknown) => describeAppointmentChange({action, details: JSON.stringify(details)});
    expect(say('Requested', {automatic: true})).toBe('Requested automatically, as the stage involves a visit');
    expect(say('Requested', {})).toBe('');
    expect(say('Rescheduled', {previousStartUtc: '2026-09-21T09:00:00Z', previousEndUtc: '2026-09-21T09:30:00Z', previousStaffUsername: 'k.ashford'})).toBe(
      'From Mon 21 Sept 2026, 10:00 to 10:30 with k.ashford',
    );
    expect(say('Rescheduled', {previousStaffUsername: 'k.ashford'})).toBe('From With k.ashford');
    expect(say('Rescheduled', {})).toBe('');
    expect(say('Attended', {previousStatus: 'DidNotAttend'})).toBe('Was marked Did not attend');
    expect(say('DidNotAttend', {previousStatus: 'Attended'})).toBe('Was marked Attended');
    expect(say('Attended', {previousStatus: 'Booked'})).toBe('');
    expect(say('Cancelled', {reason: 'Synthetic reason', automatic: true})).toBe('Cancelled automatically: Synthetic reason');
    expect(say('NotesUpdated', {from: '', to: 'Synthetic note'})).toBe('Nothing → "Synthetic note"');
    expect(say('Booked', {})).toBe('');
    expect(say('RecordedAfterEvent', {outcome: 'DidNotAttend', previousStatus: 'Requested'})).toBe('Did not attend, was in Requires Appointment');
    expect(say('RecordedAfterEvent', {outcome: 'Attended'})).toBe('Attended');
    expect(say('RecordedAfterEvent', {})).toBe('');
    expect(say('EnteredInError', {reason: ' Synthetic: never happened '})).toBe('Synthetic: never happened');
    expect(describeAppointmentChange({action: 'Cancelled', details: 'not json'})).toBe('');
    expect(describeAppointmentChange({action: 'Cancelled', details: '"text"'})).toBe('');
  });

  it("says who asked for, booked and recorded an appointment, and a walk-in's waiting visit as requested", () => {
    const walkIn = {
      isRecordedAfterEvent: true,
      createdAtUtc: '2026-11-10T09:00:00Z',
      isAutomatic: true,
      bookedAtUtc: '2026-11-18T09:00:00Z',
      bookedBy: 'j.smith',
    };
    expect(appointmentHistoryWords(walkIn)).toMatch(/^requested .* automatically, recorded after the event .* by j\.smith$/);
    expect(appointmentHistoryWords({...walkIn, createdAtUtc: walkIn.bookedAtUtc})).toMatch(/^recorded after the event .* by j\.smith$/);
  });

  it('words referral history and names referrals', () => {
    expect(referralChangeLabel('EnteredInError')).toBe('Referral marked entered in error');
    expect(referralChangeLabel(null)).toBe('Referral: ');
    expect(referralName({specialtyName: 'Urology', receivedOn: undefined})).toBe('the referral to Urology');
    expect(describeReferralChange('{"changedFields":["reason","receivedOn",3],"specialtyName":"Urology","previousSpecialtyName":"Dermatology","toStatus":"Withdrawn"}')).toBe(
      'Specialty: Urology, was Dermatology, Changed: reason, received date, Now withdrawn',
    );
    expect(describeReferralChange('not json')).toBe('');
  });

  it('describes an appointment by its status and time', () => {
    expect(describeExportedAppointment({status: 'Requested'})).toBe('Requires appointment');
    expect(describeExportedAppointment({status: 'DidNotAttend', startUtc: '2026-09-21T09:00:00Z', endUtc: '2026-09-21T09:30:00Z'})).toBe(
      'Did not attend: Mon 21 Sept 2026, 10:00 to 10:30',
    );
  });
});
