import {screen, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {HalfViewContext} from '../pages/halfView';
import {bannerName, PatientBanner} from './PatientBanner';

// Synthetic patients and NHS numbers (the 999 range is never issued to real patients).
const freya = {firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', nhsNumber: '9990000018'};
const oriel = {firstName: 'Oriel', lastName: 'Penhallow', dateOfBirth: '1951-03-14', nhsNumber: '9990000026'};

describe('bannerName', () => {
  it('puts the family name first, keeping its own case', () => {
    expect(bannerName({firstName: ' Freya ', lastName: "o'Neill"})).toBe("o'Neill, Freya");
  });

  it('copes with a missing part', () => {
    expect(bannerName({firstName: 'Freya', lastName: null})).toBe('Freya');
    expect(bannerName({firstName: undefined, lastName: 'Ahmed'})).toBe('Ahmed');
    expect(bannerName({})).toBe('Unknown patient');
  });
});

describe('PatientBanner', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date']});
    vi.setSystemTime(new Date('2026-11-16T12:00:00Z'));
  });

  it('shows the name, date of birth with age, and NHS number, in a region named for the patient', async () => {
    renderWithApp(<PatientBanner patient={freya} />);

    const region = await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'});
    // The family name in capitals by CSS; its text, which screen readers read, in its own case.
    const family = within(region).getByText('Ahmed');
    expect(family).toHaveStyle({textTransform: 'uppercase'});
    expect(family.parentElement).toHaveTextContent(/^Ahmed, Freya$/);
    expect(within(region).getByText('Born 1 December 1976 (49 years)')).toBeInTheDocument();
    // Seen grouped, heard a digit at a time.
    expect(within(region).getByText('NHS No. 999 000 0018')).toHaveAttribute('aria-hidden', 'true');
    expect(within(region).getByText('NHS number 9 9 9, 0 0 0, 0 0 1 8')).toBeInTheDocument();
    expect(within(region).queryByText('Restricted')).not.toBeInTheDocument();
  });

  it('names two banners apart, as in two half views', async () => {
    renderWithApp(
      <>
        <PatientBanner patient={freya} />
        <PatientBanner patient={oriel} />
      </>,
    );

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Penhallow, Oriel'})).toBeInTheDocument();
  });

  it('names a patient with only one name, or none', async () => {
    renderWithApp(
      <>
        <PatientBanner patient={{firstName: 'Freya'}} />
        <PatientBanner patient={{lastName: 'Ahmed'}} />
        <PatientBanner patient={{}} />
      </>,
    );

    expect(await screen.findByRole('region', {name: 'Patient: Freya'})).toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Ahmed'})).toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Unknown patient'})).toBeInTheDocument();
  });

  it.each([
    ['2026-11-10', '6 days'],
    ['2026-09-07', '10 weeks'],
    ['2025-09-16', '14 months'],
  ])('gives a young child born %s as %s, as the guidance does', async (dateOfBirth, age) => {
    renderWithApp(<PatientBanner patient={{...freya, dateOfBirth}} />);

    expect(await screen.findByText(new RegExp(`\\(${age}\\)$`))).toBeInTheDocument();
  });

  it('says when there is no NHS number or date of birth', async () => {
    renderWithApp(<PatientBanner patient={{...freya, nhsNumber: null, dateOfBirth: null}} />);

    expect(await screen.findByText('No NHS number')).toBeInTheDocument();
    expect(screen.getByText('Date of birth not known')).toBeInTheDocument();
  });

  it('shows a date of birth it cannot give an age for as it is', async () => {
    renderWithApp(<PatientBanner patient={{...freya, dateOfBirth: '2999-01-01'}} />);

    expect(await screen.findByText('Born 1 January 2999')).toBeInTheDocument();
  });

  it('marks a restricted, disputed or anonymised record', async () => {
    renderWithApp(<PatientBanner patient={{...freya, isRestricted: true, isDisputed: true, isAnonymised: true}} />);

    const region = await screen.findByRole('region', {name: /^Patient: /});
    expect(within(region).getByText('Restricted')).toBeInTheDocument();
    expect(within(region).getByText('Disputed')).toBeInTheDocument();
    expect(within(region).getByText('Anonymised')).toBeInTheDocument();
  });

  it('shows the same details in a half view', async () => {
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <PatientBanner patient={freya} />
      </HalfViewContext.Provider>,
    );

    expect(await screen.findByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(screen.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
  });
});
