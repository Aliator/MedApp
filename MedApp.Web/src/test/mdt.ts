import type {MdtItem, MdtMeeting} from '../api/mdt';

/** Synthetic MDTs and items for tests: invented people, 999 NHS numbers. */
export const mdtPatient = {id: 'patient-1', firstName: 'Test', lastName: 'Patient', dateOfBirth: '1960-05-04', nhsNumber: '9990000026'};

export const mdtMeeting = (overrides: Partial<MdtMeeting> = {}): MdtMeeting => ({
  id: '00000000-0000-4000-8000-0000000000a1',
  title: 'Skin MDT',
  startUtc: '2099-12-03T13:00:00Z',
  endUtc: '2099-12-03T14:00:00Z',
  specialtyId: '00000000-0000-4000-8000-0000000000b1',
  specialtyName: 'Dermatology',
  openItemCount: 1,
  itemCount: 1,
  attendees: [
    {userId: 'u-1', username: 'k.ashford'},
    {userId: 'u-2', username: 's.brightwell'},
  ],
  ...overrides,
});

export const mdtItem = (overrides: Partial<MdtItem> = {}): MdtItem => ({
  id: '00000000-0000-4000-8000-0000000000c1',
  status: 'Listed',
  patient: mdtPatient,
  pathwayId: 'proc-1',
  pathwayTitle: 'Skin lesion',
  specialtyId: '00000000-0000-4000-8000-0000000000b1',
  specialtyName: 'Dermatology',
  meetingId: '00000000-0000-4000-8000-0000000000a1',
  meetingTitle: 'Skin MDT',
  meetingStartUtc: '2099-12-03T13:00:00Z',
  question: 'Synthetic question for the team.',
  listedByUsername: 'k.ashford',
  listedAtUtc: '2026-11-18T10:00:00Z',
  present: [],
  version: 1,
  canWithdraw: true,
  canDefer: true,
  canRecordOutcome: false,
  ...overrides,
});
