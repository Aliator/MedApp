import {screen, waitFor} from '@testing-library/react';
import {calls, json, mockApi, pageOf, renderWithApp, staffUser} from '../../test/render';
import {REFERRAL_PATIENT} from '../../test/referrals';
import {ADD_PATIENT_FOR_REFERRAL, patientSearchesFor, ReferralPatientFinder} from './ReferralPatientFinder';

describe('patientSearchesFor', () => {
  it('searches digits as an NHS number, one word as either name, and two as a first and last name', () => {
    expect(patientSearchesFor('999 000 0018')).toEqual([{NhsNumber: '9990000018'}]);
    expect(patientSearchesFor('Ahmed')).toEqual([{FirstName: 'Ahmed'}, {LastName: 'Ahmed'}]);
    expect(patientSearchesFor('Freya van Ahmed')).toEqual([{FirstName: 'Freya', LastName: 'van Ahmed'}]);
    expect(patientSearchesFor('   ')).toEqual([]);
  });
});

describe('ReferralPatientFinder', () => {
  it('finds a patient by NHS number, with their date of birth and number, and chooses them', async () => {
    const fetch = mockApi({'GET /api/patients/search': () => json(pageOf([REFERRAL_PATIENT]))});
    const onChoose = vi.fn();
    const {user} = renderWithApp(<ReferralPatientFinder onChoose={onChoose} />, {user: staffUser});

    await user.type(await screen.findByRole('combobox', {name: /Patient/}), '999 000 0018');
    const option = await screen.findByRole('option', {name: /Freya Ahmed/});
    expect(option).toHaveTextContent('Born 1 Dec 1976 · NHS No. 999 000 0018');
    expect(calls(fetch).some((call) => call.includes('NhsNumber=9990000018'))).toBe(true);
    await user.click(option);
    expect(onChoose).toHaveBeenCalledWith(REFERRAL_PATIENT.id);
  });

  it('offers Add Patient, first of all when nobody matches', async () => {
    mockApi({'GET /api/patients/search': () => json(pageOf([{...REFERRAL_PATIENT, dateOfBirth: null, nhsNumber: null}].slice(0, 0)))});
    const {user} = renderWithApp(<ReferralPatientFinder onChoose={vi.fn()} />, {user: staffUser});

    expect(await screen.findByText('Not in MedApp yet?')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Add Patient'})).toHaveAttribute('href', ADD_PATIENT_FOR_REFERRAL);
    await user.type(screen.getByRole('combobox', {name: /Patient/}), 'Nobody');
    await waitFor(() => expect(screen.getByText('Nobody matches: add them to MedApp first.')).toBeInTheDocument());
  });

  it('says when a patient has no date of birth or NHS number', async () => {
    mockApi({'GET /api/patients/search': () => json(pageOf([{...REFERRAL_PATIENT, dateOfBirth: null, nhsNumber: null}]))});
    const {user} = renderWithApp(<ReferralPatientFinder onChoose={vi.fn()} />, {user: staffUser});

    await user.type(await screen.findByRole('combobox', {name: /Patient/}), 'Freya');
    expect(await screen.findByRole('option', {name: /Freya Ahmed/})).toHaveTextContent('Date of birth not known · No NHS number');
  });
});
