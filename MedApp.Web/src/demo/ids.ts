/**
 * The light demo's fixed IDs: the seed makes every record with one of these, so the Read Me's tour can open an exact
 * record, and Reset brings back the same ones. Synthetic, like everything in the seed.
 */

/**
 * A fixed ID: `kind` says what it is (one kind per group below, and per slice: the lab's are 0xe1 to 0xe5, letters,
 * the MDT and consent 0xe6 to 0xeb, setup 0xf0 to 0xf8, pathways' extras 0xf9 to 0xfe), `n` which one.
 */
export const id = (kind: number, n: number) =>
  `${kind.toString(16).padStart(8, '0')}-0000-4000-8000-${n.toString(16).padStart(12, '0')}`;

export const USERS = {
  admin: id(0xd0, 1),
  thornbury: id(0xd0, 2),
  whitlock: id(0xd0, 3),
  sandiford: id(0xd0, 4),
  marsh: id(0xd0, 5),
  hallworth: id(0xd0, 6),
  aldous: id(0xd0, 7),
} as const;

export const TEAMS = {
  /** The one clinic (19 Nov: one clinic, one lab). */
  dermatology: id(0xd1, 1),
  lab: id(0xd1, 6),
} as const;

export const TEMPLATES = {
  /** Dermatology's ten (the owner's reviewed templates, `docs/pathways/dermatology.yaml`, mapped in `dermatology.ts`). */
  suspectedSkinCancer: id(0xd2, 5),
  inflammatorySkin: id(0xd2, 2),
  changingMole: id(0xd2, 8),
  skinCancerSurveillance: id(0xd2, 9),
  eczema: id(0xd2, 10),
  psoriasis: id(0xd2, 11),
  acne: id(0xd2, 12),
  skinInfection: id(0xd2, 13),
  hairLoss: id(0xd2, 14),
  pigmentation: id(0xd2, 15),
} as const;

/** Addable routes (MC10): routes a clinician may add on new information, never suggested. */
export const TEMPLATE_BRANCHES = {
  /** Suspected skin cancer's "Biopsy another lesion" (Take biopsy, then Histology), after Dermoscopy assessment. */
  biopsyAnother: id(0xfc, 1),
} as const;

/** The stage library's own definition, which Dermatology's reviewed templates share; theirs are numbered from
 * `id(0xd3, 0x20)` (`dermatologyStageDefinitions`). */
export const STAGES = {
  carePlan: id(0xd3, 13),
} as const;

export const PATIENTS = {
  /** Has the suspected skin cancer pathway at Take biopsy: the visit to book, and consent to record again. */
  ellisVarden: id(0xd4, 1),
  /** Has the suspected skin cancer pathway at Histology (the lab's): its stage can be finished. */
  priyaHolloway: id(0xd4, 2),
  /** Has the suspected skin infection pathway on hold. */
  samuelOkafor: id(0xd4, 3),
  /** No pathway: her care ended over eight years ago, so the retention review has a record to show. */
  graceAbernethy: id(0xd4, 6),
  tobiasLund: id(0xd4, 7),
  ilonaPetrova: id(0xd4, 8),
  /** Referred to Dermatology: awaiting triage. */
  oliverMaddox: id(0xd4, 9),
  aishaKhoury: id(0xd4, 10),
  callumAshby: id(0xd4, 12),
  /** Has the suspected skin cancer pathway past its histology result (Low-risk BCC): at Refer to hospital. */
  harrietQuill: id(0xd4, 13),
  /** Has the suspected skin cancer pathway at Dermoscopy assessment, a choice node. */
  felixMoreton: id(0xd4, 14),
} as const;

export const PATHWAYS = {
  /** Suspected skin cancer, Ellis Varden, Nadia Thornbury's: at Take biopsy (a visit, not booked; his consent was withdrawn). */
  biopsyToBook: id(0xd5, 1),
  /** Suspected skin cancer, Priya Holloway, Nadia Thornbury's: with the lab at Histology, ready to finish. */
  histologyToFinish: id(0xd5, 2),
  /** Suspected skin infection, Samuel Okafor: on hold at Take skin sample, the sample sent. */
  onHold: id(0xd5, 3),
  /** Changing mole or lesion check, Tobias Lund: at Triage, nobody assigned yet. */
  unassigned: id(0xd5, 7),
  /** Psoriasis assessment, Ilona Petrova, Nadia Thornbury's: assessment only, past its care plan; the review booked this week. */
  followUpBooked: id(0xd5, 8),
  /** Suspected skin cancer, Harriet Quill, Nadia Thornbury's: Low-risk BCC recorded; at Refer to hospital (needs its letter), overdue. */
  referredToHospital: id(0xd5, 9),
  /** Suspected skin cancer, Felix Moreton, Nadia Thornbury's: at Dermoscopy assessment, a choice node with six outcomes. */
  branchToChoose: id(0xd5, 10),
  /** Suspected skin cancer, Aisha Khoury, Nadia Thornbury's: Biopsy another lesion added, so two Histology stages run side by side. */
  forkedTests: id(0xd5, 13),
  /** Suspected skin cancer, Callum Ashby, Nadia Thornbury's: back from the lab at Histology result (a choice node), not picked up yet. */
  backFromLab: id(0xd5, 14),
} as const;

export const REFERRALS = {
  /** Oliver Maddox to Dermatology, awaiting triage: the tour's triage step. */
  dermatologyToTriage: id(0xd6, 1),
  aishaDermatology: id(0xd6, 2),
  /** Ellis Varden's, accepted into the biopsy pathway. */
  ellisAccepted: id(0xd6, 5),
} as const;

export const APPOINTMENTS = {
  ilonaFollowUp: id(0xd7, 2),
  ellisFirstVisit: id(0xd7, 3),
  priyaFirstVisit: id(0xd7, 4),
  priyaBiopsy: id(0xd7, 5),
} as const;

export const MEETINGS = {
  skinMdt: id(0xd8, 1),
} as const;

export const ACTIVITY_TYPES = {
  clinic: id(0xd9, 1),
  admin: id(0xd9, 2),
  lunch: id(0xd9, 3),
  triage: id(0xd9, 4),
  minorSurgery: id(0xd9, 5),
} as const;

export const SITES = {
  centre: id(0xda, 1),
} as const;

export const ROOMS = {
  clinicRoom1: id(0xdb, 1),
  procedureRoom: id(0xdb, 2),
} as const;

/** Appointments added with the second seed (pathways' extras). */
export const MORE_APPOINTMENTS = {
  /** Harriet Quill's results visit (Histology result), attended this morning. */
  harrietResults: id(0xf9, 1),
  harrietFirstVisit: id(0xf9, 2),
  harrietBiopsy: id(0xf9, 3),
  felixFirstVisit: id(0xf9, 4),
} as const;

/** Every record ID in the seed, by kind (the tour's test checks its links against these). */
export const ALL_IDS = {USERS, TEAMS, TEMPLATES, STAGES, PATIENTS, PATHWAYS, REFERRALS, APPOINTMENTS, MEETINGS} as const;

/** A new record's ID, for what a visitor makes. */
export const newId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx'.replace(/x/g, () => Math.floor(Math.random() * 16).toString(16));
