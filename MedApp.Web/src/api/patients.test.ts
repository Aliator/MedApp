import {patientInput, toPatientRequest, UNLISTED_PRACTICE, type Patient, type PatientInput} from './patients';
import {NO_PRACTICE_ID} from './practices';

const base: PatientInput = {
  firstName: 'Freya',
  lastName: 'Ahmed',
  dateOfBirth: '1990-01-02',
  email: 'freya.ahmed18@example.com',
  nhsNumber: '',
  addressLines: ['', '', ''],
  postcode: '',
  mobilePhone: '',
  practiceId: '',
  unlistedPracticeName: '',
  unlistedPracticeAddressLines: ['', '', ''],
  unlistedPracticePostcode: '',
  unlistedPracticePhone: '',
  reminderChannel: 'None',
};

const unlisted: PatientInput = {
  ...base,
  practiceId: UNLISTED_PRACTICE,
  unlistedPracticeName: ' Nowhereville Health Centre ',
  unlistedPracticeAddressLines: ['2 Pretend Road', ' ', 'Fictionton'],
  unlistedPracticePostcode: 'zz9 9zz ',
  unlistedPracticePhone: '',
};

describe('a GP practice that is not listed', () => {
  it('starts the edit form from the patient: "Not listed here" with its details, or none', () => {
    const patient: Patient = {
      id: 'p1',
      firstName: 'Freya',
      unlistedPractice: {name: 'Nowhereville Health Centre', addressLines: ['2 Pretend Road'], postcode: 'ZZ9 9ZZ', phone: '01632 960111'},
    };
    expect(patientInput(patient)).toMatchObject({
      practiceId: UNLISTED_PRACTICE,
      unlistedPracticeName: 'Nowhereville Health Centre',
      unlistedPracticeAddressLines: ['2 Pretend Road', '', ''],
      unlistedPracticePostcode: 'ZZ9 9ZZ',
      unlistedPracticePhone: '01632 960111',
    });
    expect(patientInput({id: 'p2'})).toMatchObject({practiceId: '', unlistedPracticeName: '', unlistedPracticeAddressLines: ['', '', '']});
    expect(patientInput({id: 'p3', practiceId: 'p-test'}).practiceId).toBe('p-test');
  });

  it('sends a new patient its details trimmed, without a practice ID', () => {
    const request = toPatientRequest(unlisted, 'create');
    expect(request.practiceId).toBeUndefined();
    expect(request.unlistedPractice).toEqual({
      name: 'Nowhereville Health Centre',
      addressLines: ['2 Pretend Road', 'Fictionton'],
      postcode: 'zz9 9zz',
      phone: null,
    });
    expect(toPatientRequest(base, 'create').unlistedPractice).toBeUndefined();
  });

  it('on an edit, sends it only when newly chosen or changed, and the empty ID when it is removed', () => {
    const chosenNow = toPatientRequest(unlisted, 'update', {...base, practiceId: 'p-test'});
    expect(chosenNow.unlistedPractice?.name).toBe('Nowhereville Health Centre');
    expect(chosenNow.practiceId).toBeUndefined();

    expect(toPatientRequest(unlisted, 'update', unlisted).unlistedPractice).toBeUndefined();
    expect(toPatientRequest({...unlisted, unlistedPracticePhone: '01632 960111'}, 'update', unlisted).unlistedPractice?.phone).toBe('01632 960111');

    const removed = toPatientRequest({...unlisted, practiceId: ''}, 'update', unlisted);
    expect(removed.practiceId).toBe(NO_PRACTICE_ID);
    expect(removed.unlistedPractice).toBeUndefined();

    const directory = toPatientRequest({...unlisted, practiceId: 'p-test'}, 'update', unlisted);
    expect(directory.practiceId).toBe('p-test');
    expect(directory.unlistedPractice).toBeUndefined();
  });
});
