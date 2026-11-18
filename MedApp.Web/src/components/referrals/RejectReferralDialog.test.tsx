import {screen, waitFor, within} from '@testing-library/react';
import {calls, isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {REFERRAL_ID, testReferral} from '../../test/referrals';
import {RejectReferralDialog} from './RejectReferralDialog';

const REJECT = `POST /api/referrals/${REFERRAL_ID}/reject`;
const clinician = userWithRoles('d.clinician', 'Clinician');
const rejected = testReferral({status: 'Rejected', isOpen: false, rejectionReason: 'Other'});

function render(handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({[REJECT]: () => json(rejected), [`GET /api/referrals/${REFERRAL_ID}`]: () => json(testReferral()), ...handlers});
  const onCancel = vi.fn();
  const onRejected = vi.fn();
  const result = renderWithApp(<RejectReferralDialog isOpen referral={testReferral({canTriage: true})} onCancel={onCancel} onRejected={onRejected} />, {
    user: clinician,
  });
  return {...result, fetch, onCancel, onRejected};
}

const dialog = () => within(openDialog()!);
const posted = async (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!).json();

async function chooseReason(user: ReturnType<typeof render>['user'], reason: string) {
  await user.click(dialog().getByRole('combobox', {name: /Reason/}));
  await user.click(await screen.findByRole('option', {name: reason}));
}

describe('RejectReferralDialog', () => {
  it('starts with no reason chosen, lists the reasons in words, and needs a detail for Other', async () => {
    const {user} = render();

    expect(await screen.findByRole('heading', {name: 'Reject Referral'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Reason/})).toHaveTextContent('Choose a reason');
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);

    await user.click(dialog().getByRole('combobox', {name: /Reason/}));
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual([
      'Not offered here',
      'Does not meet the criteria',
      'Insufficient information',
      'Duplicate',
      'Other',
    ]);
    await user.click(screen.getByRole('option', {name: 'Other'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Detail/}), 'Synthetic: seen privately.');
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(false);
  });

  it('checks the reason with the patient shown, then rejects, the detail left out when blank', async () => {
    const {user, fetch, onRejected} = render();

    await screen.findByRole('heading', {name: 'Reject Referral'});
    await chooseReason(user, 'Duplicate');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));

    expect(await screen.findByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('Dermatology')).toBeInTheDocument();
    expect(dialog().getByText('Duplicate')).toBeInTheDocument();
    expect(dialog().getByText('None')).toBeInTheDocument();
    // Back to the form, the reason kept, and on again.
    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(await screen.findByRole('heading', {name: 'Reject Referral'})).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Reason/})).toHaveTextContent('Duplicate');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await dialog().findByRole('button', {name: 'Reject Referral'}));

    await waitFor(() => expect(onRejected).toHaveBeenCalledWith(rejected));
    expect(await posted(fetch)).toEqual({reason: 'Duplicate', detail: null, expectedVersion: 2});
  });

  it('says when the detail is too long', async () => {
    const {user} = render();

    await screen.findByRole('heading', {name: 'Reject Referral'});
    await chooseReason(user, 'Duplicate');
    await user.click(dialog().getByRole('textbox', {name: /Detail/}));
    await user.paste('x'.repeat(501));
    expect(dialog().getByText('At most 500 characters.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);
  });

  it('goes back to the form with a 400, and shows a 409 after reading the referral again', async () => {
    let attempt = 0;
    const {user, fetch} = render({
      [REJECT]: () =>
        ++attempt === 1
          ? json({title: 'Validation failed', status: 400, errors: {Detail: ['Say why.']}}, 400)
          : json({title: 'k.ashford has already accepted this referral.', status: 409}, 409),
    });

    await screen.findByRole('heading', {name: 'Reject Referral'});
    await chooseReason(user, 'Duplicate');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Reject Referral'}));
    expect(await dialog().findByText('Say why.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Reject Referral'})).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Reject Referral'}));
    expect(await dialog().findByText('k.ashford has already accepted this referral.')).toBeInTheDocument();
    expect(calls(fetch).filter((c) => c === REJECT)).toHaveLength(2);
  }, 15_000);
});
