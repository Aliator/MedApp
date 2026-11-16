import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {formatDateTime} from '../../utils/dates';
import {hasPatientFlags, PatientFlagBanners} from './PatientFlagBanners';

const at = '2026-11-10T09:30:00Z';

describe('PatientFlagBanners', () => {
  it('shows nothing for a record with no flags', () => {
    const {container} = renderWithApp(<PatientFlagBanners patient={{isRestricted: false, isDisputed: false, isAnonymised: false}} />);

    expect(hasPatientFlags({})).toBe(false);
    expect(container.textContent).toBe('');
  });

  it('shows the restriction, with its reason and since when', async () => {
    renderWithApp(<PatientFlagBanners patient={{isRestricted: true, restrictionReason: 'Synthetic accuracy check', restrictedAtUtc: at}} />);

    expect(await screen.findByText(/Restricted record: nothing about this patient/)).toBeInTheDocument();
    expect(screen.getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    expect(screen.getByText(`Since ${formatDateTime(at)}.`)).toBeInTheDocument();
  });

  it('shows the dispute and what is disputed', async () => {
    renderWithApp(<PatientFlagBanners patient={{isDisputed: true, disputeReason: 'Synthetic date of birth', disputedAtUtc: at}} />);

    expect(await screen.findByText('Disputed: the patient disputes something in this record.')).toBeInTheDocument();
    expect(screen.getByText("What's disputed: Synthetic date of birth")).toBeInTheDocument();
  });

  it('shows that the record is anonymised; and flags without reasons or dates show just their headline', async () => {
    renderWithApp(<PatientFlagBanners patient={{isAnonymised: true, anonymisedAtUtc: at, isRestricted: true, isDisputed: true}} />);

    expect(await screen.findByText(/This record has been anonymised/)).toBeInTheDocument();
    expect(screen.getByText(/Restricted record/)).toBeInTheDocument();
    expect(screen.getByText(/Disputed:/)).toBeInTheDocument();
    expect(screen.queryByText(/^Reason:/)).not.toBeInTheDocument();
    expect(hasPatientFlags({isAnonymised: true})).toBe(true);
  });
});
