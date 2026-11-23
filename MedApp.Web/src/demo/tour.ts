import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {ADMIN, LAB, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, TEAMS} from './ids';
import {LAB_CASE_IDS} from './slices/labCases';
import type {DemoState} from './state';

/** One step of the walkthrough: the exact page or record it opens, as the right account. */
export interface TourStep {
  key: string;
  /** The part of the tour it's in (`TOUR_PARTS`): a part's steps are together, in the parts' order. */
  part: TourPart;
  title: string;
  /** The account the step needs (it switches to it first), or undefined for either. */
  account?: string;
  /** The page to open: a path in the app, with its query; none stays where it is. */
  path?: string;
  /** The account a step with no page of its own (the notepad's) is done as. */
  switchTo?: string;
  /** Synthetic sample text the walkthrough offers to copy (the notepad's notes), one line each. */
  sampleText?: string[];
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
  {key: 'start', title: 'Getting started', intro: 'Find your way around, and meet the accounts.'},
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
    path: '/account',
  },
  {
    key: 'pin-patient',
    part: 'start',
    title: 'Pin a patient',
    path: `/patients/${PATIENTS.samuelOkafor}`,
    whyNot: () =>
      isPatientPinned(PATIENTS.samuelOkafor)
        ? `You've pinned Samuel Okafor already: he's under Patients in the side nav. Unpin him from his row's menu (the burger at the end of the row) to try again.`
        : null,
  },
  {
    key: 'team-walls',
    part: 'start',
    title: 'Teams keep their records apart',
    account: LAB.username,
    path: `/patients/${PATIENTS.tobiasLund}`,
  },

  // The relay (the owner, 19 November 2026): one step for each of its first three stages.
  {
    key: 'relay-triage',
    part: 'story',
    title: 'The relay, 1: consent, then finish Triage',
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.relay}`,
    whyNot: (state) =>
      relayStage(state, 'Triage')?.completedAtUtc
        ? `You've finished her Triage already in your copy of the demo: the pathway is with the lab. ${RESET_HINT}`
        : null,
  },
  {
    key: 'relay-lab',
    part: 'story',
    title: 'The relay, 2: the lab enters the results and sends it back',
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.relay}`,
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
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.relay}`,
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
    account: ADMIN.username,
    path: `/calendar?view=team&team=${TEAMS.dermatology}`,
  },
  {
    key: 'meeting-seen',
    part: 'around',
    title: 'See the meeting as the person invited',
    account: STAFF.username,
    path: '/calendar',
  },
  {
    key: 'on-hold-overdue',
    part: 'around',
    title: 'Every team’s pathways, and the team picker',
    account: ADMIN.username,
    path: '/pathways',
  },
  {
    key: 'notepad',
    part: 'around',
    title: 'The notepad',
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
    path: '/account',
  },
  {
    key: 'colours',
    part: 'around',
    title: 'Colours',
    path: '/account/colours',
  },
  {
    key: 'insights',
    part: 'around',
    title: 'Insights',
    account: ADMIN.username,
    path: '/admin/insights',
  },
];
