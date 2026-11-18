import {screen} from '@testing-library/react';
import {findAlert, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {REFERRAL_PATIENT_ID, testReferralSummary} from '../../test/referrals';
import {PatientReferrals} from './PatientReferrals';

describe('PatientReferrals', () => {
  it('lists the open referrals first, says how many more, and offers Register Referral', async () => {
    mockApi({
      'GET /api/referrals/search': () =>
        json({
          items: [
            testReferralSummary({id: 'closed', status: 'Withdrawn', specialtyName: 'Urology'}),
            testReferralSummary({id: 'open', specialtyName: 'Dermatology'}),
          ],
          totalCount: 23,
        }),
    });
    renderWithApp(<PatientReferrals patientId={REFERRAL_PATIENT_ID} canRegister />, {user: staffUser});

    const links = await screen.findAllByRole('link', {name: /^Referral to/});
    expect(links.map((link) => link.textContent)).toEqual(['Referral to Dermatology', 'Referral to Urology']);
    expect(screen.getByText('And 21 older referrals: the timeline has them all.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Register Referral'})).toHaveAttribute('href', `/referrals/new?patientId=${REFERRAL_PATIENT_ID}`);
  });

  it("says when there are none, and offers nothing to register while the record can't change", async () => {
    mockApi({'GET /api/referrals/search': () => json({items: [], totalCount: 0})});
    renderWithApp(<PatientReferrals patientId={REFERRAL_PATIENT_ID} canRegister={false} />, {user: staffUser});

    expect(await screen.findByText('No referrals yet.')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Register Referral'})).not.toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    mockApi({'GET /api/referrals/search': () => json({title: 'Down'}, 500)});
    renderWithApp(<PatientReferrals patientId={REFERRAL_PATIENT_ID} canRegister />, {user: staffUser});

    expect(await findAlert('The referrals could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
