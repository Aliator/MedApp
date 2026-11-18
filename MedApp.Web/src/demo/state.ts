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
  specialtyId: string;
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
  assignments: {userId: string; assignedByUserId: string; assignedAtUtc: string}[];
  notes: DemoNote[];
  /** Changes of status (put on hold, taken off it, discharged), for the timeline. */
  statusChanges: {atUtc: string; userId: string; from: DemoPathway['status']; to: DemoPathway['status']}[];
}

export interface DemoReferral {
  id: string;
  patientId: string;
  specialtyId: string;
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
  specialties: Schemas['SpecialtyResponse'][];
  /** The stage library. */
  stageDefinitions: DemoStageDefinition[];
  templates: Schemas['PathwayTemplateResponse'][];
  pathways: DemoPathway[];
  noteDeletionRequests: DemoNoteDeletionRequest[];
  referrals: DemoReferral[];
  appointments: Schemas['AppointmentResponse'][];
  blocks: Schemas['CalendarBlockResponse'][];
  meetings: Schemas['MeetingResponse'][];
  notifications: DemoNotification[];
  /** Each person's notepad, by user ID. */
  notepads: Record<string, Schemas['NotepadResponse']>;
  /** Each person's saved app settings (a JSON string, as the API keeps them), by user ID. */
  settings: Record<string, string>;
}
