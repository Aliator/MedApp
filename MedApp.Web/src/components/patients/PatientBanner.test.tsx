import {screen, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {HalfViewContext} from '../pages/halfView';
import {ageText, bannerName, PatientBanner} from './PatientBanner';

// Synthetic patient and NHS number (the 999 range is never issued to real patients).
const freya = {firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', nhsNumber: '9990000018'};

describe('bannerName', () => {
  it('puts the family name first, in capitals', () => {
    expect(bannerName({firstName: ' Freya ', lastName: "o'neill"})).toBe("O'NEILL, Freya");
  });

  it('copes with a missing part', () => {
    expect(bannerName({firstName: 'Freya', lastName: null})).toBe('Freya');
    expect(bannerName({firstName: undefined, lastName: 'Ahmed'})).toBe('AHMED');
    expect(bannerName({})).toBe('Unknown patient');
  });
});

describe('ageText', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date']});
    vi.setSystemTime(new Date('2026-11-16T12:00:00Z'));
  });
  afterEach(() => vi.useRealTimers());

  it('gives the age in years, or under 1 year', () => {
    expect(ageText('1976-12-01')).toBe('49 years');
    expect(ageText('2025-11-01')).toBe('1 year');
    expect(ageText('2026-06-01')).toBe('under 1 year');
  });

  it('gives nothing for a date it cannot read, or one in the future', () => {
    expect(ageText('not a date')).toBeNull();
    expect(ageText('2027-01-01')).toBeNull();
  });
});

describe('PatientBanner', () => {
  it('shows the name, date of birth with age, and NHS number, as a Patient region', async () => {
    renderWithApp(<PatientBanner patient={freya} />);

    const region = await screen.findByRole('region', {name: 'Patient'});
    expect(within(region).getByText('AHMED, Freya')).toBeInTheDocument();
    expect(within(region).getByText(`Born 1 December 1976 (${ageText('1976-12-01')})`)).toBeInTheDocument();
    // Seen grouped, heard a digit at a time.
    expect(within(region).getByText('NHS No. 999 000 0018')).toHaveAttribute('aria-hidden', 'true');
    expect(within(region).getByText('NHS number 9 9 9, 0 0 0, 0 0 1 8')).toBeInTheDocument();
    expect(within(region).queryByText('Restricted')).not.toBeInTheDocument();
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

    const region = await screen.findByRole('region', {name: 'Patient'});
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

    expect(await screen.findByText('AHMED, Freya')).toBeInTheDocument();
    expect(screen.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
  });
});
