/**
 * The light demo's fixed IDs: the seed makes every record with one of these, so the walkthrough can open an exact
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
  varma: id(0xd0, 8),
  ferrand: id(0xd0, 9),
  castell: id(0xd0, 10),
} as const;

export const TEAMS = {
  /** The two clinics and the lab (20 Nov: Dermatology, the Eye clinic and the lab). */
  dermatology: id(0xd1, 1),
  eyeClinic: id(0xd1, 7),
  lowerLimb: id(0xd1, 8),
  lab: id(0xd1, 6),
} as const;

/** Each team's name, in the order the seed makes them. */
export const TEAM_NAMES: [string, string][] = [
  [TEAMS.dermatology, 'Dermatology'],
  [TEAMS.eyeClinic, 'Eye clinic'],
  [TEAMS.lowerLimb, 'Lower limb biomechanics'],
  // The lab (decision 182): a team of its own kind, serving every clinic.
  [TEAMS.lab, 'Lab'],
];

export const TEMPLATES = {
  /** Dermatology's ten (the owner's reviewed templates, `docs/pathways/dermatology.yaml`, mapped in `clinicTemplates.ts`). */
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
  /** Not one of the ten: the tour's three-stage relay (`TOUR_RELAY` in `clinicTemplates.ts`). */
  skinSample: id(0xd2, 16),
  /** The Eye clinic's four (`docs/pathways/ophthalmology.yaml`, copied in `ophthalmologyReviewed.ts`). */
  cataract: id(0xd2, 17),
  glaucoma: id(0xd2, 18),
  redEye: id(0xd2, 19),
  macular: id(0xd2, 20),
  lowerLimb: id(0xd2, 21),
} as const;

/** Addable routes (MC10): routes a clinician may add on new information, never suggested. */
export const TEMPLATE_BRANCHES = {
  /** Suspected skin cancer's "Biopsy another lesion" (Take biopsy, then Histology), after Dermoscopy assessment. */
  biopsyAnother: id(0xfc, 1),
} as const;

/** The stage library's own definition, which Dermatology's reviewed templates share; theirs are numbered from
 * `id(0xd3, 0x20)` (`reviewedStageDefinitions`). */
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
  /** The tour's relay (the owner, 19 November 2026): at Triage, her consent to record, the lab's test next. */
  leahCorrin: id(0xd4, 16),
  /** Has the changing mole pathway at Take photographs, which needs her consent to clinical photography (MC27, MC29). */
  noraFennick: id(0xd4, 15),
  /** The Eye clinic's: cataract, at Biometry (booked). */
  edithMarlow: id(0xd4, 17),
  /** The Eye clinic's: red eye, her swab with the lab (an open lab case). */
  freyaTolland: id(0xd4, 18),
  /** The Eye clinic's: macular, at Triage, nobody assigned. */
  hamishDewar: id(0xd4, 19),
  /** Referred to the Eye clinic: awaiting triage. */
  ruthPenhaligon: id(0xd4, 20),
  joelBrannock: id(0xd4, 21),
} as const;

export const PATHWAYS = {
  /** Suspected skin cancer, Ellis Varden, Nadia Thornbury's: a non-healing scaly lesion, at Take biopsy (a visit, not booked; his consent was withdrawn). */
  biopsyToBook: id(0xd5, 1),
  /** Suspected skin cancer, Priya Holloway, Nadia Thornbury's: with the lab at Histology, ready to finish. */
  histologyToFinish: id(0xd5, 2),
  /** Suspected skin infection, Samuel Okafor: on hold, his scraping sent, so the lab's Microbiology is current. */
  onHold: id(0xd5, 3),
  /** Changing mole or lesion check, Tobias Lund: at Triage, nobody assigned yet. */
  unassigned: id(0xd5, 7),
  /** Inflammatory skin assessment, Ilona Petrova, Nadia Thornbury's: blood tests with the lab; the results visit booked. */
  followUpBooked: id(0xd5, 8),
  /** Suspected skin cancer, Harriet Quill, Nadia Thornbury's: Low-risk BCC recorded; at Refer to hospital (needs its letter), overdue. */
  referredToHospital: id(0xd5, 9),
  /** Suspected skin cancer, Felix Moreton, Nadia Thornbury's: at Dermoscopy assessment, a choice node with six outcomes. */
  branchToChoose: id(0xd5, 10),
  /** Suspected skin cancer, Aisha Khoury, Nadia Thornbury's: Biopsy another lesion added, so two Histology stages run side by side. */
  forkedTests: id(0xd5, 13),
  /** Suspected skin cancer, Callum Ashby, Nadia Thornbury's: back from the lab at Histology result (a choice node), not picked up yet. */
  backFromLab: id(0xd5, 14),
  /** Changing mole or lesion check, Nora Fennick, Nadia Thornbury's: at Take photographs, no photo consent yet (MC29). */
  photosToTake: id(0xd5, 15),
  /** The tour's relay: Triage (a visit, consent needed), the lab's Testing, then a Results appointment (Leah Corrin). */
  relay: id(0xd5, 16),
  /** Suspected skin infection, Ilona Petrova, Nadia Thornbury's: at Take skin sample, seen this morning, her scraping not yet sent. */
  scrapingToSend: id(0xd5, 17),
  /** Cataract assessment, Edith Marlow, Saira Varma's: Wants surgery and suitable recorded; at Biometry, booked. */
  eyeCataract: id(0xd5, 18),
  /** Glaucoma assessment, Oliver Maddox (also referred to Dermatology), Saira Varma's: at Glaucoma assessment, booked. */
  eyeGlaucoma: id(0xd5, 19),
  /** Red eye assessment, Freya Tolland, Saira Varma's: her swab received by the lab, at the lab's Microbiology. */
  eyeRedEye: id(0xd5, 20),
  /** Macular assessment, Hamish Dewar: at Triage, nobody assigned yet. */
  eyeMacular: id(0xd5, 21),
  lowerLimb: id(0xd5, 22),
} as const;

export const REFERRALS = {
  /** Oliver Maddox to Dermatology, awaiting triage: the tour's triage step. */
  dermatologyToTriage: id(0xd6, 1),
  aishaDermatology: id(0xd6, 2),
  /** Ellis Varden's, accepted into the biopsy pathway. */
  ellisAccepted: id(0xd6, 5),
  /** Ruth Penhaligon to the Eye clinic, awaiting triage. */
  eyeToTriage: id(0xd6, 6),
} as const;

export const APPOINTMENTS = {
  ilonaFollowUp: id(0xd7, 2),
  ellisFirstVisit: id(0xd7, 3),
  priyaFirstVisit: id(0xd7, 4),
  priyaBiopsy: id(0xd7, 5),
  /** The Eye clinic's visits, with Saira Varma. */
  edithAssessment: id(0xd7, 6),
  edithBiometry: id(0xd7, 7),
  oliverGlaucoma: id(0xd7, 8),
  freyaExam: id(0xd7, 9),
  joelAssessment: id(0xd7, 10),
  joelFitting: id(0xd7, 11),
} as const;

export const MEETINGS = {
  skinMdt: id(0xd8, 1),
} as const;

export const ACTIVITY_TYPES = {
  clinic: id(0xd9, 1),
  admin: id(0xd9, 2),
  lunch: id(0xd9, 3),
  triage: id(0xd9, 4),
  procedureList: id(0xd9, 5),
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
  /** Leah Corrin's Triage visit yesterday afternoon, where her skin scraping was taken (the tour's relay). */
  leahTriage: id(0xf9, 5),
  /** Ilona Petrova's blood test visits: the first sample (rejected by the lab), then the repeat. */
  ilonaBloods: id(0xf9, 6),
  ilonaRepeatBloods: id(0xf9, 7),
  /** Ilona Petrova's skin sample visit this morning. */
  ilonaSample: id(0xf9, 8),
} as const;

/** Every record ID in the seed, by kind (the tour's test checks its links against these). */
export const ALL_IDS = {USERS, TEAMS, TEMPLATES, STAGES, PATIENTS, PATHWAYS, REFERRALS, APPOINTMENTS, MEETINGS} as const;

/** A new record's ID, for what a visitor makes. */
export const newId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : 'xxxxxxxx-xxxx-4xxx-8xxx-xxxxxxxxxxxx'.replace(/x/g, () => Math.floor(Math.random() * 16).toString(16));
