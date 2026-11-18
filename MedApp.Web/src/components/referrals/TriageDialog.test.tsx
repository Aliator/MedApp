import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Text} from '@astryxdesign/core/Text';
import {screen, within} from '@testing-library/react';
import {isDisabled, openDialog, renderWithApp, staffUser} from '../../test/render';
import {testReferral} from '../../test/referrals';
import {AppIcon} from '../shared/icons';
import {TriageDialog, type TriageDialogProps} from './TriageDialog';

function render(props: Partial<TriageDialogProps> = {}) {
  const handlers = {onContinue: vi.fn(), onBack: vi.fn(), onConfirm: vi.fn(), onCancel: vi.fn()};
  const result = renderWithApp(
    <TriageDialog
      isOpen
      title="Reject Referral"
      icon={AppIcon.StatusCancelled}
      referral={testReferral()}
      step="form"
      canContinue={false}
      isProcessing={false}
      outcome="Rejecting closes the referral."
      summary={<MetadataListItem label="Reason">Duplicate</MetadataListItem>}
      {...handlers}
      {...props}>
      <Text type="body">The form</Text>
    </TriageDialog>,
    {user: staffUser},
  );
  return {...result, ...handlers};
}

describe('TriageDialog', () => {
  it('shows the patient and the referral beside the form, with Continue held until the choices are made', async () => {
    const {user, onCancel} = render();

    expect(await screen.findByRole('heading', {name: 'Reject Referral'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Dermatology')).toBeInTheDocument();
    expect(dialog.getByText('2 November 2026')).toBeInTheDocument();
    expect(dialog.getByText('The form')).toBeInTheDocument();
    expect(dialog.queryByText('Duplicate')).not.toBeInTheDocument();
    expect(isDisabled(dialog.getByRole('button', {name: 'Continue'}))).toBe(true);
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });

  it('continues once the choices are made', async () => {
    const {user, onContinue} = render({canContinue: true});

    await user.click(await screen.findByRole('button', {name: 'Continue'}));
    expect(onContinue).toHaveBeenCalled();
  });

  it('checks the choices with the patient shown before confirming, and goes back to change them', async () => {
    const {user, onBack, onConfirm} = render({step: 'check'});

    expect(await screen.findByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText(/^Check this is the right patient and referral, and that this is your decision\. Rejecting closes the referral\.$/)).toBeInTheDocument();
    expect(dialog.getByText('Duplicate')).toBeInTheDocument();
    expect(dialog.queryByText('The form')).not.toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Back'}));
    expect(onBack).toHaveBeenCalled();
    await user.click(dialog.getByRole('button', {name: 'Reject Referral'}));
    expect(onConfirm).toHaveBeenCalled();
  });

  it("shows the API's errors and messages", async () => {
    render({
      errors: {title: 'Invalid', fields: {reason: ['Choose a reason.'], other: ['Something else.']}, general: []},
      fields: ['reason'],
      message: 'Someone triaged this referral meanwhile.',
    });

    expect(await screen.findByRole('heading', {name: 'Reject Referral'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(dialog.getByText('Something else.')).toBeInTheDocument();
    expect(dialog.queryByText('Choose a reason.')).not.toBeInTheDocument();
    expect(dialog.getByText('Someone triaged this referral meanwhile.')).toBeInTheDocument();
  });

  it("can't be left while it's sending", async () => {
    render({step: 'check', isProcessing: true});

    expect(await screen.findByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
    const dialog = within(openDialog()!);
    expect(isDisabled(dialog.getByRole('button', {name: 'Back'}))).toBe(true);
  });
});
