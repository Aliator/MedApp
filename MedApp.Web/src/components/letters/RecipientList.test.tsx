import {screen, within} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {patientRecipient, practiceRecipient} from '../../test/letters';
import {dispatchWords, RecipientList} from './RecipientList';

describe('RecipientList', () => {
  it('lists recipients with their addresses, without dispatch before approval', async () => {
    renderWithApp(<RecipientList recipients={[practiceRecipient(), patientRecipient({name: ''})]} showsDispatch={false} />, {user: staffUser});

    const list = await screen.findByRole('list', {name: 'Recipients'});
    expect(within(list).getByText('Addressed to: Testbrook Surgery')).toBeInTheDocument();
    expect(within(list).getByText('Copy: The patient')).toBeInTheDocument();
    expect(within(list).getByText('1 Synthetic Street')).toBeInTheDocument();
    expect(within(list).getByText('ZZ1 1ZZ')).toBeInTheDocument();
    expect(within(list).getByText('testbrook.surgery@nhs.net')).toBeInTheDocument();
    expect(screen.queryByText('Not sent')).not.toBeInTheDocument();
  });

  it('says when there are none', async () => {
    renderWithApp(<RecipientList recipients={[]} showsDispatch />, {user: staffUser});
    expect(await screen.findByText('No recipients.')).toBeInTheDocument();
  });

  it("shows each one's dispatch, offers what the API allows, and says why secure email can't be used", async () => {
    const onSend = vi.fn();
    const onRecord = vi.fn();
    const {user} = renderWithApp(
      <RecipientList
        recipients={[
          practiceRecipient({canSendSecureEmail: true, canRecordSent: true}),
          patientRecipient({canRecordSent: true, whyNoSecureEmail: 'Letters go to patients by post or by hand.'}),
          patientRecipient({id: 'r3', dispatchStatus: 'Sent', method: 'Post', sentOn: '2026-11-17', sentByUsername: 'k.ashford'}),
          practiceRecipient({id: 'r4', dispatchStatus: 'Failed', method: 'SecureEmail'}),
        ]}
        showsDispatch
        onSendSecureEmail={onSend}
        onRecordSent={onRecord}
      />,
      {user: staffUser},
    );

    const items = within(await screen.findByRole('list', {name: 'Recipients'})).getAllByRole('listitem');
    await user.click(within(items[0]).getByRole('button', {name: 'Send by Secure Email'}));
    expect(onSend).toHaveBeenCalledWith(expect.objectContaining({kind: 'GpPractice'}));
    await user.click(within(items[1]).getByRole('button', {name: 'Record as Sent'}));
    expect(onRecord).toHaveBeenCalledWith(expect.objectContaining({kind: 'Patient'}));
    expect(within(items[1]).getByText('Letters go to patients by post or by hand.')).toBeInTheDocument();
    expect(within(items[1]).queryByRole('button', {name: 'Send by Secure Email'})).not.toBeInTheDocument();
    expect(within(items[2]).getByText('Sent')).toBeInTheDocument();
    expect(within(items[2]).getByText('Post, 17 November 2026, by k.ashford')).toBeInTheDocument();
    expect(within(items[3]).getByText('Failed')).toBeInTheDocument();
    expect(within(items[3]).getByText('Secure email from MedApp')).toBeInTheDocument();
  });

  it('words how it went', () => {
    expect(dispatchWords(practiceRecipient())).toBeNull();
    expect(dispatchWords(practiceRecipient({dispatchStatus: 'Sent', method: null}))).toBeNull();
    expect(dispatchWords(practiceRecipient({dispatchStatus: 'Sent', method: 'HandedToPatient'}))).toBe('Handed to the patient');
  });
});
