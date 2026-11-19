import type {Schemas} from '../api/client';
import type {ClinicalSlice} from './slices/clinical';
import type {LabSlice} from './slices/lab';
import type {SetupSlice} from './slices/setup';

/**
 * Everything the light demo holds, kept in this browser's IndexedDB (`store.ts`) and changed by the handlers. Records
 * are kept close to the API's DTOs; what depends on who's looking (`can...` flags, overdue, the current stage) is
 * worked out as each answer is made, and a patient's timeline is read from their records (`handlers/timeline.ts`).
 */

/** A stage of a pathway, as stored. */
export interface DemoStage {
  id: string;
  stageDefinitionId: string;
  stageOrder: number;
  name: string;
  description: string | null;
  visitRequirement: 'None' | 'Suggested' | 'Required';
  /** The letter kind that must be approved and sent before it's finished (the letter gate), copied from its definition. */
  needsLetterKind: string | null;
  /** Needs a Valid consent record before it's finished (the consent gate), copied from its definition. */
  needsConsent: boolean;
  /** Waits for the pathway's results to be signed off before it's finished (the results gate), from its definition. */
  waitsForResults: boolean;
  isOptional: boolean;
  /** A branch point: finishing it chooses one of the branches after it. */
  choosesBranch: boolean;
  /** The branch it's in, or null. */
  branch: string | null;
  /** A branch point's chosen branch, once it's been finished (it changes only by reinstating a stage). */
  chosenBranch: string | null;
  /** A finished stage reopened by someone who can unlock stages: editable until it's finished again. */
  unlocked: boolean;
  notes: string | null;
  startedAtUtc: string | null;
  startedByUserId: string | null;
  completedAtUtc: string | null;
  completedByUserId: string | null;
  skippedAtUtc: string | null;
  skippedByUserId: string | null;
  /** `StageSkipReason` by name: BranchNotChosen, NotNeeded, PatientDeclined or Other. */
  skipReason: string | null;
  skipDetail: string | null;
  /** The branch addition that added it (MC10, a template's addable branch), or none for a stage from the template. */
  branchAdditionId?: string | null;
  /** The team that works it (the relay, MC7): the template's choice, or null for the pathway's own team. */
  ownerTeamId: string | null;
  /** When it became the current stage, or null if it hasn't yet. */
  reachedAtUtc: string | null;
  /** The team that handed it over when it was reached (its owner differs from the stage before's), or null. */
  handedOverFromTeamId: string | null;
  /** When the receiving team picked it up (or started or finished it), after a handover. */
  pickedUpAtUtc: string | null;
  pickedUpByUserId: string | null;
}

/**
 * A template's addable branch added to a pathway on new information (MC10, `PathwayBranchAddition`): which, after which
 * stage, the stages it added, and the clinician's reason (free text: kept here, never in the audit log).
 */
export interface DemoBranchAddition {
  id: string;
  templateBranchId: string;
  name: string;
  afterStageId: string;
  stageIds: string[];
  reason: string;
  addedByUserId: string;
  addedAtUtc: string;
}

/** A link from one stage of a pathway to the next (`PatientPathwayStageLinks`): a fork has several from a stage, a join several to it. */
export interface DemoStageLink {
  fromStageId: string;
  toStageId: string;
}

export interface DemoNote {
  id: string;
  authorUserId: string;
  text: string;
  createdAtUtc: string;
  lastEditedAtUtc: string | null;
  version: number;
  /** Deleted by an admin (soft: hidden from the pathway, kept for its history), or null. */
  deleted: {atUtc: string; byUserId: string; reason: string; detail: string | null} | null;
  /** A member of staff's request to delete it, while it's waiting for a decision. */
  pendingRequestId: string | null;
}

/** A request to delete a pathway's note, which someone who can correct records decides. */
export interface DemoNoteDeletionRequest {
  id: string;
  pathwayId: string;
  noteId: string;
  status: 'Pending' | 'Approved' | 'Declined' | 'Closed';
  reason: string;
  detail: string | null;
  requestedByUserId: string;
  requestedAtUtc: string;
  decidedByUserId: string | null;
  decidedAtUtc: string | null;
}

export interface DemoPathway {
  id: string;
  patientId: string;
  templateId: string;
  templateName: string;
  teamId: string;
  referralId: string | null;
  title: string;
  /** The target date (a UK date). */
  dueDate: string;
  priority: 'Low' | 'Medium' | 'High' | 'Urgent';
  status: 'Unassigned' | 'Assigned' | 'InProgress' | 'InReview' | 'Completed' | 'Cancelled' | 'Blocked' | 'Archived';
  blockedFromStatus: DemoPathway['status'] | null;
  createdAt: string;
  lastUpdated: string;
  stages: DemoStage[];
  /** How its stages lead to each other (MC8, tree pathways): copied from the template's links, by stage ID. */
  links: DemoStageLink[];
  /** Branches added on new information (MC10); none when it has had none. */
  branchAdditions?: DemoBranchAddition[];
  assignments: {userId: string; assignedByUserId: string; assignedAtUtc: string}[];
  notes: DemoNote[];
  /** Each handover between teams (the relay), for the audit log: by IDs and team names, never free text. */
  handovers: {stageId: string; fromTeamId: string; toTeamId: string; atUtc: string; byUserId: string}[];
  /** Changes of status (put on hold, taken off it, discharged), for the timeline. */
  statusChanges: {atUtc: string; userId: string; from: DemoPathway['status']; to: DemoPathway['status']}[];
}

export interface DemoReferral {
  id: string;
  patientId: string;
  teamId: string;
  receivedOn: string;
  referrerPractice: string;
  referrerClinician: string;
  requestedPriority: 'Low' | 'Medium' | 'High' | 'Urgent';
  reason: string;
  status: 'AwaitingTriage' | 'Accepted' | 'Rejected' | 'RedirectedElsewhere' | 'Withdrawn' | 'EnteredInError' | 'InformationRequested';
  registeredAtUtc: string;
  registeredByUserId: string;
  decidedAtUtc: string | null;
  decidedByUserId: string | null;
  rejectionReason: string | null;
  decisionDetail: string | null;
  redirectedTo: string | null;
  pathwayId: string | null;
  version: number;
  lastUpdated: string;
  history: Schemas['ReferralEventResponse'][];
}

export type DemoNotification = Schemas['NotificationResponse'] & {userId: string};

/** An appointment, as stored: the response's fields and its team (its pathway's, else the clinician's chosen at booking). */
export type DemoAppointment = Schemas['AppointmentResponse'] & {teamId: string | null};

/**
 * A grant to see a pathway in full (`PathwayAccess`, decision 201 of `docs/multi-clinic-plan.md`): `Owner` for the
 * pathway's own team, written with it; `StageOwner` for the lab once a stage it owns is reached (or a sample is passed
 * to it), never ended (decision 184). Kept after the pathway is deleted. The rules that read them are in `handlers/teamWalls.ts`.
 */
export interface DemoPathwayAccess {
  pathwayId: string;
  patientId: string;
  teamId: string;
  reason: 'Owner' | 'StageOwner';
  startedAtUtc: string;
  endedAtUtc: string | null;
}

/**
 * Everything the light demo holds: the base (below) and each area's slice, each in its own file under `slices/`, with
 * its own seed function (`seed.ts` calls them).
 */
export type DemoState = BaseState & LabSlice & ClinicalSlice & SetupSlice;

/** A stage definition in the stage library (`PatientPathwayStageDefinitionResponse`, without its usage counts). */
export type DemoStageDefinition = Omit<Schemas['PatientPathwayStageDefinitionResponse'], 'templateCount' | 'canBeDeleted'> & {id: string};

export interface BaseState {
  /** The seed's shape: an older one is seeded again. */
  version: number;
  /** The UK date it was seeded on (dates in it are relative to this). */
  seededOn: string;
  /** Who's signed in (a username), or null. */
  user: string | null;
  patients: Schemas['PatientResponse'][];
  teams: Schemas['TeamResponse'][];
  /** The stage library. */
  stageDefinitions: DemoStageDefinition[];
  templates: Schemas['PathwayTemplateResponse'][];
  pathways: DemoPathway[];
  noteDeletionRequests: DemoNoteDeletionRequest[];
  referrals: DemoReferral[];
  appointments: DemoAppointment[];
  /** Who may see each pathway in full, beyond admins (`handlers/teamWalls.ts`). */
  pathwayAccess: DemoPathwayAccess[];
  blocks: Schemas['CalendarBlockResponse'][];
  meetings: Schemas['MeetingResponse'][];
  notifications: DemoNotification[];
  /** Each person's notepad, by user ID. */
  notepads: Record<string, Schemas['NotepadResponse']>;
  /** Each person's saved app settings (a JSON string, as the API keeps them), by user ID. */
  settings: Record<string, string>;
}
