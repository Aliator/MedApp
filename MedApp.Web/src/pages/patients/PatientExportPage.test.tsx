import {screen} from '@testing-library/react';
import type {PatientExport} from '../../api/patients';
import {adminUser, calls, findAlert, json, mockApi, renderWithApp} from '../../test/render';
import {exportFileName, PatientExportPage} from './PatientExportPage';

const id = 'd184a7b8-2585-4437-bcd4-e1700be38211';
const procedureId = 'e184a7b8-2585-4437-bcd4-e1700be38212';
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
      notes: 'Synthetic procedure note',
      assignments: [{userId: 'u1', username: 'k.ashford'}],
      stages: [
        {
          id: 's1',
          stageOrder: 1,
          name: 'Consent',
          notes: 'Synthetic stage note',
          startedAtUtc: '2026-09-02T09:00:00Z',
          startedByUsername: 'k.ashford',
          completedAtUtc: '2026-09-03T09:00:00Z',
          completedByUsername: 'l.northcott',
        },
        {id: 's2', stageOrder: 2, name: 'Scan', notes: ''},
      ],
    },
  ],
  procedureHistory: [
    {
      id: 'h1',
      patientProcedureId: procedureId,
      procedureTitle: 'Synthetic knee review',
      stageName: 'Consent',
      action: 'StageFinished',
      username: 'l.northcott',
      atUtc: '2026-09-03T09:00:00Z',
      details: '{}',
    },
  ],
  accessLog: [
    {id: 'a1', action: 'PatientExported', username: 'test-admin', atUtc: '2026-11-16T09:30:00Z', patientId: id, details: '{}'},
    {id: 'a2', action: 'PatientRestricted', username: 'test-admin', atUtc: '2026-11-01T09:00:00Z', patientId: id, details: JSON.stringify({reason: 'Synthetic accuracy check'})},
  ],
  notifications: [
    {id: 'n1', kind: 'ProcedureAssigned', username: 'k.ashford', title: 'Synthetic knee review', message: 'test-admin assigned you to this procedure.', createdAtUtc: '2026-09-01T09:00:00Z'},
  ],
};

function renderExport(body: unknown = data, status = 200) {
  const fetch = mockApi({[`GET ${EXPORT}`]: () => json(body, status)});
  return {fetch, ...renderWithApp(<PatientExportPage id={id} />, {user: adminUser})};
}

describe('PatientExportPage', () => {
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
    // The history, the access log and the notifications.
    expect(screen.getByText(/Stage finished by l\.northcott/)).toBeInTheDocument();
    expect(screen.getByText(/Record exported by test-admin/)).toBeInTheDocument();
    expect(screen.getByText(/Record restricted by test-admin/)).toBeInTheDocument();
    expect(screen.getByText(/to k\.ashford/)).toBeInTheDocument();
    expect(screen.getByText('Synthetic knee review: test-admin assigned you to this procedure.')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${EXPORT}`]);
  });

  it('says when there is nothing in a part', async () => {
    renderExport({exportedAtUtc: '2026-11-16T09:30:00Z', exportedBy: 'test-admin', patient: {id, firstName: 'Oriel'}});

    expect(await screen.findByText('No procedures.')).toBeInTheDocument();
    expect(screen.getByText('No changes recorded.')).toBeInTheDocument();
    expect(screen.getByText('No access recorded.')).toBeInTheDocument();
    expect(screen.getByText('No notifications.')).toBeInTheDocument();
    expect(screen.getByText('Not given')).toBeInTheDocument();
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
