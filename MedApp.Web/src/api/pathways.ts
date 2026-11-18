import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type PathwaySummary = Schemas['PatientPathwaySummaryResponse'];
export type Pathway = Schemas['PatientPathwayResponse'];
export type PathwayStage = Schemas['PatientPathwayStageResponse'];
/** One of a pathway's notes: its author, times, version and whether you can edit it (only your own). */
export type PathwayNote = Schemas['PatientPathwayNoteResponse'];
/** A member of staff's request to delete a note, as an admin decides it: the note, the patient, who asked and why. */
export type NoteDeletionRequest = Schemas['NoteDeletionRequestResponse'];

/**
 * Every pathway query is keyed under ['pathways'], so a change to any pathway refreshes them all.
 * A patient's pathway count is under ['patients', id, ...] too, so patient writes refresh it.
 */
export const pathwayKeys = {
  all: ['pathways'] as const,
  /** Every patient's timeline (`src/api/timeline.ts`), which a change to any pathway adds to. */
  timelines: ['pathways', 'timeline'] as const,
  countForPatient: (patientId: string) => ['patients', patientId, 'pathways', 'count'] as const,
};

/** One pathway in full (`GET api/pathways/patient-pathways/{id}`). A missing one is an ApiError with status 404. */
export function pathwayQuery(id: string) {
  return queryOptions({
    queryKey: ['pathways', id],
    queryFn: () => unwrap(api.GET('/api/pathways/patient-pathways/{id}', {params: {path: {id}}})),
  });
}

/**
 * The titles and patients' names of these pathways (`GET api/pathways/patient-pathways/labels`), for the side
 * nav's pins: labels, not a view of the records, so the API records nothing. Pathways that no longer exist are left
 * out. One request for every pin; keyed under ['pathways'], so a change to a pathway refreshes it.
 */
export function pathwayLabelsQuery(ids: readonly string[]) {
  const sorted = [...new Set(ids)].sort();
  return queryOptions({
    queryKey: ['pathways', 'labels', sorted],
    queryFn: () => unwrap(api.GET('/api/pathways/patient-pathways/labels', {params: {query: {ids: sorted}}})),
    enabled: sorted.length > 0,
    placeholderData: keepPreviousData,
  });
}

/**
 * How many of a patient's pathways are under way: not completed, cancelled or archived (a search's default; blocked
 * ones count). Keyed under the patient, so patient and pathway writes refresh it.
 */
export function patientOpenPathwayCountQuery(patientId: string) {
  return queryOptions({
    queryKey: ['patients', patientId, 'pathways', 'open-count'],
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/pathways/patient-pathways/search', {params: {query: {PatientId: patientId, PageSize: 1}}}),
      );
      return page.totalCount ?? 0;
    },
  });
}

/** How many pathways a patient has, in any status (one row of a search, for its total). */
export function patientPathwayCountQuery(patientId: string) {
  return queryOptions({
    queryKey: pathwayKeys.countForPatient(patientId),
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/pathways/patient-pathways/search', {
          params: {query: {PatientId: patientId, IncludeClosed: true, PageSize: 1}},
        }),
      );
      return page.totalCount ?? 0;
    },
  });
}

const PATHWAY = '/api/pathways/patient-pathways/{id}';

/** Finishing a stage that involves a visit without an attended one: `withoutVisit`, and an admin's reason. */
export type FinishStageRequest = Schemas['FinishPathwayStageRequest'];

/**
 * Everything staff (and admins) can do to a pathway from its page. Each action returns the updated pathway,
 * which replaces the cached one, so the page shows the result at once; lists and searches are refreshed too.
 * Admin-only actions (assign, unassign, unlock) are refused by the API for anyone else.
 */
export function usePathwayActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = async (pathway: Pathway) => {
    queryClient.setQueryData(pathwayQuery(id).queryKey, pathway);
    await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
    await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
    // Finishing a stage, or cancelling the pathway, cancels Requested visits and can request the next stage's.
    await queryClient.invalidateQueries({queryKey: ['appointments']});
  };
  const stage = (stageId: string) => ({params: {path: {id, stageId}}});

  return {
    startStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PATHWAY}/stages/{stageId}/start`, stage(stageId))),
      onSuccess,
    }),
    saveNotes: useMutation({
      mutationFn: ({stageId, notes}: {stageId: string; notes: string}) =>
        unwrap(api.PUT(`${PATHWAY}/stages/{stageId}/notes`, {...stage(stageId), body: {notes}})),
      onSuccess,
    }),
    /**
     * Finishes a stage: by its ID, or with `withoutVisit` (and an admin's `reason`) to finish one that involves a visit
     * without an attended one (docs/calendar-plan.md, decisions 24 and 25).
     */
    finishStage: useMutation({
      mutationFn: (finish: string | ({stageId: string} & FinishStageRequest)) => {
        const {stageId, ...body} = typeof finish === 'string' ? {stageId: finish} : finish;
        return unwrap(
          api.POST(`${PATHWAY}/stages/{stageId}/finish`, {...stage(stageId), ...(Object.keys(body).length > 0 ? {body} : {})}),
        );
      },
      onSuccess,
    }),
    unlockStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PATHWAY}/stages/{stageId}/unlock`, stage(stageId))),
      onSuccess,
    }),
    changeStatus: useMutation({
      mutationFn: (change: {status: string; reason?: string}) =>
        unwrap(api.POST(`${PATHWAY}/status`, {params: {path: {id}}, body: change})),
      onSuccess,
    }),
    assign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.POST(`${PATHWAY}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
    unassign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.DELETE(`${PATHWAY}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
  };
}

export type PathwayTemplate = Schemas['PathwayTemplateResponse'];

/** What New pathway sends: the patient, the template it's built from, and its details. */
export type NewPathwayInput = Schemas['CreatePatientPathwayRequest'];

/** A pathway's editable details: title, target date and priority. Notes are added one at a time (`usePathwayNoteActions`). */
export interface PathwayDetailsInput {
  title: string;
  /** A date, `YYYY-MM-DD`; due by the end of that day, UK time. */
  dueDate: string;
  priority: string;
}

/** A new pathway's details: its notes, if any, become its first note, by whoever creates it. */
export interface NewPathwayDetailsInput extends PathwayDetailsInput {
  notes: string;
}

/** The templates staff can create a pathway from (archived ones are left out). */
export function pathwayTemplatesQuery() {
  return queryOptions({
    queryKey: ['pathwayTemplates'],
    queryFn: () => unwrap(api.GET('/api/pathways/patient-pathway-stage/templates')),
  });
}

/** Creates a pathway from a template, returning it; lists and the patient's pathway count refresh. */
export function useCreatePathway() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: NewPathwayInput) => unwrap(api.POST('/api/pathways/patient-pathways', {body: input})),
    onSuccess: async (pathway) => {
      queryClient.setQueryData(pathwayQuery(pathway.id ?? '').queryKey, pathway);
      await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
      await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
      await queryClient.invalidateQueries({queryKey: pathwayKeys.countForPatient(pathway.patientId ?? '')});
    },
  });
}

/** Saves a pathway's details, returning it updated; it replaces the cached one, and lists refresh. */
export function useUpdatePathway(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: PathwayDetailsInput) =>
      unwrap(api.PATCH(PATHWAY, {params: {path: {id}}, body: input})),
    onSuccess: async (pathway) => {
      queryClient.setQueryData(pathwayQuery(id).queryKey, pathway);
      await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
      await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
    },
  });
}

/**
 * Adding a note to a pathway, and editing one of your own (from the version you saw: someone, or another tab, saving
 * it first is a 409, whose message says to reload). Each returns the note, which goes into the cached pathway at
 * once (newest first), so it shows straight away wherever the pathway is open; the pathway (its last updated
 * time), lists and timelines then refresh.
 */
export function usePathwayNoteActions(id: string) {
  const show = useShowNote();
  return {
    add: useMutation({
      mutationFn: (text: string) => unwrap(api.POST(`${PATHWAY}/notes`, {params: {path: {id}}, body: {text}})),
      onSuccess: (note) => show(id, note),
    }),
    edit: useMutation({
      mutationFn: ({noteId, text, expectedVersion}: {noteId: string; text: string; expectedVersion: number}) =>
        unwrap(api.PUT(`${PATHWAY}/notes/{noteId}`, {params: {path: {id, noteId}}, body: {text, expectedVersion}})),
      onSuccess: (note) => show(id, note),
    }),
  };
}

/**
 * Adding a note to whichever pathway is given (the notepad's Add Note to Pathway, which can choose between two
 * open ones). Like `usePathwayNoteActions`' `add`, the note shows on that pathway's page at once.
 */
export function useAddPathwayNote() {
  const show = useShowNote();
  return useMutation({
    mutationFn: ({pathwayId, text}: {pathwayId: string; text: string}) =>
      unwrap(api.POST(`${PATHWAY}/notes`, {params: {path: {id: pathwayId}}, body: {text}})),
    onSuccess: (note, {pathwayId}) => show(pathwayId, note),
  });
}

/** Puts a saved note into its cached pathway (newest first), then refreshes the pathway, lists and timelines. */
function useShowNote() {
  const queryClient = useQueryClient();
  return async (pathwayId: string, note: PathwayNote) => {
    queryClient.setQueryData(pathwayQuery(pathwayId).queryKey, (pathway) =>
      pathway && {...pathway, noteList: [note, ...(pathway.noteList ?? []).filter((n) => n.id !== note.id)].sort(newestFirst)},
    );
    await queryClient.invalidateQueries({queryKey: pathwayQuery(pathwayId).queryKey});
    await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
    await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
  };
}

/** Notes newest first, as the API sends them. */
const newestFirst = (a: PathwayNote, b: PathwayNote) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? '');

/** Reads the pathway afresh: after a note was changed somewhere else (a 409), to show it as it is now. */
export function useReloadPathway(id: string) {
  const queryClient = useQueryClient();
  // Read again even where nothing shows it now (a note's conflict can be seen from the notepad, too).
  return () => queryClient.invalidateQueries({queryKey: pathwayQuery(id).queryKey, refetchType: 'all'});
}

/** Why a note is being deleted, or should be: one of the API's reasons, and an optional detail. */
export interface NoteDeletionReasonInput {
  reason: string;
  /** Sent as none when empty. */
  detail?: string | null;
}

const detailOrNull = (detail: string | null | undefined) => (detail?.trim() ? detail.trim() : null);

/**
 * Deleting a note (admins, with a reason, from the version they read: `expectedVersion`, so a note edited since is a
 * 409) and asking an admin to delete one (staff, with a reason). Deleting is soft: the API keeps the note, hidden, so
 * it's taken out of the cached pathway at once; asking returns the note, which then shows "Deletion requested". The
 * pathway, lists and timelines then refresh, and after a failure the pathway is read again, so a 409 (asked or
 * changed meanwhile) shows the note as it is now.
 */
export function useNoteDeletionActions(id: string) {
  const queryClient = useQueryClient();
  const show = useShowNote();
  const reload = useReloadPathway(id);
  return {
    remove: useMutation({
      mutationFn: ({noteId, reason, detail, expectedVersion}: NoteDeletionReasonInput & {noteId: string; expectedVersion: number}) =>
        unwrap(
          api.POST(`${PATHWAY}/notes/{noteId}/delete`, {
            params: {path: {id, noteId}},
            body: {reason, detail: detailOrNull(detail), expectedVersion},
          }),
        ),
      onError: () => void reload(),
      onSuccess: async (_, {noteId}) => {
        queryClient.setQueryData(pathwayQuery(id).queryKey, (pathway) =>
          pathway && {...pathway, noteList: (pathway.noteList ?? []).filter((n) => n.id !== noteId)},
        );
        await queryClient.invalidateQueries({queryKey: pathwayQuery(id).queryKey});
        await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
        await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
      },
    }),
    request: useMutation({
      mutationFn: ({noteId, reason, detail}: NoteDeletionReasonInput & {noteId: string}) =>
        unwrap(
          api.POST(`${PATHWAY}/notes/{noteId}/deletion-requests`, {
            params: {path: {id, noteId}},
            body: {reason, detail: detailOrNull(detail)},
          }),
        ),
      onSuccess: (note) => show(id, note),
      onError: () => void reload(),
    }),
  };
}

/** Keyed under ['pathways'], so a change to any pathway (a note deleted) reads it afresh. */
export const noteDeletionRequestKey = (id: string) => ['pathways', 'note-deletion-requests', id] as const;

/** A request to delete a note (`GET api/pathways/note-deletion-requests/{id}`, admins). Reading it is recorded as a view. */
export function noteDeletionRequestQuery(id: string) {
  return queryOptions({
    queryKey: noteDeletionRequestKey(id),
    queryFn: () => unwrap(api.GET('/api/pathways/note-deletion-requests/{id}', {params: {path: {id}}})),
  });
}

/** An admin's decision: keep the note, or delete it from the version they read, with a reason. */
export type NoteDeletionDecision = {deleteNote: false} | ({deleteNote: true; expectedVersion: number} & NoteDeletionReasonInput);

/**
 * An admin's decision on a request to delete a note: delete it (from the version they read, with the reason given) or
 * keep it. The request comes back decided; its pathway (the note gone, or no longer waiting), lists and timelines
 * refresh.
 */
export function useDecideNoteDeletion(requestId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (decision: NoteDeletionDecision) =>
      unwrap(
        api.POST('/api/pathways/note-deletion-requests/{id}/decision', {
          params: {path: {id: requestId}},
          body: decision.deleteNote
            ? {deleteNote: true, expectedVersion: decision.expectedVersion, reason: decision.reason, detail: detailOrNull(decision.detail)}
            : {deleteNote: false},
        }),
      ),
    onSuccess: async (decided) => {
      queryClient.setQueryData(noteDeletionRequestKey(requestId), decided);
      await queryClient.invalidateQueries({queryKey: pathwayQuery(decided.pathwayId ?? '').queryKey});
      await queryClient.invalidateQueries({queryKey: ['pathways', 'search']});
      await queryClient.invalidateQueries({queryKey: pathwayKeys.timelines});
    },
  });
}

/** Deletes a pathway (admins, and only once it's cancelled or archived). */
export function useDeletePathway(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE(PATHWAY, {params: {path: {id}}})),
    onSuccess: async () => {
      // Its own query would only 404 if refetched, so drop it rather than refresh it.
      queryClient.removeQueries({queryKey: ['pathways', id]});
      await queryClient.invalidateQueries({queryKey: ['pathways']});
      await queryClient.invalidateQueries({queryKey: ['patients']});
    },
  });
}

/** Decisions 13 and 20: only an admin can delete a pathway, and only one the API allows (cancelled or archived). */
export const canDeletePathway = (pathway: Pathway, isAdmin: boolean) => isAdmin && (pathway.canBeDeleted ?? false);

export const PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
