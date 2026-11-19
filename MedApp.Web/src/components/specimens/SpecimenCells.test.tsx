import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {SPECIMEN_ID, SPECIMEN_PATIENT_ID, testSummary} from '../../test/specimens';
import {latestStep, OverdueAtLabToken, SpecimenNumberCell, SpecimenPatientCell, SpecimenStatusToken} from './SpecimenCells';

describe('SpecimenCells', () => {
  afterEach(() => localStorage.clear());

  it('words statuses, with an icon when icons on statuses are on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <SpecimenStatusToken status="Taken" />
        <SpecimenStatusToken status="RejectedByLab" />
        <SpecimenStatusToken status="EnteredInError" />
        <SpecimenStatusToken status="Elsewhere" />
        <OverdueAtLabToken />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('Taken')).toBeInTheDocument();
    expect(screen.getByText('Rejected by the lab')).toBeInTheDocument();
    expect(screen.getByText('Entered in error')).toBeInTheDocument();
    expect(screen.getByText('Elsewhere')).toBeInTheDocument();
    expect(screen.getByText('Overdue at lab')).toBeInTheDocument();
    expect(document.querySelectorAll('svg').length).toBeGreaterThanOrEqual(4);
  });

  it('shows no icons when icons on statuses are off', async () => {
    renderWithApp(
      <>
        <SpecimenStatusToken status="Sent" />
        <OverdueAtLabToken />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('Sent')).toBeInTheDocument();
    expect(document.querySelectorAll('svg')).toHaveLength(0);
  });

  it("links the number to the sample and the patient to their page, with the NHS number", async () => {
    renderWithApp(
      <>
        <SpecimenNumberCell specimen={testSummary()} />
        <SpecimenPatientCell specimen={testSummary()} />
        <SpecimenPatientCell specimen={testSummary({patientNhsNumber: null})} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByRole('link', {name: 'MA26-000123'})).toHaveAttribute('href', `/specimens/${SPECIMEN_ID}`);
    expect(screen.getByText('Skin biopsy (punch)')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Freya Ahmed'})[0]).toHaveAttribute('href', `/patients/${SPECIMEN_PATIENT_ID}`);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
  });

  it('says the latest step and when', () => {
    expect(latestStep(testSummary())).toBe('Sent 2 Nov 2026, 15:00');
    expect(latestStep(testSummary({status: 'Lost', closedAtUtc: '2026-11-04T10:00:00Z'}))).toBe('Lost 4 Nov 2026, 10:00');
    expect(latestStep(testSummary({status: 'Taken', sentAtUtc: null}))).toBe('Taken 2 Nov 2026, 09:00');
  });
});
