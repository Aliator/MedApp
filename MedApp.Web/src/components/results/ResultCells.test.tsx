import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {RESULT_ID, RESULT_PATIENT_ID, testResultSummary} from '../../test/results';
import {ageWords, NeedsClinicianToken, ResultPatientCell, ResultStatusToken, ResultTitleCell} from './ResultCells';

describe('ResultCells', () => {
  afterEach(() => localStorage.clear());

  it('words statuses, with an icon when icons on statuses are on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <ResultStatusToken status="AwaitingSignOff" />
        <ResultStatusToken status="Seen" />
        <ResultStatusToken status="SignedOff" />
        <ResultStatusToken status="EnteredInError" />
        <ResultStatusToken status="Elsewhere" />
        <NeedsClinicianToken />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('Awaiting sign-off')).toBeInTheDocument();
    expect(screen.getByText('Seen')).toBeInTheDocument();
    expect(screen.getByText('Signed off')).toBeInTheDocument();
    expect(screen.getByText('Entered in error')).toBeInTheDocument();
    expect(screen.getByText('Elsewhere')).toBeInTheDocument();
    expect(screen.getByText('Needs a clinician')).toBeInTheDocument();
    expect(document.querySelectorAll('svg').length).toBeGreaterThanOrEqual(5);
  });

  it('shows no icons when icons on statuses are off', async () => {
    renderWithApp(<ResultStatusToken status="Seen" />, {user: staffUser});
    expect(await screen.findByText('Seen')).toBeInTheDocument();
    expect(document.querySelectorAll('svg')).toHaveLength(0);
  });

  it('shows the patient with their NHS number and date of birth, and the title linked to the result', async () => {
    renderWithApp(
      <>
        <ResultPatientCell result={testResultSummary()} />
        <ResultTitleCell result={testResultSummary()} />
        <ResultTitleCell result={testResultSummary({id: 'r2', title: 'CT chest', specimenNumber: null, source: 'ExternalReport'})} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('href', `/patients/${RESULT_PATIENT_ID}`);
    expect(screen.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(screen.getByText('Born 1 Dec 1976')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Skin biopsy (punch) report'})).toHaveAttribute('href', `/results/${RESULT_ID}`);
    expect(screen.getByText('Specimen MA26-000123')).toBeInTheDocument();
    expect(screen.getByText('External report')).toBeInTheDocument();
  });

  it('words an age in days', () => {
    expect(ageWords(0)).toBe('Today');
    expect(ageWords(1)).toBe('1 day');
    expect(ageWords(12)).toBe('12 days');
  });
});
