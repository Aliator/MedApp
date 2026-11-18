import {screen, within} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {testReferralSummary, UROLOGY_ID} from '../../test/referrals';
import {ReferralList} from './ReferralList';

describe('ReferralList', () => {
  it('lists each referral with its status, priority, when and from where, flagging the same specialty', async () => {
    const open = testReferralSummary({id: 'a', daysSinceReceived: 3});
    const closed = testReferralSummary({
      id: 'b',
      specialtyId: UROLOGY_ID,
      specialtyName: 'Urology',
      status: 'Withdrawn',
      requestedPriority: 'Low',
      referrerPractice: null,
    });
    renderWithApp(<ReferralList referrals={[open, closed]} label="Referrals" sameSpecialtyId={UROLOGY_ID} />, {user: staffUser});

    const list = await screen.findByRole('list', {name: 'Referrals'});
    expect(within(list).getByRole('link', {name: 'Referral to Dermatology'})).toHaveAttribute('href', '/referrals/a');
    expect(within(list).getByText('Received 2 Nov 2026 (3 days ago) from Synthetic Street Surgery')).toBeInTheDocument();
    expect(within(list).getByText('Received 2 Nov 2026')).toBeInTheDocument();
    expect(within(list).getByText('Withdrawn')).toBeInTheDocument();
    expect(within(list).getAllByText('Same specialty')).toHaveLength(1);
    expect(within(screen.getByText('Same specialty').closest('li')!).getByText('Referral to Urology')).toBeInTheDocument();
  });
});
