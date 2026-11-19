import {appSettings} from '../app/settingsStore';
import {PINS_KEY} from '../components/navigation/pins';
import {ADMIN, LAB, STAFF} from './accounts';
import {PATHWAYS, PATIENTS, REFERRALS, TEAMS, TEMPLATE_BRANCHES, TEMPLATES} from './ids';
import {CLINICAL_IDS} from './slices/clinical';
import {CONSULTS} from './slices/consults';
import {HOSPITAL_IDS} from './slices/hospitals';
import {LAB_IDS} from './slices/lab';
import {LAB_CASE_IDS} from './slices/labCases';
import type {DemoState} from './state';

/** One step of the Read Me's guided tour: a button that opens the exact page or record, as the right account. */
export interface TourStep {
  key: string;
  /** The part of the tour it's in (`TOUR_PARTS`): a part's steps are together, in the parts' order. */
  part: TourPart;
  title: string;
  /** What to do there, in short steps. */
  steps: string[];
  /** The account the step needs (it switches to it first), or undefined for either. */
  account?: string;
  /** The page to open: a path in the app, with its query; none stays on the Read Me. */
  path?: string;
  /** Opens the notepad beside the page. */
  opensNotepad?: boolean;
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

/** The tour's parts, in order: the Read Me shows each one's title (and its line, if any) above its first step. */
export const TOUR_PARTS = [
  {key: 'start', title: 'Getting started', intro: 'Find your way around, and meet the three accounts.'},
  {
    key: 'story',
    title: 'The Dermatology story',
    intro:
      'One route through Suspected skin cancer, from the referral to the MDT, in order. Each patient is at that point of the route: Oliver Maddox is referred, Ellis Varden is booked and biopsied, Priya Holloway’s sample is reported, Callum Ashby’s result is reviewed and Harriet Quill is referred to the hospital.',
  },
  {key: 'consults', title: 'Side story: consults'},
  {key: 'photos', title: 'Side story: clinical photo consent'},
  {key: 'homes', title: 'Side story: the clinic’s and the lab’s homes'},
  {key: 'hospital', title: 'Side story: the hospital’s samples'},
  {key: 'clocks', title: 'Side story: the clocks'},
  {key: 'templates', title: 'Side story: templates and routes'},
  {key: 'around', title: 'Around the app'},
] as const;

export type TourPart = (typeof TOUR_PARTS)[number]['key'];

export const TOUR: TourStep[] = [
  // Getting started.
  {
    key: 'half-view',
    part: 'start',
    title: 'Two pages side by side',
    steps: [
      'Rest the pointer on Patients in the side nav and open its menu (the … at the end of the row), then choose Open in half view. Patients opens on the right, beside this Read Me.',
      'Close it with the close button (×) at the top of the Patients half.',
      'Shift-click Patients in the side nav: it opens on the right again.',
      'Close it again.',
      'Ctrl-click (⌘-click on a Mac) Patients: it opens on the left, and this Read Me moves to the right.',
      'Keep the Read Me there and carry on with the tour from it: each button opens its page on the left, beside the Read Me.',
      'With two pages open, the side nav folds to its icons: open it from the side with the expand button (Expand sidebar) at the bottom of the icon rail.',
      'Fold the side nav in half view is on unless you turn it off, in Account Settings: the button below opens it on the left, beside this Read Me.',
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
    part: 'start',
    title: 'Who’s who: three accounts',
    steps: [
      'Opens Users as the admin. Each account has a type, worked out from its roles and teams.',
      'Admin: accounts, setup, Insights and every team’s records to read, but no clinical changes (a pathway has no Finish Stage or Add Note for them). In no team.',
      `Staff: in a team, a clinic or the lab. The team decides what they see, their roles what they do. ${LAB.fullName} is staff in the lab.`,
      `${STAFF.fullName} is Dermatology’s clinic admin: staff who also look after their own clinic. For now she works as any Dermatology clinician does; her extras (an oversight tab, the clinic’s own templates and letter templates) come later.`,
      'Edit teams on a user’s row sets their teams, and which clinics they’re the clinic admin of.',
    ],
    account: ADMIN.username,
    path: '/admin/users',
    buttonLabel: 'Open Users as the admin',
  },
  {
    key: 'team-walls',
    part: 'start',
    title: 'Another team’s pathway: a summary only',
    steps: [
      `As ${LAB.fullName} from the lab, open Tobias Lund. Patients are centre-wide: anyone can find anyone.`,
      'His Dermatology pathway hasn’t been passed to the lab, so it shows under In other clinics as one line (the clinic, its status, since when): no title, stages, notes or link.',
      'Pathways, referrals, appointments, letters, results, consent and MDT items belong to a team. Another team’s by its link answers “not found”, and lists, counts and Insights leave it out.',
      'The admin reads every team’s, and changes nothing clinical.',
    ],
    account: LAB.username,
    path: `/patients/${PATIENTS.tobiasLund}`,
    buttonLabel: 'Open the patient as the lab',
  },

  // The Dermatology story, in order.
  {
    key: 'triage',
    part: 'story',
    title: 'Triage a referral',
    steps: [
      `As ${STAFF.fullName}, open Oliver Maddox's urgent referral to Dermatology.`,
      'Choose Accept, pick a template (Suspected skin cancer) and a priority (nothing is chosen for you), check, and confirm. MedApp opens his new pathway, at Triage.',
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
    key: 'branch',
    part: 'story',
    title: 'The consultation: a choice node',
    steps: [
      'Opens Felix Moreton’s suspected skin cancer pathway, a step further on: Triage is done, and Dermoscopy assessment is a choice node (an assessment). A template is the full tree; a pathway shows only the stages up to its next choice node.',
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
    key: 'book',
    part: 'story',
    title: 'Book an appointment',
    steps: [
      'Opens Ellis Varden’s suspected skin cancer pathway: his dermoscopy was uncertain, so its Take biopsy stage is next, and needs a visit.',
      'Choose Book Visit on that stage, pick who and a free time, and book it. It shows on the stage, the timeline and the Calendar.',
      'Pin the pathway: while it’s open it shows in the side nav under Pathways. Open its row’s menu (the … at the end of the row) and choose Pin. You’ll come back to it from there with the notepad later.',
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
    part: 'story',
    title: 'Record consent for the biopsy',
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
    part: 'story',
    title: 'The relay, 1: take the biopsy and hand it to the lab',
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
    part: 'story',
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
    part: 'story',
    title: 'The relay, 3: the lab’s case, authorised',
    steps: [
      'Ellis Varden’s sample still has to be received and worked. Priya Holloway’s, further along the same route, is done: this opens the lab case of her Histology stage, every step recorded. Her pathway’s stage and this case are one piece of work.',
      'Choose Enter Results and Authorise. That finishes her Histology stage with the results, and the next stage, Histology result (a choice node), is Dermatology’s: the pathway goes back with the results on it, Dermatology is told, and the result waits for sign-off. The lab keeps reading it.',
    ],
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.stageToReport}`,
    buttonLabel: 'Open Priya Holloway’s lab case',
    whyNot: (state) =>
      state.labCases.find((c) => c.id === LAB_CASE_IDS.stageToReport)?.status === 'Open'
        ? null
        : `You've authorised this case already in your copy of the demo: her Histology is finished. ${RESET_HINT}`,
  },
  {
    key: 'relay-back',
    part: 'story',
    title: 'The relay, 4: back to the clinic',
    steps: [
      `As ${STAFF.fullName}, Back from the lab lists Dermatology’s pathways the lab has handed back, not picked up yet: Callum Ashby’s, and Priya Holloway’s once authorised.`,
      'Open one and choose Pick Up (or just start the stage): it leaves the list. Our turn and Awaiting the lab are beside it.',
    ],
    account: STAFF.username,
    path: '/tasks?view=backFromLab',
    buttonLabel: 'Open Back from the lab',
  },
  {
    key: 'sign-off',
    part: 'story',
    title: 'Sign off the result',
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
    key: 'results-review',
    part: 'story',
    title: 'Results review: the second choice node',
    steps: [
      'Opens Callum Ashby’s pathway at Histology result, a choice node (a lab result). With his result signed off (the step before), choose Finish Stage and record the diagnosis as the report gives it: nothing is chosen for you.',
      'Benign adds Agree care plan and Letter to GP and discharge (a care plan). Bowen’s disease or Low-risk BCC adds Refer to hospital (the next step); High-risk BCC, SCC or melanoma, the Skin cancer MDT, with pathology.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.backFromLab}`,
    buttonLabel: 'Open the pathway at its results review',
    whyNot: (state) =>
      state.pathways.find((p) => p.id === PATHWAYS.backFromLab)?.stages.find((s) => s.name === 'Histology result')?.outcome
        ? `You've recorded his histology outcome already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'referral-letter',
    part: 'story',
    title: 'The outcome: a referral letter to the hospital',
    steps: [
      'Opens Harriet Quill’s onward referral to Riverside General (demo). Her histology was a low-risk BCC, so her pathway is at Refer to hospital, which can’t be finished until this letter is approved and sent.',
      'It’s your draft: check it, then Approve it (you can approve letters), and Record as Sent for each recipient, the hospital and her GP. The light version doesn’t send letters.',
      'Back on her pathway, Finish Stage on Refer to hospital ends it in a referral to the hospital. A letter’s merge fields copy only names and addresses; the clinical words are the author’s.',
    ],
    account: STAFF.username,
    path: `/letters/${CLINICAL_IDS.onwardReferralLetter}`,
    buttonLabel: 'Open the referral letter',
    whyNot: (state) =>
      state.letters.find((l) => l.id === CLINICAL_IDS.onwardReferralLetter)?.status === 'Draft'
        ? null
        : `You've approved this letter already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'mdt-pathology',
    part: 'story',
    title: 'The MDT, 1: pathology joins it',
    steps: [
      `As ${LAB.fullName} from the lab, this morning’s skin MDT: he was invited, so he reads the cases listed on it, and only those, read only, until each is discussed or withdrawn (and at least until the meeting ends). Anyone from another team works the same way; someone not invited sees none of the list.`,
      'Harriet Quill’s item names him as its presenter, with her biopsy and a link to its report. A listed case the lab never had, he reads only as an attendee, and loses once it’s discussed. Only Dermatology chooses the presenter, moves or withdraws an item.',
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
    key: 'mdt',
    part: 'story',
    title: 'The MDT, 2: record its outcome',
    steps: [
      `As ${STAFF.fullName}, this morning’s skin MDT (also reached from the Calendar’s MDT button).`,
      'Choose Record Outcome on Harriet Quill’s item, tick who was there and write the outcome. MedApp never suggests one; whoever has the pathway is told.',
    ],
    account: STAFF.username,
    path: `/mdt?meeting=${CLINICAL_IDS.mdtToday}`,
    buttonLabel: 'Open this morning’s MDT',
    whyNot: (state) =>
      state.mdtItems.find((i) => i.id === CLINICAL_IDS.mdtItemPresented)?.status === 'Listed'
        ? null
        : `This item has been discussed already in your copy of the demo. ${RESET_HINT}`,
  },

  // Side stories.
  {
    key: 'consult',
    part: 'consults',
    title: 'A consult: Dermatology asks the lab',
    steps: [
      `As ${LAB.fullName}, open Felix Moreton’s pathway. It’s Dermatology’s and no sample has gone to the lab, but Dermatology asked the lab a question (a consult), so the lab reads it, read only, while the consult is open. The lab’s home lists it under Consults for the lab.`,
      'In Consults, choose Reply and write the pathologist’s opinion. It’s kept on the pathway, and Dermatology is told (the notification never holds the question or the reply).',
      `Then as ${STAFF.fullName}, Close Consult on the same pathway ends the lab’s reading. Ask Another Team asks a new one: a clinic or the lab, none chosen for you.`,
    ],
    account: LAB.username,
    path: `/pathways/${PATHWAYS.branchToChoose}`,
    buttonLabel: 'Open the pathway as the lab',
    whyNot: (state) => {
      const status = state.consults.find((c) => c.id === CONSULTS.felixToLab)?.status;
      if (status === 'Open') return null;
      return status === 'Replied'
        ? `The lab has replied already in your copy of the demo: Dermatology can close it. ${RESET_HINT}`
        : `This consult is closed in your copy of the demo, so the lab can no longer open the pathway. ${RESET_HINT}`;
    },
  },
  {
    key: 'photo-consent',
    part: 'photos',
    title: 'Photos need the patient’s consent',
    steps: [
      'Opens Nora Fennick’s changing mole pathway: her dermoscopy was uncertain, so Take photographs is next, to compare at her mole review. Finish Stage is refused: it needs her consent to clinical photography.',
      'Record it in the Consent card on the Clinical photography form (who signed, the date). Then Finish Stage works (say why, if the visit hasn’t happened yet), and Mole review is next.',
      'In the full version, a picture on her pathway can then be marked a clinical photo; files aren’t in the light version.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.photosToTake}`,
    buttonLabel: 'Open the pathway that needs photo consent',
    whyNot: (state) =>
      state.consentRecords.some(
        (c) => c.pathwayId === PATHWAYS.photosToTake && c.consentFormId === CLINICAL_IDS.photographyConsentForm && c.status === 'Valid',
      )
        ? `You've recorded her photo consent already in your copy of the demo. ${RESET_HINT}`
        : null,
  },
  {
    key: 'team-home',
    part: 'homes',
    title: 'The clinic’s home',
    steps: [
      `${STAFF.fullName}’s home in Dermatology, first in her side nav: referrals to triage, her pathways, pathways back from the lab, results to sign off and appointments to book.`,
      'Each list shows its first five and how many there are, soonest first, and opens its full list. Any clinic team gets the same home.',
    ],
    account: STAFF.username,
    path: '/team',
    buttonLabel: 'Open the team’s home',
  },
  {
    key: 'lab',
    part: 'homes',
    title: 'The lab’s home',
    steps: [
      `${LAB.fullName}’s home in the lab: pathways new for the lab and its turn, samples to receive and in progress, lab cases to report, at risk and overdue (each with its countdown), consults for the lab, and back to the clinic, each opening its full list.`,
      'Samples in the side nav is the samples database: every sample, from the clinics and the hospital, each marked with where it came from, in tabs and searchable by clinic, test, status and due date. A pathway’s last stage can’t be finished while any of its samples is still with the lab.',
    ],
    account: LAB.username,
    path: '/lab',
    buttonLabel: 'Open the lab’s home',
  },
  {
    key: 'hospital-request',
    part: 'hospital',
    title: 'A sample from the hospital',
    steps: [
      'Opens an urgent request from Riverside General (demo), typed in by the lab: the hospital’s patient number, the NHS number, the surgeon, the priority and the dates, as the request gave them. It opened a lab case of the lab’s own, with no pathway.',
      'The patient, at the top of Details, sits over the request’s own numbers, so both can be checked. Wilfred Ashdown came only from the hospital: clinic staff don’t find him in Patients; the lab does.',
      'Back in Hospital Requests, Register Request types in a new one: find the patient by NHS number, or last name and date of birth, and choose them yourself (never matched for you), or add a new patient.',
    ],
    account: LAB.username,
    path: `/hospital-requests/${HOSPITAL_IDS.urgentRequest}`,
    buttonLabel: 'Open the hospital request as the lab',
  },
  {
    key: 'lab-case',
    part: 'hospital',
    title: 'Work a lab case',
    steps: [
      'Opens Wilfred Ashdown’s case from the hospital: received, accessioned, “Sample adequate?” recorded as adequate, and the test’s steps done. Each step says who and when.',
      'What’s Next asks “Needs more work?”. Choose an outcome yourself (nothing is chosen for you): No more work, extra tests (special stains) or a second opinion, which adds their steps first.',
      'Once there’s no more work, Enter Results and Authorise: the results go back to the hospital (for a clinic’s case, to the clinician as a result to sign off, finishing the pathway’s lab stage). “Insufficient” at “Sample adequate?” asks the clinic for a repeat instead, reopening the stage that took the sample.',
    ],
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.atChoice}`,
    buttonLabel: 'Open the hospital’s lab case',
    whyNot: (state) =>
      state.labCases.find((c) => c.id === LAB_CASE_IDS.atChoice)?.status === 'Open'
        ? null
        : `You've finished this case already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'hospital-report',
    part: 'hospital',
    title: 'Back to the hospital: the report and surgery details',
    steps: [
      'Opens Samuel Okafor’s earlier case from the hospital (he’s a centre patient too). When its report was authorised it went back to the hospital in the same action, addressed to the request’s surgeon: Report shows it sent, when and how. Its clock stopped there.',
      'It now awaits the hospital’s surgery details. Add Surgery Details plays the hospital’s side: type what it sent, as written (the date, the procedure, the outcome), or “No surgery expected” and why. Adding them closes the case. Nothing is worked out from them.',
      'Details can arrive before the report too: Wilfred Ashdown’s open excision already has the hospital’s extra details. Lab Cases has an Awaiting surgery details tab, and the lab’s home a list of them.',
    ],
    account: LAB.username,
    path: `/lab/cases/${LAB_CASE_IDS.awaitingSurgeryDetails}`,
    buttonLabel: 'Open the case awaiting surgery details',
    whyNot: (state) =>
      state.labCases.find((c) => c.id === LAB_CASE_IDS.awaitingSurgeryDetails)?.status === 'AwaitingSurgeryDetails'
        ? null
        : `You've added this case's surgery details already in your copy of the demo. ${RESET_HINT}`,
  },
  {
    key: 'lab-tests',
    part: 'clocks',
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
    key: 'clocks',
    part: 'clocks',
    title: 'Every case’s countdown',
    steps: [
      'Lab Cases lists every case with its countdown: open, at risk (Ilona Petrova’s is due today), overdue (Samuel Okafor’s, away for a second opinion), to report, reported and repeats asked for.',
      'A case’s clock is strict: the lab’s own target (the test’s 7 days, its limit 10) and the hospital’s “report needed by” date, whichever is sooner. Wilfred Ashdown’s hospital date is sooner, so it applies; it turns at risk 2 days before, and overdue after the end of that day until the report is authorised. A second opinion doesn’t stop it.',
      'Clocks are dates only, never anything from a result.',
    ],
    account: LAB.username,
    path: '/lab/cases',
    buttonLabel: 'Open Lab Cases as the lab',
  },
  {
    key: 'dermatology-templates',
    part: 'templates',
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
    key: 'draw-routes',
    part: 'templates',
    title: 'Draw a template’s routes',
    steps: [
      'Opens the Suspected skin cancer template as the admin (setup is the admin’s): the full tree the story followed. Dermoscopy assessment and Histology result are choice nodes (an assessment, a lab result); each stage after one names the outcome that leads to it.',
      'Each stage says what it comes after; every route ends in a discharge or a referral. Histology is the lab’s stage. A stage several outcomes lead to shows one outcome’s name here, so leave without saving.',
    ],
    account: ADMIN.username,
    path: `/admin/pathway-templates/${TEMPLATES.suspectedSkinCancer}`,
    buttonLabel: 'Open the template editor',
  },
  {
    key: 'assessment-only',
    part: 'templates',
    title: 'An assessment decides the route',
    steps: [
      'Opens Ilona Petrova’s Psoriasis assessment pathway, as the admin (read only): its assessment is a choice node.',
      'Its clinician recorded Self-care is enough, so only the care plan and the review were added; the biopsy and the referrals of the other outcomes never appeared.',
    ],
    account: ADMIN.username,
    path: `/pathways/${PATHWAYS.followUpBooked}`,
    buttonLabel: 'Open the psoriasis pathway',
  },
  {
    key: 'added-branch',
    part: 'templates',
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
    key: 'fork',
    part: 'templates',
    title: 'A pathway that forks',
    steps: [
      'Opens Aisha Khoury’s pathway, where that’s been done: the skin check found a second lesion, so Biopsy another lesion was added after the dermoscopy. Both biopsies are taken, so two Histology stages are current side by side, both the lab’s.',
      'Stages shows the tree: the added route indented on its own. It ends by itself, and she isn’t discharged until it’s done.',
      'The second biopsy was too small to test: the lab recorded it insufficient, so its Take biopsy is reopened for a repeat and its Histology waits with the lab. Lab Cases’ Repeat requested tab has its case.',
    ],
    account: STAFF.username,
    path: `/pathways/${PATHWAYS.forkedTests}`,
    buttonLabel: 'Open the forked pathway',
  },

  // Around the app.
  {
    key: 'timeline',
    part: 'around',
    title: 'A patient’s timeline',
    steps: ['Opens Priya Holloway: everything that happened to her, newest first. Filter it by kind with the chips above it.'],
    path: `/patients/${PATIENTS.priyaHolloway}`,
    buttonLabel: 'Open the patient’s timeline',
  },
  {
    key: 'meeting',
    part: 'around',
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
    part: 'around',
    title: 'See the meeting as the person invited',
    steps: [`Switches to ${STAFF.fullName}: the meeting is on her calendar, and the bell has the invitation to accept or decline.`],
    account: STAFF.username,
    path: '/calendar',
    buttonLabel: `Open ${STAFF.fullName}'s calendar`,
  },
  {
    key: 'team-calendar',
    part: 'around',
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
    part: 'around',
    title: 'Every clinic’s pathways, and the clinic picker',
    steps: [
      'As the admin, Pathways lists every team’s open pathways. The clinic picker narrows it to one clinic; staff in several clinics pick among their own.',
      'Samuel Okafor’s is on hold; Harriet Quill’s is overdue until her referral finishes it. Sort and filter with the bar above the list.',
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
      `As ${STAFF.fullName} (switch above if you’re not her), copy the sample notes below: select them, or choose Copy the Notes.`,
      'Open the notepad with the button below: it opens beside this page and stays open as you move around the app. Paste the notes into it; it saves as you type, and each account has its own.',
      'Open Ellis Varden’s pathway, Changing mole, left forearm, from its pin under Pathways in the side nav (you pinned it when booking his visit). Not pinned? Patients, then Ellis Varden, then the pathway.',
      'In the notepad, choose Add Note to Pathway, check it’s his pathway, and add it. The pathway then shows the note under Notes, by you.',
      'Shift-click Notepad in the side nav to open it in the half view on the right instead.',
    ],
    isNumbered: true,
    sampleText: [
      'Seen in clinic today.',
      'Mole on the left forearm, 6 mm, as in the referral photo.',
      'Biopsy explained; patient happy to go ahead.',
    ],
    opensNotepad: true,
    buttonLabel: 'Open the notepad',
  },
  {
    key: 'accessibility',
    part: 'around',
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
    title: 'Lab reports as files',
    full: 'A lab case’s report is a file the lab attaches and authorises, and a hospital’s goes back as a letter (a PDF) with it attached. Here the results are typed in as text, and the hospital’s report is recorded as sent.',
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
