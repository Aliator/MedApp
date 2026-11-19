import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {ADMIN, LAB, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, REFERRALS, TEAMS, TEMPLATE_BRANCHES, TEMPLATES} from './ids';
import {CLINICAL_IDS} from './slices/clinical';
import {HOSPITAL_IDS} from './slices/hospitals';
import {LAB_IDS} from './slices/lab';
import {LAB_CASE_IDS} from './slices/labCases';
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
  /** Synthetic sample text shown under the steps to select or copy (the notepad's notes), one line each. */
  sampleText?: string[];
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
      'With two pages open, the side nav folds to its icons: open it from the side with the expand button (Expand sidebar) at the bottom of the icon rail.',
      'To keep it open in half views, turn off Fold the side nav in half view in Account Settings.',
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
    key: 'account-types',
    title: 'Who’s who: three accounts',
    steps: [
      'Opens Users as the admin. Each account has a type, worked out from its roles and teams.',
      'Admin: accounts, setup, Insights and every team’s records to read, but no clinical changes (a pathway has no Finish Stage or Add Note for them). In no team.',
      `Staff: in a team, a clinic or the lab. The team decides what they see, their roles what they do. ${LAB.fullName} is staff in the lab.`,
      'Edit teams on a user’s row sets their teams, and which clinics they’re the clinic admin of.',
    ],
    account: ADMIN.username,
    path: '/admin/users',
    buttonLabel: 'Open Users as the admin',
  },
  {
    key: 'clinic-admin',
    title: 'Clinic admin and staff',
    steps: [
      `${STAFF.fullName} is Dermatology’s clinic admin: staff who also look after their own clinic.`,
      'For now she works exactly as any Dermatology clinician does, and sees only what her team sees.',
      'Her extras come later: an oversight tab for the clinic, and the clinic’s own templates and letter templates.',
    ],
    account: STAFF.username,
    path: '/',
    buttonLabel: `Open Home as ${STAFF.fullName}`,
  },
  {
    key: 'team-walls',
    title: 'Other clinics: a summary only',
    steps: [
      `As ${STAFF.fullName}, open Priya Holloway. Patients are centre-wide: anyone can find anyone.`,
      'Her Gynaecology pathway shows under In other clinics as one line (the clinic, its status, since when): no title, stages, notes or link.',
      'Pathways, referrals, appointments, letters, results, consent and MDT items belong to a team. Another team’s by its link answers “not found”, and lists, counts and Insights leave it out.',
      'The admin reads every team’s, and changes nothing clinical.',
    ],
    account: STAFF.username,
    path: `/patients/${PATIENTS.priyaHolloway}`,
    buttonLabel: 'Open the patient as the dermatologist',
  },
  {
    key: 'triage',
    title: 'Triage a referral',
    steps: [
      `As ${STAFF.fullName}, open Oliver Maddox's urgent referral to Dermatology.`,
      'Choose Accept, pick a template and a priority (nothing is chosen for you), check, and confirm. MedApp opens the new pathway.',
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
    key: 'book',
    title: 'Book an appointment',
    steps: [
      'Opens Ellis Varden’s suspected skin cancer pathway: his dermoscopy was uncertain, so its Take biopsy stage is next, and needs a visit.',
      'Choose Book Visit on that stage, pick who and a free time, and book it. It shows on the stage, the timeline and the Calendar.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.biopsyToBook}`,
    buttonLabel: 'Open the pathway to book its visit',
    whyNot: (state) =>
      state.appointments.some((a) => a.pathwayId === PATHWAYS.biopsyToBook && a.stageName === 'Take biopsy' && a.status === 'Booked')
        ? `You've booked this visit already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'consent',
    title: 'Record consent, then finish the stage',
    steps: [
      'Opens Ellis Varden’s pathway. Take biopsy needs a consent record (a stage gate), and he withdrew his first consent to think it over, so Finish Stage is refused.',
      'He has now agreed: record the consent in the Consent card (the form, who signed, the date). Then Finish Stage works, in the next step. MedApp never judges capacity.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.biopsyToBook}`,
    buttonLabel: 'Open the pathway that needs consent',
    whyNot: (state) => {
      const biopsy = state.pathways.find((p) => p.id === PATHWAYS.biopsyToBook)?.stages.find((s) => s.name === 'Take biopsy');
      return biopsy && state.consentRecords.some((c) => c.stageId === biopsy.id && c.status === 'Valid')
        ? `You've recorded his consent already in your copy of the demo. ${RESET_HINT}`
        : null;
    },
  },
  {
    key: 'relay-hand-over',
    title: 'The relay, 1: hand a stage to the lab',
    steps: [
      'Stages belong to a team. On Ellis Varden’s pathway, Take biopsy is Dermatology’s; the next, Histology, is the lab’s.',
      'With his consent recorded (the step before), choose Finish Stage on Take biopsy (say why, if the visit hasn’t happened yet). The pathway is handed to the lab, and the lab is told.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.biopsyToBook}`,
    buttonLabel: 'Open the pathway to hand over',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.biopsyToBook)?.stages.find((s) => s.name === 'Take biopsy')?.completedAtUtc
        ? `You've finished Take biopsy already in your copy of the demo: it's with the lab. ${RESET_HINT}`
        : null,
  },
  {
    key: 'relay-arrives',
    title: 'The relay, 2: it arrives in the lab',
    steps: [
      `Switches to ${LAB.fullName}. My Worklist’s New for us lists what has just reached the lab, Ellis Varden’s too once handed over.`,
      'Reaching the lab’s stage opened its lab case, waiting for the sample: receiving the sample picks the stage up, or choose Pick Up on it. The bell has the handover.',
    ],
    account: LAB.username,
    path: '/tasks?view=newForUs',
    buttonLabel: 'Open New for us as the lab',
  },
  {
    key: 'finish-stage',
    title: 'The relay, 3: the lab hands it back',
    steps: [
      'Opens the lab case of Priya Holloway’s Histology stage: every step done. Her pathway’s stage and this case are one piece of work.',
      'Choose Enter Results and Authorise. That finishes her Histology stage with the results, and the next stage, Histology result (a choice node), is Dermatology’s: the pathway goes back with the results on it, Dermatology is told, and the result waits for sign-off. The lab keeps reading it.',
    ],
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.stageToReport}`,
    buttonLabel: 'Open the lab case as the lab',
    whyNot: (state) =>
      state.labCases.find((c) => c.id === LAB_CASE_IDS.stageToReport)?.status === 'Open'
        ? null
        : `You've authorised this case already in your copy of the demo: her Histology is finished. ${RESET_HINT}`,
  },
  {
    key: 'relay-back',
    title: 'The relay, 4: back from the lab',
    steps: [
      `As ${STAFF.fullName}, Back from the lab lists Dermatology’s pathways the lab has handed back, not picked up yet.`,
      'Open one and choose Pick Up (or just start the stage): it leaves the list. Our turn and Awaiting the lab are beside it.',
    ],
    account: STAFF.username,
    path: '/tasks?view=backFromLab',
    buttonLabel: 'Open Back from the lab',
  },
  {
    key: 'sign-off',
    title: 'Sign off a result',
    steps: [
      `Opens Callum Ashby’s biopsy result, back from the lab and waiting for ${STAFF.fullName}’s sign-off.`,
      'Choose Sign Off and say what’s been done. Nothing is chosen for you; his Histology result stage waits for it.',
    ],
    account: STAFF.username,
    path: `/results/${LAB_IDS.resultToSignOff}`,
    buttonLabel: 'Open the result to sign off',
    whyNot: (state) =>
      state.results.find((r) => r.id === LAB_IDS.resultToSignOff)?.status === 'AwaitingSignOff'
        ? null
        : `You've signed off this result already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'lab',
    title: 'The lab’s home',
    steps: [
      `${LAB.fullName}’s home in the lab: pathways new for the lab and its turn, samples to receive and in progress, lab cases to report, at risk and overdue (each with its countdown), and back to the clinic, each opening its full list.`,
      'Samples in the side nav is the samples database: every sample, from the clinics and the hospital, each marked with where it came from, in tabs and searchable by clinic, test, status and due date. A pathway’s last stage can’t be finished while any of its samples is still with the lab.',
    ],
    account: LAB.username,
    path: '/lab',
    buttonLabel: 'Open the lab’s home',
  },
  {
    key: 'lab-tests',
    title: 'The lab’s test catalogue',
    steps: [
      'Each test the lab does: its kind, its target and limit in calendar days (7 and 10 for histology, as the RCPath turnaround), when it’s at risk, and its steps in order.',
      'There’s no list of outside labs any more: the centre’s own lab does every test.',
    ],
    account: ADMIN.username,
    path: '/admin/specimen-setup',
    buttonLabel: 'Open the test catalogue',
  },
  {
    key: 'hospital-request',
    title: 'A sample from the hospital',
    steps: [
      'Opens an urgent request from Riverside General (demo), typed in by the lab: the hospital’s patient number, the NHS number, the surgeon, the priority and the dates, as the request gave them. It opened a lab case of the lab’s own, with no pathway.',
      'The patient banner sits over the request’s own numbers, so both can be checked. Wilfred Ashdown came only from the hospital: clinic staff don’t find him in Patients; the lab does.',
      'Back in Hospital Requests, Register Request types in a new one: find the patient by NHS number, or last name and date of birth, and choose them yourself (never matched for you), or add a new patient.',
    ],
    account: LAB.username,
    path: `/hospital-requests/${HOSPITAL_IDS.urgentRequest}`,
    buttonLabel: 'Open the hospital request as the lab',
  },
  {
    key: 'lab-case',
    title: 'Work a lab case',
    steps: [
      'Opens Wilfred Ashdown’s case from the hospital: received, accessioned, “Sample adequate?” recorded as adequate, and the test’s steps done. Each step says who and when.',
      'What’s Next asks “Needs more work?”. Choose an outcome yourself (nothing is chosen for you): No more work, extra tests (special stains) or a second opinion, which adds their steps first.',
      'Once there’s no more work, Enter Results and Authorise: the results go back to the hospital (for a clinic’s case, to the clinician as a result to sign off, finishing the pathway’s lab stage). “Insufficient” at “Sample adequate?” asks the clinic for a repeat instead, reopening the stage that took the sample.',
      'Its Clock is strict: the lab’s own target (the test’s 7 days, its limit 10) and the hospital’s “report needed by” date, whichever is sooner. Wilfred’s hospital date is sooner, so it applies; it turns at risk 2 days before, and overdue after the end of that day until the report is authorised. A second opinion doesn’t stop it.',
      'Lab Cases in the side nav lists every case with its countdown: open, at risk (Ilona Petrova’s is due today), overdue (Ellis Varden’s, away for a second opinion), to report, reported and repeats asked for. Clocks are dates only, never anything from a result.',
    ],
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.atChoice}`,
    buttonLabel: 'Open the lab case as the lab',
    whyNot: (state) =>
      state.labCases.find((c) => c.id === LAB_CASE_IDS.atChoice)?.status === 'Open'
        ? null
        : `You've finished this case already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'fork',
    title: 'A pathway that forks',
    steps: [
      'Opens Aisha Khoury’s pathway: the skin check found a second lesion, so Biopsy another lesion was added after the dermoscopy. Both biopsies are taken, so two Histology stages are current side by side, both the lab’s.',
      'Stages shows the tree: the added route indented on its own. It ends by itself, and she isn’t discharged until it’s done.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.forkedTests}`,
    buttonLabel: 'Open the forked pathway',
  },
  {
    key: 'branch',
    title: 'Record an outcome: the pathway grows',
    steps: [
      'Opens Felix Moreton’s suspected skin cancer pathway. Triage is done, and Dermoscopy assessment is a choice node (an assessment). A template is the full tree; a pathway shows only the stages up to its next choice node.',
      'Choose Finish Stage and record one of its six outcomes (nothing is chosen for you). Uncertain: biopsy adds Take biopsy, Histology and Histology result, the next choice node; the others add a care plan or a referral. Outcomes not chosen never appear, and progress counts the stages it has.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.branchToChoose}`,
    buttonLabel: 'Open the pathway at its choice node',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.branchToChoose)?.stages.some((s) => s.outcome)
        ? `You've recorded this pathway's outcome already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'added-branch',
    title: 'Add a branch on new information',
    steps: [
      'Opens Priya Holloway’s pathway. Say the skin check found a second lesion that needs a biopsy too.',
      'Choose Add Branch, then Biopsy another lesion (its template lists what may be added; nothing is suggested), after Dermoscopy assessment, and why.',
      'Its stages run beside what’s left, on a route of their own; she isn’t discharged until they’re done. The reason stays on the pathway, never in the audit log.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.histologyToFinish}`,
    buttonLabel: 'Open the pathway to add a branch to',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.histologyToFinish)?.branchAdditions?.some((a) => a.templateBranchId === TEMPLATE_BRANCHES.biopsyAnother)
        ? `You've added the second biopsy already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'draw-routes',
    title: 'Draw a template’s routes',
    steps: [
      'Opens the Suspected skin cancer template as the admin (setup is the admin’s): the full tree, from the clinic’s reviewed pathways. Dermoscopy assessment and Histology result are choice nodes (an assessment, a lab result); each stage after one names the outcome that leads to it.',
      'Each stage says what it comes after; every route ends in a discharge or a referral. Histology is the lab’s stage. A stage several outcomes lead to shows one outcome’s name here, so leave without saving.',
    ],
    account: ADMIN.username,
    path: `/admin/pathway-templates/${TEMPLATES.suspectedSkinCancer}`,
    buttonLabel: 'Open the template editor',
  },
  {
    key: 'dermatology-templates',
    title: 'Dermatology’s ten templates',
    steps: [
      'Pathway Templates lists Dermatology’s ten, from the clinic’s reviewed pathways: suspected skin cancer, a changing mole, surveillance after treatment, and assessments of inflammatory skin, eczema, psoriasis, acne, infection, hair loss and pigmentation.',
      'They share the stage library’s stages (Triage, Take biopsy, Histology and the rest), and the skin cancer ones are kept for the Oncology retention period. Routes back (a repeat biopsy, another review) are addable routes or stay at the review: routes only go forward.',
    ],
    account: ADMIN.username,
    path: '/admin/pathway-templates',
    buttonLabel: 'Open the pathway templates',
  },
  {
    key: 'assessment-only',
    title: 'An assessment-only pathway',
    steps: [
      'Opens Biomechanics’ Lower limb biomechanics pathway, as the admin (read only): no lab, just an assessment choice node.',
      'Its outcomes are custom orthoses, an exercise plan, footwear advice or a referral to the hospital; Biomechanics’ clinician records one, and only its stages are added.',
    ],
    account: ADMIN.username,
    path: `/pathways/${PATHWAYS.biomechanicsAssessment}`,
    buttonLabel: 'Open the assessment-only pathway',
  },
  {
    key: 'mdt',
    title: 'Record an MDT outcome',
    steps: [
      'Opens this morning’s skin MDT (also reached from the Calendar’s MDT button).',
      'Choose Record Outcome on Ellis Varden’s item, tick who was there and write the outcome. MedApp never suggests one; whoever has the pathway is told.',
    ],
    account: STAFF.username,
    path: `/mdt?meeting=${CLINICAL_IDS.mdtToday}`,
    buttonLabel: 'Open this morning’s MDT',
    whyNot: (state) =>
      state.mdtItems.find((i) => i.id === CLINICAL_IDS.mdtItemToDiscuss)?.status === 'Listed'
        ? null
        : `This item has been discussed already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'mdt-pathology',
    title: 'Pathology at the MDT',
    steps: [
      `As ${LAB.fullName} from the lab, this morning’s skin MDT: the lab was invited, so it reads the cases listed on it, read only, until each is discussed or withdrawn.`,
      'Harriet Quill’s case names him as its presenter, with her biopsy and a link to its report. Only Dermatology chooses the presenter, moves or withdraws an item.',
    ],
    account: LAB.username,
    path: `/mdt?meeting=${CLINICAL_IDS.mdtToday}`,
    buttonLabel: 'Open this morning’s MDT as the lab',
    whyNot: (state) =>
      state.mdtItems.find((i) => i.id === CLINICAL_IDS.mdtItemPresented)?.presenterUserId === LAB.id
        ? null
        : `Harriet Quill’s case has another presenter, or none, in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'timeline',
    title: 'A patient’s timeline',
    steps: ['Opens Priya Holloway: everything that happened to her, newest first. Filter it by kind with the chips above it.'],
    path: `/patients/${PATIENTS.priyaHolloway}`,
    buttonLabel: 'Open the patient’s timeline',
  },
  {
    key: 'meeting',
    title: 'Arrange a meeting',
    steps: [
      'As the admin, choose New, then New meeting, on the Calendar.',
      `Invite ${STAFF.fullName}, pick a suggested time, and save. The next step shows it on her calendar.`,
    ],
    account: ADMIN.username,
    path: '/calendar',
    buttonLabel: 'Open the Calendar as the admin',
  },
  {
    key: 'meeting-seen',
    title: 'See the meeting as the person invited',
    steps: [`Switches to ${STAFF.fullName}: the meeting is on her calendar, and the bell has the invitation to accept or decline.`],
    account: STAFF.username,
    path: '/calendar',
    buttonLabel: `Open ${STAFF.fullName}'s calendar`,
  },
  {
    key: 'team-calendar',
    title: 'Team calendars and busy time',
    steps: [
      `As ${LAB.fullName}, the Team view shows Dermatology’s calendar: anyone can look at any team’s, picked in Team.`,
      'Another team’s appointments show only as “Busy, another team” (unless their pathway was passed to you, as Harriet Quill’s was to the lab), and the Skin cancer team meeting (two weekdays ahead) as “Meeting, another team”.',
      'Busy time still counts when suggesting free times. Meetings are only with your own team, except an MDT.',
    ],
    account: LAB.username,
    path: `/calendar?view=team&team=${TEAMS.dermatology}`,
    buttonLabel: 'Open Dermatology’s calendar as the lab',
  },
  {
    key: 'on-hold-overdue',
    title: 'Every clinic’s pathways, and the clinic picker',
    steps: [
      'As the admin, Pathways lists every team’s open pathways. The clinic picker narrows it to one clinic; staff in several clinics pick among their own.',
      'Samuel Okafor’s is on hold; Margaret Fenwick’s is overdue. Sort and filter with the bar above the list.',
    ],
    account: ADMIN.username,
    path: '/pathways',
    buttonLabel: 'Open the list of pathways',
  },
  {
    key: 'notepad',
    title: 'The notepad',
    steps: [
      `As ${STAFF.fullName} (switch above if you’re not her), copy the sample notes below: select them, or choose Copy the Notes.`,
      'Open the notepad with the button below: it opens beside this page and stays open as you move around the app. Paste the notes into it; it saves as you type, and each account has its own.',
      'Open Ilona Petrova’s pathway, Psoriasis, new care plan (Patients, then Ilona Petrova, then the pathway).',
      'In the notepad, choose Add Note to Pathway, check it’s her pathway, and add it. The pathway then shows the note under Notes, by you.',
      'Shift-click Notepad in the side nav to open it in the half view on the right instead.',
    ],
    isNumbered: true,
    sampleText: [
      'Seen in clinic today.',
      'Plaques on both elbows, unchanged since the last visit.',
      'Patient happy with the care plan; review in 8 weeks.',
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
    title: 'Lab reports as files',
    full: 'A lab case’s report is a file the lab attaches and authorises. Here the results are typed in as text.',
  },
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
    full: 'Appointment reminders to patients, service alerts (admins told of referrals not triaged, results not signed off, visits not booked and specimens and lab cases overdue in time), purges of old operational data, and other jobs on a schedule. Here the Scheduled Jobs page lists them, but nothing runs.',
  },
  {
    title: 'Sending letters',
    full: 'Approved letters are sent by email or post, and their PDFs made on the server. Here a letter can be written, approved and recorded as sent by hand, but not sent or printed.',
  },
  {
    title: 'Changing roles and accounts',
    full: 'Admins add staff, reset passwords and two-factor, and change roles and their permissions. Here the three accounts are fixed, though their teams can be changed.',
  },
  {
    title: 'Files from the lab',
    full: 'A specimen’s report and an external result arrive as files, and a specimen’s label has a QR code. Here only the seeded reports are there.',
  },
];
