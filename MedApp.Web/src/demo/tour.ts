import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {ADMIN, LAB, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, TEAMS} from './ids';
import {LAB_CASE_IDS} from './slices/labCases';
import type {DemoState} from './state';
import {db} from './store';

/** One step of the walkthrough: the exact page or record it opens, as the right account. */
export interface TourStep {
  key: string;
  /** The part of the tour it's in (`TOUR_PARTS`): a part's steps are together, in the parts' order. */
  part: TourPart;
  title: string;
  /** What to do there, in short steps. */
  steps: string[];
  /** The account the step needs (it switches to it first), or undefined for either. */
  account?: string;
  /** The page to open: a path in the app, with its query; none stays where it is. */
  path?: string;
  /**
   * An account to switch to from the step itself (a "Switch to …" button above its sample text), for a step with no
   * page of its own to open: the notepad's, done as that account.
   */
  switchTo?: string;
  /** Its steps are numbered, as they're done in order. */
  isNumbered?: boolean;
  /** Synthetic sample text shown under the steps to select or copy (the notepad's notes), one line each. */
  sampleText?: string[];
  /** The step's button, opening its page; none for a step done from the app itself (the notepad's). */
  buttonLabel?: string;
  /** Why the step can't do what it says in this copy of the demo (already done, say), or null when it can. */
  whyNot?: (state: DemoState) => string | null;
}

const RESET_HINT = 'Reset the Demo, below, brings it back.';

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

/** The tour's parts, in order. */
export const TOUR_PARTS = [
  {key: 'start', title: 'Getting started', intro: 'Find your way around, and meet the three accounts.'},
  {
    key: 'story',
    title: 'The relay',
    intro:
      'From the clinic to the lab and back. Leah Corrin’s pathway has Triage (Dermatology’s), Testing (the lab’s) and a Results appointment (Dermatology’s), then a referral back to her GP for medication.',
  },
  {key: 'around', title: 'Around the app'},
] as const;

export type TourPart = (typeof TOUR_PARTS)[number]['key'];

/** A stage of the relay's pathway (Leah Corrin's), by name. */
const relayStage = (state: DemoState, name: string) => state.pathways.find((p) => p.id === PATHWAYS.relay)?.stages.find((s) => s.name === name);

export const TOUR: TourStep[] = [
  // Getting started.
  {
    key: 'half-view',
    part: 'start',
    title: 'Two pages side by side',
    steps: [
      'Rest the pointer on Patients in the side nav and open its menu (the burger at the end of the row), then choose Open in half view on the right. Patients opens on the right, beside this page.',
      'Close it with the close button (×) at the top of the Patients half.',
      'Shift-click Patients in the side nav: it opens on the right again.',
      'Close it again.',
      'Ctrl-click (⌘-click on a Mac) Patients: it opens on the left, and this page moves to the right.',
      'With two pages open, the side nav folds to its icons: open it from the side with the expand button (Expand sidebar) at the bottom of the icon rail.',
      'This folding is a setting in Account Settings (Shrink the side nav when two pages are open), on by default.',
    ],
    isNumbered: true,
    path: '/account',
    buttonLabel: 'Open Account Settings',
  },
  {
    key: 'pin-patient',
    part: 'start',
    title: 'Pin a patient',
    steps: [
      'Opens Samuel Okafor’s record. While it’s open, he shows in the side nav, indented under Patients.',
      'Rest the pointer on his row there and open its menu (the burger at the end of the row), then choose Pin.',
      'Go to another page, Home say: he stays under Patients. Click his name there to open him again.',
    ],
    path: `/patients/${PATIENTS.samuelOkafor}`,
    buttonLabel: 'Open the patient to pin',
    whyNot: () =>
      isPatientPinned(PATIENTS.samuelOkafor)
        ? `You've pinned Samuel Okafor already: he's under Patients in the side nav. Unpin him from his row's menu (the burger at the end of the row) to try again.`
        : null,
  },
  {
    key: 'team-walls',
    part: 'start',
    title: 'Teams keep their records apart',
    steps: [
      `Opens Tobias Lund as ${LAB.fullName}, from the lab. Patients are shared across the centre.`,
      'Each team, a clinic or the lab, sees only its own pathways in full. His Dermatology pathway shows here as a one-line summary.',
      'The admin can read every team’s records, but changes nothing clinical.',
    ],
    account: LAB.username,
    path: `/patients/${PATIENTS.tobiasLund}`,
    buttonLabel: 'Open the patient as the lab',
  },

  // The relay (the owner, 19 November 2026): one step for each of its first three stages.
  {
    key: 'relay-triage',
    part: 'story',
    title: 'The relay, 1: consent, then finish Triage',
    steps: [
      `As ${STAFF.fullName}, this opens Leah Corrin’s pathway, at Triage.`,
      'Pin it: it shows in the side nav under Pathways while it’s open. Open its row’s menu (the burger at the end of the row) and choose Pin. You’ll come back to it from there with the notepad later.',
      'She was seen yesterday, and a skin scraping was taken with her verbal agreement, but her consent was never recorded, so Triage can’t be finished yet.',
      'Record the consent she gave at yesterday’s visit in the Consent card (the form, who signed, the date it was given).',
      'Choose Finish Stage on Triage. The next stage, Testing, is the lab’s, so the pathway goes to the lab.',
    ],
    isNumbered: true,
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.relay}`,
    buttonLabel: 'Open the pathway at Triage',
    whyNot: (state) =>
      relayStage(state, 'Triage')?.completedAtUtc
        ? `You've finished her Triage already in your copy of the demo: the pathway is with the lab. ${RESET_HINT}`
        : null,
  },
  {
    key: 'relay-lab',
    part: 'story',
    title: 'The relay, 2: the lab enters the results and sends it back',
    steps: [
      `Switches to ${LAB.fullName}, from the lab. This opens Leah Corrin’s lab case: her sample is in, and its steps are done.`,
      'Choose Enter Results and Authorise, type the results, and choose Authorise and Open Pathway. The results go on the lab’s Testing stage, and her pathway opens.',
      'On the pathway, choose Finish Stage on Testing. That sends it back to Dermatology.',
    ],
    isNumbered: true,
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.relay}`,
    buttonLabel: 'Open the lab case as the lab',
    whyNot: (state) =>
      relayStage(state, 'Testing')?.completedAtUtc
        ? `You've sent it back already in your copy of the demo: the pathway is with Dermatology. ${RESET_HINT}`
        : state.labCases.find((c) => c.id === LAB_CASE_IDS.relay)?.status !== 'Open'
          ? 'You’ve entered the results already: open her pathway and choose Finish Stage on Testing to send it back.'
          : relayStage(state, 'Triage')?.completedAtUtc
            ? null
            : 'Finish her Triage first (the step before): until then the pathway isn’t with the lab.',
  },
  {
    key: 'relay-back',
    part: 'story',
    title: 'The relay, 3: back to Dermatology, book the appointment',
    steps: [
      `As ${STAFF.fullName}, Leah Corrin’s pathway is back from the lab, with the results on Testing. The bell has it too.`,
      'Results appointment needs a visit: choose Book Appointment on it, pick who and a free time, and book it.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.relay}`,
    buttonLabel: 'Open the pathway back from the lab',
    whyNot: (state) =>
      !relayStage(state, 'Testing')?.completedAtUtc
        ? 'The lab hasn’t sent it back yet: do the step before first.'
        : state.appointments.some((a) => a.pathwayId === PATHWAYS.relay && a.stageName === 'Results appointment' && a.status === 'Booked')
          ? `You've booked her appointment already in your copy of the demo. ${RESET_HINT}`
          : null,
  },

  // Around the app.
  {
    key: 'team-calendar',
    part: 'around',
    title: 'Team calendars: find a time for a meeting',
    steps: [
      'As the admin, this opens Dermatology’s team calendar: everyone in the team side by side. Anyone can look at any team’s, picked in Team.',
      `Choose New, then New Meeting. Give it a title, and in Who add one person: ${STAFF.username} (${STAFF.fullName}).`,
      'Pick the first suggested time.',
      'Now add the rest of the team to Who: b.sandiford and t.hallworth. The dialog says not everyone is free then: Bea Sandiford is at an outreach clinic.',
      `The suggested times now fit everyone (Tom Hallworth works Tuesday to Thursday). Pick another one, and save. The next step shows it on ${STAFF.fullName}’s calendar.`,
    ],
    isNumbered: true,
    account: ADMIN.username,
    path: `/calendar?view=team&team=${TEAMS.dermatology}`,
    buttonLabel: 'Open Dermatology’s calendar as the admin',
  },
  {
    key: 'meeting-seen',
    part: 'around',
    title: 'See the meeting as the person invited',
    steps: [`Switches to ${STAFF.fullName}: the meeting is on her calendar, and the bell has the invitation to accept or decline.`],
    account: STAFF.username,
    path: '/calendar',
    buttonLabel: `Open ${STAFF.fullName}'s calendar`,
  },
  {
    key: 'on-hold-overdue',
    part: 'around',
    title: 'Every team’s pathways, and the team picker',
    steps: [
      'As the admin, Pathways lists every team’s open pathways. The picker narrows it to Dermatology or the lab.',
      'Samuel Okafor’s is on hold. Harriet Quill’s is past its target date: its last stage waits for her referral letter to be approved and sent. Sort and filter with the bar above the list.',
    ],
    account: ADMIN.username,
    path: '/pathways',
    buttonLabel: 'Open the list of pathways',
  },
  {
    key: 'notepad',
    part: 'around',
    title: 'The notepad',
    steps: [
      `Do this as ${STAFF.fullName}: not signed in as her? Choose Switch to ${STAFF.username} below. Then copy the sample notes below: select them, or choose Copy the Notes.`,
      'Open the notepad from Notepad in the side nav (near the bottom): it opens beside the page you’re on and stays open as you move around the app. Paste the notes into it; it saves as you type, and each account has its own.',
      'Or Shift-click Notepad in the side nav, to open it in the half view on the right instead.',
      'Open Leah Corrin’s pathway, Scaly patch, left forearm, from its pin under Pathways in the side nav (you pinned it in the relay). Not pinned? Patients, then Leah Corrin, then the pathway.',
      'In the notepad, choose Add Note to Pathway, check it’s her pathway, and add it. The pathway then shows the note under Notes, by you.',
    ],
    isNumbered: true,
    switchTo: STAFF.username,
    sampleText: [
      'Seen yesterday: scaly patch on the left forearm, about 3 cm, itchy for two months.',
      'Skin scraping taken and sent to the lab.',
      'To go through the results at the results appointment.',
    ],
  },
  {
    key: 'accessibility',
    part: 'around',
    title: 'Accessibility settings',
    steps: [
      'Text size, High contrast, Reduce motion, Easy-to-read font and more, in Account Settings.',
      'They apply at once, and the full version keeps them with your account.',
    ],
    path: '/account',
    buttonLabel: 'Open the accessibility settings',
  },
  {
    key: 'colours',
    part: 'around',
    title: 'Colours',
    steps: ['Choose the app’s colours, light or dark, or a colour theme.'],
    path: '/account/colours',
    buttonLabel: 'Open the colour settings',
  },
  {
    key: 'insights',
    part: 'around',
    title: 'Insights',
    steps: [
      'As the admin: open, in progress and overdue pathways, who has overdue work, how long each stage takes, and the weekly flow, for every team or the one picked. Staff see only their own teams’.',
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
    title: 'Reports as files',
    full: 'A lab case’s report is a file the lab attaches and authorises, an external result arrives as a file, and a hospital’s report goes back as a letter (a PDF) with the report attached. Here results are typed in as text, and the hospital’s report is recorded as sent.',
  },
  {
    title: 'File uploads',
    full: 'Attach files to patients, pathways and referrals, with thumbnails, type checks from the contents, and virus scanning; mark a picture as a clinical photo, only with the patient’s consent to clinical photography (the consent form is here).',
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
  {
    title: 'Background jobs',
    full: 'Appointment reminders to patients, service alerts (admins told of referrals not triaged, results not signed off, visits not booked, and samples and lab cases overdue), purges of old operational data, and other jobs on a schedule. Here the Scheduled Jobs page lists them, but nothing runs.',
  },
  {
    title: 'Sending letters',
    full: 'Approved letters are sent by email or post, and their PDFs made on the server. Here a letter can be written, approved and recorded as sent by hand, but not sent or printed.',
  },
  {
    title: 'Changing roles and accounts',
    full: 'Admins add staff, reset passwords and two-factor, and change roles and their permissions. Here the three accounts are fixed, though their teams can be changed.',
  },
];

const RECORDS: Record<string, (id: string) => boolean> = {
  patients: (id) => db().patients.some((p) => p.id === id),
  pathways: (id) => db().pathways.some((p) => p.id === id),
  referrals: (id) => db().referrals.some((r) => r.id === id),
};

export function whyStepCantOpen(step: TourStep): string | null {
  const [, kind, id] = (step.path ?? '').split(/[/?]/);
  if (id && RECORDS[kind] && !RECORDS[kind](id)) return 'That record isn’t in your copy of the demo any more. Reset the Demo, below, brings it back.';
  return step.whyNot?.(db()) ?? null;
}
