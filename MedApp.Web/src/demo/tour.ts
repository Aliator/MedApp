import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {ADMIN, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, REFERRALS} from './ids';
import {CLINICAL_IDS} from './slices/clinical';
import {LAB_IDS} from './slices/lab';
import type {DemoState} from './state';

/** One step of the Read Me's guided tour: a button that opens the exact page or record, as the right account. */
export interface TourStep {
  key: string;
  title: string;
  /** What to do there, in short steps. */
  steps: string[];
  /** The account the step needs (it switches to it first), or undefined for either. */
  account?: string;
  /** The page to open: a path in the app, with its query; none stays on the Read Me. */
  path?: string;
  /** Opens the notepad beside the page. */
  opensNotepad?: boolean;
  /** Stays on the Read Me, on its own (closing any half view), for steps that start from it. */
  staysOnReadMe?: boolean;
  /** Its steps are numbered, as they're done in order. */
  isNumbered?: boolean;
  buttonLabel: string;
  /** Why the step can't do what it says in this copy of the demo (already done, say), or null when it can. */
  whyNot?: (state: DemoState) => string | null;
}

const RESET_HINT = 'Reset (in the banner at the top of the Read Me) brings it back.';

/**
 * Whether the signed-in person has pinned a patient: their pins as this browser holds them (`medapp.pins`, what the
 * side nav shows), which the stand-in saves with the account a moment after each change.
 */
export function isPatientPinned(patientId: string): boolean {
  try {
    const pins: unknown = JSON.parse(appSettings.get(PINS_KEY) ?? '{}');
    const patients = pins && typeof pins === 'object' ? (pins as {patients?: unknown}).patients : undefined;
    return Array.isArray(patients) && patients.includes(patientId);
  } catch {
    return false;
  }
}

export const TOUR: TourStep[] = [
  {
    key: 'half-view',
    title: 'Two pages side by side',
    steps: [
      'Rest the pointer on Patients in the side nav and open its menu (the … at the end of the row), then choose Open in half view. Patients opens on the right, beside this Read Me.',
      'Close it with the close button (×) at the top of the Patients half.',
      'Shift-click Patients in the side nav: it opens on the right again.',
      'Close it again.',
      'Ctrl-click (⌘-click on a Mac) Patients: it opens on the left, and this Read Me moves to the right.',
      'Keep the Read Me there and carry on with the tour from it: each button opens its page on the left, beside the Read Me.',
      'With two pages open the side nav folds to its icons. Open it from the side with the expand button (Expand sidebar) at the bottom of the icon rail. To stop it folding, turn off Fold the side nav in half view in Account Settings.',
    ],
    isNumbered: true,
    staysOnReadMe: true,
    buttonLabel: 'Start here, on the Read Me',
  },
  {
    key: 'pin-patient',
    title: 'Pin a patient',
    steps: [
      'Opens Samuel Okafor’s record. While it’s open, he shows in the side nav, indented under Patients.',
      'Rest the pointer on his row there and open its menu (the … at the end of the row), then choose Pin.',
      'Go to another page, Home say: he stays under Patients. Click his name there to open him again. Unpin is in the same menu.',
      'Pins belong to the account (up to five patients and five pathways), so they follow you to another browser.',
    ],
    path: `/patients/${PATIENTS.samuelOkafor}`,
    buttonLabel: 'Open the patient to pin',
    whyNot: () =>
      isPatientPinned(PATIENTS.samuelOkafor)
        ? `You've pinned Samuel Okafor already: he's under Patients in the side nav. Unpin him from his row's menu (the …) to try again.`
        : null,
  },
  {
    key: 'meeting',
    title: 'Arrange a meeting',
    steps: [
      'As the admin, choose New, then New meeting, on the Calendar.',
      `Invite ${STAFF.fullName} (${STAFF.username}), pick one of the suggested times, and save.`,
      'Then do the next step to see it on her calendar.',
    ],
    account: ADMIN.username,
    path: '/calendar',
    buttonLabel: 'Open the Calendar as the admin',
  },
  {
    key: 'meeting-seen',
    title: 'See the meeting as the person invited',
    steps: [
      `Switches to ${STAFF.fullName} and opens her calendar.`,
      'The meeting is on it, and the bell (top left) has the invitation. Open it to accept or decline.',
    ],
    account: STAFF.username,
    path: '/calendar',
    buttonLabel: `Open ${STAFF.fullName}'s calendar`,
  },
  {
    key: 'book',
    title: 'Book an appointment',
    steps: [
      'Opens Ellis Varden’s skin lesion pathway: its Biopsy or sample stage needs a visit.',
      'Choose Book Visit on that stage, pick who and a free time, and book it.',
      'It shows on the stage, on the patient’s timeline and on the Calendar.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.biopsyToBook}`,
    buttonLabel: 'Open the pathway to book its visit',
    whyNot: (state) =>
      state.appointments.some((a) => a.pathwayId === PATHWAYS.biopsyToBook && a.stageName === 'Biopsy or sample' && a.status === 'Booked')
        ? `You've booked this visit already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'triage',
    title: 'Triage a referral',
    steps: [
      `As ${STAFF.fullName}, a dermatologist, open Oliver Maddox's urgent referral to Dermatology.`,
      'Choose Accept, pick a template and a priority (nothing is chosen for you), check, and confirm.',
      'MedApp opens the new pathway. Reject and Redirect work the same way.',
    ],
    account: STAFF.username,
    path: `/referrals/${REFERRALS.dermatologyToTriage}`,
    buttonLabel: 'Open the referral to triage it',
    whyNot: (state) =>
      state.referrals.find((r) => r.id === REFERRALS.dermatologyToTriage)?.status === 'AwaitingTriage'
        ? null
        : `You've triaged this referral already in your copy of the demo: it opens with the decision. ${RESET_HINT}`,
  },
  {
    key: 'finish-stage',
    title: 'Finish a stage',
    steps: [
      'Opens Priya Holloway’s pathway, at Histology (lab).',
      'Write a note on the stage if you like, then choose Finish Stage. The next stage becomes current.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.histologyToFinish}`,
    buttonLabel: 'Open the pathway at its current stage',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.histologyToFinish)?.stages.find((s) => s.name === 'Histology (lab)')?.completedAtUtc
        ? `You've finished Histology already in your copy of the demo; the next stage is current now. ${RESET_HINT}`
        : null,
  },
  {
    key: 'sign-off',
    title: 'Sign off a result',
    steps: [
      `As ${STAFF.fullName}, open Priya Holloway’s biopsy result: the lab’s report, waiting for her sign-off.`,
      'Choose Sign Off and say what’s been done about it. Nothing is chosen for you, and MedApp never reads or flags a result.',
      'Her Results and team review stage waits for it: it can’t be finished while a result awaits sign-off.',
    ],
    account: STAFF.username,
    path: `/results/${LAB_IDS.priyaResult}`,
    buttonLabel: 'Open the result to sign off',
    whyNot: (state) =>
      state.results.find((r) => r.id === LAB_IDS.priyaResult)?.status === 'AwaitingSignOff'
        ? null
        : `You've signed off this result already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'lab',
    title: 'The specimen worklist',
    steps: [
      'The Lab page: specimens to send, expected at the lab, in the lab, overdue and reported.',
      'Open one to mark it sent, received, rejected or lost; each step follows the specimen’s status. Find one by its number with the search.',
      'A pathway’s last stage can’t be finished while any of its specimens is still with the lab.',
    ],
    account: ADMIN.username,
    path: '/lab',
    buttonLabel: 'Open the Lab worklist',
  },
  {
    key: 'consent',
    title: 'Record consent, then finish the stage',
    steps: [
      'Opens Harriet Quill’s pathway at Treatment: the excision was done this morning.',
      'Choose Finish Stage first: it’s refused, because the stage needs a consent record. That’s a stage gate, with no override.',
      'In the Consent card, record the consent: the form, who signed and the date. MedApp records what you say; it never judges capacity.',
      'Now Finish Stage works.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.consentToRecord}`,
    buttonLabel: 'Open the pathway that needs consent',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.consentToRecord)?.stages.find((s) => s.name === 'Treatment')?.completedAtUtc
        ? `You've finished Treatment already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'branch',
    title: 'Choose a branch',
    steps: [
      'Opens Felix Moreton’s pathway at its first visit, a branch point: biopsy first, or treat directly.',
      'Choose Finish Stage and pick the branch. Nothing is chosen for you. The other branch’s stages are skipped.',
      'Follow-up is optional: skip it with a reason when you reach it.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.branchToChoose}`,
    buttonLabel: 'Open the pathway at its branch point',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.branchToChoose)?.stages.some((s) => s.chosenBranch)
        ? `You've chosen this pathway's branch already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'mdt',
    title: 'Record an MDT outcome',
    steps: [
      'Opens this morning’s skin MDT: the pathways listed for the team to discuss.',
      'Choose Record Outcome on Ellis Varden’s item, tick who was there and write the team’s outcome. MedApp never suggests one.',
      `${STAFF.fullName}, who has the pathway, is told in her notifications.`,
      'The MDT list is reached from the Calendar page, by its MDT button.',
    ],
    account: ADMIN.username,
    path: `/mdt?meeting=${CLINICAL_IDS.mdtToday}`,
    buttonLabel: 'Open this morning’s MDT',
    whyNot: (state) =>
      state.mdtItems.find((i) => i.id === CLINICAL_IDS.mdtItemToDiscuss)?.status === 'Listed'
        ? null
        : `This item has been discussed already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'timeline',
    title: 'A patient’s timeline',
    steps: [
      'Opens Priya Holloway: everything that happened to her, newest first, across her pathways, appointments and referrals.',
      'Filter it by kind (stages, appointments, referrals) with the chips above it.',
    ],
    path: `/patients/${PATIENTS.priyaHolloway}`,
    buttonLabel: 'Open the patient’s timeline',
  },
  {
    key: 'on-hold-overdue',
    title: 'On hold and overdue',
    steps: [
      'The Pathways list shows every open pathway, with its status, target date and progress.',
      'Samuel Okafor’s is on hold; Margaret Fenwick’s is overdue. Sort and filter with the bar above the list.',
    ],
    path: '/pathways',
    buttonLabel: 'Open the list of pathways',
  },
  {
    key: 'notepad',
    title: 'The notepad',
    steps: [
      'Opens your notepad beside this page. It stays open as you move around the app.',
      'Jot anything down: it saves as you type. Each account has its own.',
      'Shift-click Notepad in the side nav to open it in the half view on the right instead.',
    ],
    opensNotepad: true,
    buttonLabel: 'Open the notepad',
  },
  {
    key: 'accessibility',
    title: 'Accessibility settings',
    steps: [
      'Text size, high contrast, reduced motion, an easy-to-read font and more, in Account Settings.',
      'They apply at once, and the full version keeps them with your account.',
    ],
    path: '/account',
    buttonLabel: 'Open the accessibility settings',
  },
  {
    key: 'colours',
    title: 'Colours',
    steps: ['Choose the app’s colours, light or dark, or a colour theme.'],
    path: '/account/colours',
    buttonLabel: 'Open the colour settings',
  },
  {
    key: 'insights',
    title: 'Insights',
    steps: [
      'As the admin: open, in progress and overdue pathways, who has overdue work, how long each stage takes, and the weekly flow.',
      'This week’s counts come from the demo’s pathways; the history behind the charts is made up for the light version.',
    ],
    account: ADMIN.username,
    path: '/admin/insights',
    buttonLabel: 'Open Insights as the admin',
  },
];

/** What the light version leaves out, each with what the full version does. */
export const NOT_IN_THE_LIGHT_VERSION: {title: string; full: string}[] = [
  {title: 'Real-time notifications', full: 'New notifications arrive at once on any open page, over a live connection (SignalR).'},
  {title: 'Real sign-in and two-factor', full: 'Passwords, sessions that time out, account lockout, and codes from an authenticator app or email.'},
  {title: 'Email', full: 'Password reset, confirming an address, and sign-in codes by email.'},
  {
    title: 'File uploads',
    full: 'Attach files to patients, pathways and referrals, with thumbnails, type checks from the contents, and virus scanning.',
  },
  {
    title: 'Sharing',
    full: 'Everyone works on the same records. Here, nobody else sees your changes, and nothing follows you to another device.',
  },
  {
    title: 'The server’s rules',
    full: 'Every rule is checked on the server: calendar clashes, permissions, locks so two people can’t book the same slot, and concurrency. Here they’re simplified.',
  },
  {title: 'Server rendering', full: 'Pages are rendered on the server, so they arrive with their data and show at once.'},
  {
    title: 'The activity log, exports and the data-request pack',
    full: 'Every read and change of a patient’s record is logged as it happens, admins download everything held about a patient, and a data request is answered with a pack made on the server: a PDF report, the record as JSON and the files. Here the logs are worked out from the demo’s records, the export page only shows what would be in it, and the pack can’t be downloaded.',
  },
  {title: 'The demographics lookup and NHS links', full: 'Patients’ details looked up from the national demographics service (PDS), and FHIR and SNOMED CT links.'},
  {
    title: 'Background jobs',
    full: 'Appointment reminders to patients, service alerts (admins told of referrals not triaged, results not signed off, visits not booked and specimens overdue in time), purges of old operational data, and other jobs on a schedule. Here the Scheduled Jobs page lists them, but nothing runs.',
  },
  {
    title: 'Sending letters',
    full: 'Approved letters are sent by email or post, and their PDFs made on the server. Here a letter can be written, approved and recorded as sent by hand, but not sent or printed.',
  },
  {
    title: 'Changing roles and accounts',
    full: 'Admins add staff, reset passwords and two-factor, and change roles and their permissions. Here the two accounts are fixed.',
  },
  {
    title: 'Files from the lab',
    full: 'A specimen’s report and an external result arrive as files, and a specimen’s label has a QR code. Here only the seeded reports are there.',
  },
];
