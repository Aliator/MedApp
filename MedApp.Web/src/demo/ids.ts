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
  carrow: id(0xd0, 8),
} as const;

export const TEAMS = {
  dermatology: id(0xd1, 1),
  gynaecology: id(0xd1, 2),
  urology: id(0xd1, 3),
  diabetes: id(0xd1, 4),
  biomechanics: id(0xd1, 5),
  lab: id(0xd1, 6),
} as const;

export const TEMPLATES = {
  skinLesion: id(0xd2, 1),
  inflammatorySkin: id(0xd2, 2),
  postMenopausalBleeding: id(0xd2, 3),
  diabetesReview: id(0xd2, 4),
  /** Branches at the first visit: biopsy first, or treat directly. */
  suspectedSkinCancer: id(0xd2, 5),
  /** A fork after the first visit (MC8): the biopsy and histology beside the bloods and lab tests, joined at the review. */
  lesionWithBloods: id(0xd2, 6),
} as const;

/** The skin lesion template's addable branches (MC10): routes a clinician may add on new information, never suggested. */
export const TEMPLATE_BRANCHES = {
  /** Wide excision and follow-up, after histology or the review: the tour adds it to Priya Holloway's pathway. */
  melanomaRoute: id(0xfc, 1),
  /** Another biopsy and its histology, after any stage, as often as needed. */
  repeatBiopsy: id(0xfc, 2),
} as const;

/** Stage definitions (the stage library), shared by the templates. */
export const STAGES = {
  /** Every definition below; the ones after `clinicReview` came with the light demo's second seed. */
  firstClinicVisit: id(0xd3, 1),
  biopsyOrSample: id(0xd3, 2),
  histology: id(0xd3, 3),
  resultsAndTeamReview: id(0xd3, 4),
  treatment: id(0xd3, 5),
  followUp: id(0xd3, 6),
  dischargeLetter: id(0xd3, 7),
  medicinesReview: id(0xd3, 8),
  bloodAndUrineTests: id(0xd3, 9),
  labTests: id(0xd3, 10),
  clinicReview: id(0xd3, 11),
  /** Excision straight away, without a biopsy first (the branching template's second branch). */
  excision: id(0xd3, 12),
} as const;

export const PATIENTS = {
  /** Has the skin lesion pathway waiting for its biopsy to be booked. */
  ellisVarden: id(0xd4, 1),
  /** Has the skin lesion pathway at histology: its stage can be finished. */
  priyaHolloway: id(0xd4, 2),
  /** Has the inflammatory skin review on hold. */
  samuelOkafor: id(0xd4, 3),
  /** Has the overdue post-menopausal bleeding pathway. */
  margaretFenwick: id(0xd4, 4),
  daniyalRees: id(0xd4, 5),
  graceAbernethy: id(0xd4, 6),
  tobiasLund: id(0xd4, 7),
  ilonaPetrova: id(0xd4, 8),
  /** Referred to Dermatology: awaiting triage. */
  oliverMaddox: id(0xd4, 9),
  aishaKhoury: id(0xd4, 10),
  ruthPenrose: id(0xd4, 11),
  callumAshby: id(0xd4, 12),
  /** Has the skin lesion pathway at Treatment: the excision was done, and its consent isn't recorded yet. */
  harrietQuill: id(0xd4, 13),
  /** Has the suspected skin cancer pathway at its branch point: biopsy first, or treat directly. */
  felixMoreton: id(0xd4, 14),
} as const;

export const PATHWAYS = {
  /** Skin lesion, Ellis Varden, Nadia Thornbury's: the biopsy (a visit) is next and not booked. */
  biopsyToBook: id(0xd5, 1),
  /** Skin lesion, Priya Holloway, Nadia Thornbury's: at histology, ready to finish. */
  histologyToFinish: id(0xd5, 2),
  /** Inflammatory skin review, Samuel Okafor: on hold. */
  onHold: id(0xd5, 3),
  /** Post-menopausal bleeding, Margaret Fenwick, Tom Hallworth's (Gynaecology): overdue. */
  overdue: id(0xd5, 4),
  /** Diabetes review, Daniyal Rees: assigned, not started. */
  notStarted: id(0xd5, 5),
  /** Diabetes review, Grace Abernethy: discharged. */
  completed: id(0xd5, 6),
  /** Skin lesion, Tobias Lund: nobody assigned yet. */
  unassigned: id(0xd5, 7),
  /** Inflammatory skin review, Ilona Petrova, Nadia Thornbury's: follow-up booked this week. */
  followUpBooked: id(0xd5, 8),
  /** Skin lesion, Harriet Quill, Nadia Thornbury's: at Treatment, which needs a consent record before it's finished. */
  consentToRecord: id(0xd5, 9),
  /** Suspected skin cancer, Felix Moreton, Nadia Thornbury's: at the first visit, a branch point (biopsy or treat). */
  branchToChoose: id(0xd5, 10),
  /** Post-menopausal bleeding, Priya Holloway, Gynaecology's: beside her Dermatology one, behind the team wall; at Histology, new for the lab. */
  priyaGynaecology: id(0xd5, 11),
  /** Diabetes review, Ellis Varden, Diabetes's: discharged; Dermatology sees it only as "completed". */
  ellisDiabetes: id(0xd5, 12),
  /** Skin lesion with blood tests, Aisha Khoury, Nadia Thornbury's: two current stages side by side (histology, lab tests). */
  forkedTests: id(0xd5, 13),
  /** Skin lesion, Callum Ashby, Nadia Thornbury's: back from the lab (Histology finished), not picked up yet. */
  backFromLab: id(0xd5, 14),
} as const;

export const REFERRALS = {
  /** Oliver Maddox to Dermatology, awaiting triage: the tour's triage step. */
  dermatologyToTriage: id(0xd6, 1),
  aishaDermatology: id(0xd6, 2),
  ruthGynaecology: id(0xd6, 3),
  callumDiabetes: id(0xd6, 4),
  /** Ellis Varden's, accepted into the biopsy pathway. */
  ellisAccepted: id(0xd6, 5),
} as const;

export const APPOINTMENTS = {
  margaretFirstVisit: id(0xd7, 1),
  ilonaFollowUp: id(0xd7, 2),
  ellisFirstVisit: id(0xd7, 3),
  priyaFirstVisit: id(0xd7, 4),
  priyaBiopsy: id(0xd7, 5),
  daniyalBloods: id(0xd7, 6),
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
  /** Harriet Quill's excision, attended this morning (its stage needs a consent record to finish). */
  harrietExcision: id(0xf9, 1),
  harrietFirstVisit: id(0xf9, 2),
  harrietBiopsy: id(0xf9, 3),
  felixFirstVisit: id(0xf9, 4),
  /** Priya Holloway's Gynaecology biopsy with Tom Hallworth: "Busy", with no patient, on his calendar for Dermatology. */
  priyaGynaecologyBiopsy: id(0xf9, 5),
} as const;

/** Every record ID in the seed, by kind (the tour's test checks its links against these). */
export const ALL_IDS = {USERS, TEAMS, TEMPLATES, STAGES, PATIENTS, PATHWAYS, REFERRALS, APPOINTMENTS, MEETINGS} as const;

/** A new record's ID, for what a visitor makes. */
export const newId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx'.replace(/x/g, () => Math.floor(Math.random() * 16).toString(16));
