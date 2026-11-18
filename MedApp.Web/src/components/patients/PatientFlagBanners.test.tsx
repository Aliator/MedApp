import {screen, waitFor, within} from '@testing-library/react';
import {openAlertDetails, renderWithApp} from '../../test/render';
import {formatDateTime} from '../../utils/dates';
import {DISPUTE_IN_LABEL_MAX, hasPatientFlags, PatientFlagBanners} from './PatientFlagBanners';

const at = '2026-11-10T09:30:00Z';

describe('PatientFlagBanners', () => {
  it('shows nothing for a record with no flags', () => {
    const {container} = renderWithApp(<PatientFlagBanners patient={{isRestricted: false, isDisputed: false, isAnonymised: false}} />);

    expect(hasPatientFlags({})).toBe(false);
    expect(container.textContent).toBe('');
  });

  it('shows "Restricted record: changes paused", with the whole restriction, its reason and since when, over the page', async () => {
    const {user} = renderWithApp(
      <PatientFlagBanners patient={{isRestricted: true, restrictionReason: 'Synthetic accuracy check', restrictedAtUtc: at}} />,
    );

    await screen.findByRole('button', {name: 'Restricted record: changes paused, show details'});
    expect(screen.getByText('Restricted record: changes paused')).toBeInTheDocument();
    // Still an alert, with the whole message.
    expect(screen.getByRole('alert')).toHaveTextContent(/^Restricted record: nothing about this patient.*Reason: Synthetic accuracy check/);

    const details = await openAlertDetails(user, 'Restricted record');
    expect(within(details).getByText(/^Restricted record: nothing about this patient/)).toBeInTheDocument();
    expect(within(details).getByText('Reason: Synthetic accuracy check')).toBeInTheDocument();
    expect(within(details).getByText(`Since ${formatDateTime(at)}.`)).toBeInTheDocument();
  });

  it('shows "Disputed record: <what is disputed>" when that is short, with it over the page too', async () => {
    const {user} = renderWithApp(<PatientFlagBanners patient={{isDisputed: true, disputeReason: ' Synthetic date of birth ', disputedAtUtc: at}} />);

    await screen.findByRole('button', {name: 'Disputed record: Synthetic date of birth, show details'});
    expect(screen.getByText('Disputed record: Synthetic date of birth')).toBeInTheDocument();
    const details = await openAlertDetails(user, 'Disputed record');
    expect(within(details).getByText('Disputed: the patient disputes something in this record.')).toBeInTheDocument();
    expect(within(details).getByText("What's disputed: Synthetic date of birth")).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('shows only "Disputed record" when what is disputed is too long for the label', async () => {
    const long = 'x'.repeat(DISPUTE_IN_LABEL_MAX + 1);
    const {user} = renderWithApp(<PatientFlagBanners patient={{isDisputed: true, disputeReason: long}} />);

    await screen.findByRole('button', {name: 'Disputed record, show details'});
    expect(screen.getByText('Disputed record')).toBeInTheDocument();
    const details = await openAlertDetails(user, 'Disputed record');
    expect(within(details).getByText(`What's disputed: ${long}`)).toBeInTheDocument();
  });

  it('shows "Anonymised record"; and flags without reasons or dates show just their message', async () => {
    const {user} = renderWithApp(<PatientFlagBanners patient={{isAnonymised: true, anonymisedAtUtc: at, isRestricted: true, isDisputed: true}} />);

    expect(await screen.findByRole('button', {name: 'Restricted record: changes paused, show details'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Disputed record, show details'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Anonymised record, show details'})).toBeInTheDocument();
    expect(screen.getByText('Anonymised record')).toBeInTheDocument();

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
