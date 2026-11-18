import {ADMIN, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, REFERRALS} from './ids';
import type {DemoState} from './state';

/** One step of the Read Me's guided tour: a button that opens the exact page or record, as the right account. */
export interface TourStep {
  key: string;
  title: string;
  /** What to do there, in short steps. */
  steps: string[];
  /** The account the step needs (it switches to it first), or undefined for either. */
  account?: string;
  /** The page to open: a path in the app, with its query. */
  path: string;
  /** A page to open beside it, in half view. */
  halfView?: string;
  /** Opens the notepad beside the page. */
  opensNotepad?: boolean;
  buttonLabel: string;
  /** Why the step can't do what it says in this copy of the demo (already done, say), or null when it can. */
  whyNot?: (state: DemoState) => string | null;
}

const RESET_HINT = 'Reset (in the banner at the top) brings it back.';

export const TOUR: TourStep[] = [
  {
    key: 'half-view',
    title: 'Two pages side by side',
    steps: [
      'Opens a patient on the left and their pathway on the right, in half view.',
      'Each half scrolls on its own. Close either half to go back to one page.',
      'Any page in the side nav opens in half view from its menu (the … beside it).',
    ],
    path: `/patients/${PATIENTS.ellisVarden}`,
    halfView: `/pathways/${PATHWAYS.biopsyToBook}`,
    buttonLabel: 'Open a pathway beside its patient',
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
      'Opens your notepad beside the page. It stays open as you move around the app.',
      'Jot anything down: it saves as you type. Each account has its own.',
    ],
    path: '/',
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
  {title: 'The activity log and exports', full: 'Every read and change of a patient’s record is logged, and admins can export everything held about a patient.'},
  {title: 'The demographics lookup and NHS links', full: 'Patients’ details looked up from the national demographics service (PDS), and FHIR and SNOMED CT links.'},
  {title: 'Background jobs', full: 'Appointment reminders to patients, purges of old operational data, and other jobs on a schedule.'},
  {title: 'Letters, the lab and setup', full: 'Letters from templates, specimens and the lab’s worklist, and the setup pages (templates, sites, roles).'},
];
