import {screen} from '@testing-library/react';
import type {PatientExport} from '../../api/patients';
import {adminUser, calls, findAlert, json, mockApi, renderWithApp} from '../../test/render';
import {appointmentChangeLabel, describeAppointmentChange, describeExportedAppointment, exportFileName, PatientExportPage} from './PatientExportPage';

const id = 'd184a7b8-2585-4437-bcd4-e1700be38211';
const procedureId = 'e184a7b8-2585-4437-bcd4-e1700be38212';
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
  procedures: [
    {
      id: procedureId,
      title: 'Synthetic knee review',
      templateName: 'Knee review',
      status: 'Completed',
      priority: 'High',
      dueDate: '2026-10-01',
      createdAt: '2026-09-01T09:00:00Z',
      lastUpdated: '2026-10-02T09:00:00Z',
      notes: 'Synthetic procedure note',
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
  procedureHistory: [
    {
      procedureId,
      procedureTitle: 'Synthetic knee review',
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
      procedureId,
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
      procedureId,
      procedureTitle: 'Synthetic knee review',
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
    {kind: 'ProcedureAssigned', username: 'k.ashford', title: 'Synthetic knee review', message: 'test-admin assigned you to this procedure.', createdAtUtc: '2026-09-01T09:00:00Z'},
    {
      kind: 'AppointmentCancelled',
      username: 'k.ashford',
      title: 'Appointment cancelled',
      message: 'l.northcott cancelled an appointment.',
      createdAtUtc: '2026-09-12T09:00:00Z',
      procedureId,
      appointmentId,
    },
  ],
};

function renderExport(body: unknown = data, status = 200) {
  const fetch = mockApi({[`GET ${EXPORT}`]: () => json(body, status)});
  return {fetch, ...renderWithApp(<PatientExportPage id={id} />, {user: adminUser})};
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
        {...file, id: 'f2', attachedTo: 'Stage', procedureId, stageName: 'Consent', fileName: 'synthetic-consent.pdf', scanStatus: 'Clean'},
        {
          ...file,
          id: 'f3',
          attachedTo: 'Procedure',
          procedureId,
          fileName: 'synthetic-old.pdf',
          deletedAtUtc: '2026-10-01T09:00:00Z',
          deletedBy: 'test-admin',
        },
        {...file, id: 'f4', attachedTo: 'Stage', procedureId: 'gone', stageName: null, fileName: 'synthetic-orphan.pdf', scanStatus: 'Odd'},
      ],
    });

    expect(await screen.findByText('Files (4)')).toBeInTheDocument();
    expect(screen.getByText(/The files themselves aren't included in this export\./)).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to the patient.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to Consent, in Synthetic knee review.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to Synthetic knee review.')).toBeInTheDocument();
    expect(screen.getByText('PDF (application/pdf), 2 KB. Attached to a stage, in a procedure.')).toBeInTheDocument();
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
    // Procedures, with their stages and notes.
    expect(screen.getByText('Procedures (1)')).toBeInTheDocument();
    expect(screen.getByText('Synthetic procedure note')).toBeInTheDocument();
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
    expect(screen.getByText('Synthetic knee review: test-admin assigned you to this procedure.')).toBeInTheDocument();
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

    expect(await screen.findByText('No procedures.')).toBeInTheDocument();
    expect(screen.getByText('No changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('No access recorded.')).toBeInTheDocument();
    expect(screen.getByText('No notifications.')).toBeInTheDocument();
    expect(screen.getByText('No appointments.')).toBeInTheDocument();
    expect(screen.getByText('No appointment changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('No files.')).toBeInTheDocument();
    expect(screen.getByText('Not given')).toBeInTheDocument();
  });

  it("says what an appointment was for when it's no longer held, or not for a procedure", async () => {
    renderExport({
      ...data,
      appointments: [
        {id: 'a1', procedureId: 'gone', stageName: null, status: 'Requested', isAutomatic: true, createdAtUtc: '2026-09-10T09:00:00Z', createdBy: ''},
        {id: 'a2', procedureId: null, status: 'Attended', startUtc: '2026-09-21T09:00:00Z', endUtc: '2026-09-21T09:30:00Z', staffUsername: 'k.ashford',
          createdAtUtc: '2026-09-10T09:00:00Z', createdBy: 'k.ashford', bookedAtUtc: '2026-09-11T09:00:00Z', bookedBy: 'k.ashford',
          outcomeAtUtc: '2026-09-21T10:00:00Z', outcomeBy: 'k.ashford'},
      ],
      appointmentHistory: [{atUtc: '2026-09-12T09:00:00Z', action: 'Booked', username: 'k.ashford', appointmentId: 'a2', procedureTitle: '', details: '{}'}],
      notifications: [{kind: 'AppointmentBooked', username: 'k.ashford', title: 'Booked', message: 'Synthetic', createdAtUtc: '2026-09-12T09:00:00Z', appointmentId: 'deleted'}],
    });

    expect(await screen.findByText('Needs booking')).toBeInTheDocument();
    expect(screen.getByText(/^A deleted procedure, requested .* automatically$/)).toBeInTheDocument();
    expect(screen.getByText(/^Not for a procedure, requested .* by k\.ashford, booked .*, outcome recorded .* by k\.ashford$/)).toBeInTheDocument();
    expect(screen.getByText('Not for a procedure')).toBeInTheDocument();
    expect(screen.getByText('About the appointment: no longer held')).toBeInTheDocument();
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

    expect(queryClient.getQueryData(['patient-export', id])).toBeUndefined();
    expect(calls(fetch)).toEqual([`GET ${EXPORT}`]);
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

describe('appointment history in words', () => {
  it('names each action', () => {
    expect(['Requested', 'Booked', 'Rescheduled', 'Attended', 'DidNotAttend', 'Cancelled', 'NotesUpdated', 'Other', null].map(appointmentChangeLabel)).toEqual([
      'Appointment requested',
      'Appointment booked',
      'Appointment rescheduled',
      'Marked Attended',
      'Marked Did not attend',
      'Appointment cancelled',
      'Appointment notes changed',
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
    expect(describeAppointmentChange({action: 'Cancelled', details: 'not json'})).toBe('');
    expect(describeAppointmentChange({action: 'Cancelled', details: '"text"'})).toBe('');
  });

  it('describes an appointment by its status and time', () => {
    expect(describeExportedAppointment({status: 'Requested'})).toBe('Needs booking');
    expect(describeExportedAppointment({status: 'DidNotAttend', startUtc: '2026-09-21T09:00:00Z', endUtc: '2026-09-21T09:30:00Z'})).toBe(
      'Did not attend: Mon 21 Sept 2026, 10:00 to 10:30',
    );
  });
});
