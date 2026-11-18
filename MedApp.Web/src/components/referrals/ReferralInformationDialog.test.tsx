import {screen, waitFor, within} from '@testing-library/react';
import {json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {REFERRAL_ID, testReferral} from '../../test/referrals';
import {ReferralInformationDialog, type ReferralInformationStep} from './ReferralInformationDialog';

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function render(step: ReferralInformationStep, handlers: Parameters<typeof mockApi>[0]) {
  const fetch = mockApi(handlers);
  const onDone = vi.fn();
  const onCancel = vi.fn();
  const referral = testReferral({canTriage: true, status: step === 'ask' ? 'AwaitingTriage' : 'InformationRequested'});
  const result = renderWithApp(<ReferralInformationDialog step={step} referral={referral} onCancel={onCancel} onDone={onDone} />, {user: staffUser});
  return {...result, fetch, onDone, onCancel};
}

describe('ReferralInformationDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('asks for more information from the version shown, showing the patient and the referral first', async () => {
    const {user, fetch, onDone} = render('ask', {
      [`POST /api/referrals/${REFERRAL_ID}/ask-for-information`]: () => json(testReferral({status: 'InformationRequested'})),
    });

    expect(await screen.findByRole('heading', {name: 'Ask for More Information?'})).toBeInTheDocument();
    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('Dermatology')).toBeInTheDocument();
    expect(dialog.getByText('2 November 2026')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Ask for More Information'}));

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    expect(onDone.mock.calls[0][0]).toMatchObject({status: 'InformationRequested'});
    expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({expectedVersion: 2});
  });

  it("records that it came, showing a refusal in the API's words", async () => {
    const {user, onDone, onCancel} = render('received', {
      [`POST /api/referrals/${REFERRAL_ID}/information-received`]: () => json({title: 'Someone changed this referral meanwhile.'}, 409),
    });

    const dialog = within(await dialogOpen());
    expect(dialog.getByRole('heading', {name: 'Record Information Received?'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Information Received'}));
    expect(await dialog.findByText('Someone changed this referral meanwhile.')).toBeInTheDocument();
    expect(onDone).not.toHaveBeenCalled();
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });
});
