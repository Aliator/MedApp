import {screen, waitFor, within} from '@testing-library/react';
import {openAlertDetails, renderWithApp} from '../../test/render';
import {formatDateTime} from '../../utils/dates';
import {hasPatientFlags, PatientFlagBanners} from './PatientFlagBanners';

const at = '2026-11-10T09:30:00Z';

describe('PatientFlagBanners', () => {
  it('shows nothing for a record with no flags', () => {
    const {container} = renderWithApp(<PatientFlagBanners patient={{isRestricted: false, isDisputed: false, isAnonymised: false}} />);

    expect(hasPatientFlags({})).toBe(false);
    expect(container.textContent).toBe('');
  });

  it('shows "Restricted record", with the whole restriction, its reason and since when, over the page', async () => {
    const {user} = renderWithApp(
      <PatientFlagBanners patient={{isRestricted: true, restrictionReason: 'Synthetic accuracy check', restrictedAtUtc: at}} />,
    );

    const label = await screen.findByRole('button', {name: 'Restricted record, show details'});
    expect(label).toHaveTextContent(/^Restricted record$/);
    // Still an alert, read in full.
    expect(screen.getByRole('alert')).toHaveTextContent(/Restricted record: nothing about this patient.*Reason: Synthetic accuracy check/);

    const details = await openAlertDetails(user, 'Restricted record');
    expect(within(details).getByText(/^Restricted record: nothing about this patient/)).toBeInTheDocument();
    expect(within(details).getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    expect(within(details).getByText(`Since ${formatDateTime(at)}.`)).toBeInTheDocument();
  });

  it('shows "Disputed record", with what is disputed over the page', async () => {
    const {user} = renderWithApp(<PatientFlagBanners patient={{isDisputed: true, disputeReason: 'Synthetic date of birth', disputedAtUtc: at}} />);

    const details = await openAlertDetails(user, 'Disputed record');
    expect(within(details).getByText('Disputed: the patient disputes something in this record.')).toBeInTheDocument();
    expect(within(details).getByText("What's disputed: Synthetic date of birth")).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows "Anonymised record"; and flags without reasons or dates show just their message', async () => {
    const {user} = renderWithApp(<PatientFlagBanners patient={{isAnonymised: true, anonymisedAtUtc: at, isRestricted: true, isDisputed: true}} />);

    expect(await screen.findByRole('button', {name: 'Restricted record, show details'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Disputed record, show details'})).toBeInTheDocument();

    const details = await openAlertDetails(user, 'Anonymised record');
    expect(within(details).getByText(/^This record has been anonymised/)).toBeInTheDocument();
    expect(within(details).getByText(`Since ${formatDateTime(at)}.`)).toBeInTheDocument();
    expect(screen.queryByText(/^Reason:/)).not.toBeInTheDocument();
    expect(hasPatientFlags({isAnonymised: true})).toBe(true);
  });

  it('opens and closes under StrictMode', async () => {
    const {user} = renderWithApp(<PatientFlagBanners patient={{isRestricted: true, restrictionReason: 'Synthetic accuracy check'}} />, {
      strict: true,
    });

    const details = await openAlertDetails(user, 'Restricted record');
    expect(within(details).getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    await user.tab();
    await waitFor(() => expect(screen.queryByRole('dialog', {name: 'Restricted record: details'})).not.toBeInTheDocument());
  });
});
