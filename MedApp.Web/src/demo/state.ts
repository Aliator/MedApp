import type {Schemas} from '../api/client';

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
  notes: string | null;
  startedAtUtc: string | null;
  startedByUserId: string | null;
  completedAtUtc: string | null;
  completedByUserId: string | null;
}

export interface DemoNote {
  id: string;
  authorUserId: string;
  text: string;
  createdAtUtc: string;
  lastEditedAtUtc: string | null;
  version: number;
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

export interface DemoState {
  /** The seed's shape: an older one is seeded again. */
  version: number;
  /** The UK date it was seeded on (dates in it are relative to this). */
  seededOn: string;
  /** Who's signed in (a username), or null. */
  user: string | null;
  patients: Schemas['PatientResponse'][];
  specialties: Schemas['SpecialtyResponse'][];
  templates: Schemas['PathwayTemplateResponse'][];
  pathways: DemoPathway[];
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
