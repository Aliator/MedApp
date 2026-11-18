import {keepPreviousData, queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type ProcedureSummary = Schemas['PatientProcedureSummaryResponse'];
export type Procedure = Schemas['PatientProcedureResponse'];
export type ProcedureStage = Schemas['PatientProcedureStageResponse'];
/** One of a procedure's notes: its author, times, version and whether you can edit it (only your own). */
export type ProcedureNote = Schemas['PatientProcedureNoteResponse'];
/** A member of staff's request to delete a note, as an admin decides it: the note, the patient, who asked and why. */
export type NoteDeletionRequest = Schemas['NoteDeletionRequestResponse'];

/**
 * Every procedure query is keyed under ['procedures'], so a change to any procedure refreshes them all.
 * A patient's procedure count is under ['patients', id, ...] too, so patient writes refresh it.
 */
export const procedureKeys = {
  all: ['procedures'] as const,
  /** Every patient's timeline (`src/api/timeline.ts`), which a change to any procedure adds to. */
  timelines: ['procedures', 'timeline'] as const,
  countForPatient: (patientId: string) => ['patients', patientId, 'procedures', 'count'] as const,
};

/** One procedure in full (`GET api/procedures/patient-procedures/{id}`). A missing one is an ApiError with status 404. */
export function procedureQuery(id: string) {
  return queryOptions({
    queryKey: ['procedures', id],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/{id}', {params: {path: {id}}})),
  });
}

/**
 * The titles and patients' names of these procedures (`GET api/procedures/patient-procedures/labels`), for the side
 * nav's pins: labels, not a view of the records, so the API records nothing. Procedures that no longer exist are left
 * out. One request for every pin; keyed under ['procedures'], so a change to a procedure refreshes it.
 */
export function procedureLabelsQuery(ids: readonly string[]) {
  const sorted = [...new Set(ids)].sort();
  return queryOptions({
    queryKey: ['procedures', 'labels', sorted],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedures/labels', {params: {query: {ids: sorted}}})),
    enabled: sorted.length > 0,
    placeholderData: keepPreviousData,
  });
}

/**
 * How many of a patient's procedures are under way: not completed, cancelled or archived (a search's default; blocked
 * ones count). Keyed under the patient, so patient and procedure writes refresh it.
 */
export function patientOpenProcedureCountQuery(patientId: string) {
  return queryOptions({
    queryKey: ['patients', patientId, 'procedures', 'open-count'],
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/procedures/patient-procedures/search', {params: {query: {PatientId: patientId, PageSize: 1}}}),
      );
      return page.totalCount ?? 0;
    },
  });
}

/** How many procedures a patient has, in any status (one row of a search, for its total). */
export function patientProcedureCountQuery(patientId: string) {
  return queryOptions({
    queryKey: procedureKeys.countForPatient(patientId),
    queryFn: async () => {
      const page = await unwrap(
        api.GET('/api/procedures/patient-procedures/search', {
          params: {query: {PatientId: patientId, IncludeClosed: true, PageSize: 1}},
        }),
      );
      return page.totalCount ?? 0;
    },
  });
}

const PROCEDURE = '/api/procedures/patient-procedures/{id}';

/** Finishing a stage that involves a visit without an attended one: `withoutVisit`, and an admin's reason. */
export type FinishStageRequest = Schemas['FinishProcedureStageRequest'];

/**
 * Everything staff (and admins) can do to a procedure from its page. Each action returns the updated procedure,
 * which replaces the cached one, so the page shows the result at once; lists and searches are refreshed too.
 * Admin-only actions (assign, unassign, unlock) are refused by the API for anyone else.
 */
export function useProcedureActions(id: string) {
  const queryClient = useQueryClient();
  const onSuccess = async (procedure: Procedure) => {
    queryClient.setQueryData(procedureQuery(id).queryKey, procedure);
    await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
    await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
    // Finishing a stage, or cancelling the procedure, cancels Requested visits and can request the next stage's.
    await queryClient.invalidateQueries({queryKey: ['appointments']});
  };
  const stage = (stageId: string) => ({params: {path: {id, stageId}}});

  return {
    startStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PROCEDURE}/stages/{stageId}/start`, stage(stageId))),
      onSuccess,
    }),
    saveNotes: useMutation({
      mutationFn: ({stageId, notes}: {stageId: string; notes: string}) =>
        unwrap(api.PUT(`${PROCEDURE}/stages/{stageId}/notes`, {...stage(stageId), body: {notes}})),
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
          api.POST(`${PROCEDURE}/stages/{stageId}/finish`, {...stage(stageId), ...(Object.keys(body).length > 0 ? {body} : {})}),
        );
      },
      onSuccess,
    }),
    unlockStage: useMutation({
      mutationFn: (stageId: string) => unwrap(api.POST(`${PROCEDURE}/stages/{stageId}/unlock`, stage(stageId))),
      onSuccess,
    }),
    changeStatus: useMutation({
      mutationFn: (change: {status: string; reason?: string}) =>
        unwrap(api.POST(`${PROCEDURE}/status`, {params: {path: {id}}, body: change})),
      onSuccess,
    }),
    assign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.POST(`${PROCEDURE}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
    unassign: useMutation({
      mutationFn: (userId: string) =>
        unwrap(api.DELETE(`${PROCEDURE}/assignments/{userId}`, {params: {path: {id, userId}}})),
      onSuccess,
    }),
  };
}

export type ProcedureTemplate = Schemas['PatientProcedureStageTemplateResponse'];

/** What New procedure sends: the patient, the template it's built from, and its details. */
export type NewProcedureInput = Schemas['CreatePatientProcedureRequest'];

/** A procedure's editable details: title, due date and priority. Notes are added one at a time (`useProcedureNoteActions`). */
export interface ProcedureDetailsInput {
  title: string;
  /** A date, `YYYY-MM-DD`; due by the end of that day, UK time. */
  dueDate: string;
  priority: string;
}

/** A new procedure's details: its notes, if any, become its first note, by whoever creates it. */
export interface NewProcedureDetailsInput extends ProcedureDetailsInput {
  notes: string;
}

/** The templates staff can create a procedure from (archived ones are left out). */
export function procedureTemplatesQuery() {
  return queryOptions({
    queryKey: ['procedureTemplates'],
    queryFn: () => unwrap(api.GET('/api/procedures/patient-procedure-stage/templates')),
  });
}

/** Creates a procedure from a template, returning it; lists and the patient's procedure count refresh. */
export function useCreateProcedure() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: NewProcedureInput) => unwrap(api.POST('/api/procedures/patient-procedures', {body: input})),
    onSuccess: async (procedure) => {
      queryClient.setQueryData(procedureQuery(procedure.id ?? '').queryKey, procedure);
      await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
      await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
      await queryClient.invalidateQueries({queryKey: procedureKeys.countForPatient(procedure.patientId ?? '')});
    },
  });
}

/** Saves a procedure's details, returning it updated; it replaces the cached one, and lists refresh. */
export function useUpdateProcedure(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: ProcedureDetailsInput) =>
      unwrap(api.PATCH(PROCEDURE, {params: {path: {id}}, body: input})),
    onSuccess: async (procedure) => {
      queryClient.setQueryData(procedureQuery(id).queryKey, procedure);
      await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
      await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
    },
  });
}

/**
 * Adding a note to a procedure, and editing one of your own (from the version you saw: someone, or another tab, saving
 * it first is a 409, whose message says to reload). Each returns the note, which goes into the cached procedure at
 * once (newest first), so it shows straight away wherever the procedure is open; the procedure (its last updated
 * time), lists and timelines then refresh.
 */
export function useProcedureNoteActions(id: string) {
  const show = useShowNote();
  return {
    add: useMutation({
      mutationFn: (text: string) => unwrap(api.POST(`${PROCEDURE}/notes`, {params: {path: {id}}, body: {text}})),
      onSuccess: (note) => show(id, note),
    }),
    edit: useMutation({
      mutationFn: ({noteId, text, expectedVersion}: {noteId: string; text: string; expectedVersion: number}) =>
        unwrap(api.PUT(`${PROCEDURE}/notes/{noteId}`, {params: {path: {id, noteId}}, body: {text, expectedVersion}})),
      onSuccess: (note) => show(id, note),
    }),
  };
}

/**
 * Adding a note to whichever procedure is given (the notepad's Add Note to Procedure, which can choose between two
 * open ones). Like `useProcedureNoteActions`' `add`, the note shows on that procedure's page at once.
 */
export function useAddProcedureNote() {
  const show = useShowNote();
  return useMutation({
    mutationFn: ({procedureId, text}: {procedureId: string; text: string}) =>
      unwrap(api.POST(`${PROCEDURE}/notes`, {params: {path: {id: procedureId}}, body: {text}})),
    onSuccess: (note, {procedureId}) => show(procedureId, note),
  });
}

/** Puts a saved note into its cached procedure (newest first), then refreshes the procedure, lists and timelines. */
function useShowNote() {
  const queryClient = useQueryClient();
  return async (procedureId: string, note: ProcedureNote) => {
    queryClient.setQueryData(procedureQuery(procedureId).queryKey, (procedure) =>
      procedure && {...procedure, noteList: [note, ...(procedure.noteList ?? []).filter((n) => n.id !== note.id)].sort(newestFirst)},
    );
    await queryClient.invalidateQueries({queryKey: procedureQuery(procedureId).queryKey});
    await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
    await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
  };
}

/** Notes newest first, as the API sends them. */
const newestFirst = (a: ProcedureNote, b: ProcedureNote) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? '');

/** Reads the procedure afresh: after a note was changed somewhere else (a 409), to show it as it is now. */
export function useReloadProcedure(id: string) {
  const queryClient = useQueryClient();
  // Read again even where nothing shows it now (a note's conflict can be seen from the notepad, too).
  return () => queryClient.invalidateQueries({queryKey: procedureQuery(id).queryKey, refetchType: 'all'});
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
 * it's taken out of the cached procedure at once; asking returns the note, which then shows "Deletion requested". The
 * procedure, lists and timelines then refresh, and after a failure the procedure is read again, so a 409 (asked or
 * changed meanwhile) shows the note as it is now.
 */
export function useNoteDeletionActions(id: string) {
  const queryClient = useQueryClient();
  const show = useShowNote();
  const reload = useReloadProcedure(id);
  return {
    remove: useMutation({
      mutationFn: ({noteId, reason, detail, expectedVersion}: NoteDeletionReasonInput & {noteId: string; expectedVersion: number}) =>
        unwrap(
          api.POST(`${PROCEDURE}/notes/{noteId}/delete`, {
            params: {path: {id, noteId}},
            body: {reason, detail: detailOrNull(detail), expectedVersion},
          }),
        ),
      onError: () => void reload(),
      onSuccess: async (_, {noteId}) => {
        queryClient.setQueryData(procedureQuery(id).queryKey, (procedure) =>
          procedure && {...procedure, noteList: (procedure.noteList ?? []).filter((n) => n.id !== noteId)},
        );
        await queryClient.invalidateQueries({queryKey: procedureQuery(id).queryKey});
        await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
        await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
      },
    }),
    request: useMutation({
      mutationFn: ({noteId, reason, detail}: NoteDeletionReasonInput & {noteId: string}) =>
        unwrap(
          api.POST(`${PROCEDURE}/notes/{noteId}/deletion-requests`, {
            params: {path: {id, noteId}},
            body: {reason, detail: detailOrNull(detail)},
          }),
        ),
      onSuccess: (note) => show(id, note),
      onError: () => void reload(),
    }),
  };
}

/** Keyed under ['procedures'], so a change to any procedure (a note deleted) reads it afresh. */
export const noteDeletionRequestKey = (id: string) => ['procedures', 'note-deletion-requests', id] as const;

/** A request to delete a note (`GET api/procedures/note-deletion-requests/{id}`, admins). Reading it is recorded as a view. */
export function noteDeletionRequestQuery(id: string) {
  return queryOptions({
    queryKey: noteDeletionRequestKey(id),
    queryFn: () => unwrap(api.GET('/api/procedures/note-deletion-requests/{id}', {params: {path: {id}}})),
  });
}

/** An admin's decision: keep the note, or delete it from the version they read, with a reason. */
export type NoteDeletionDecision = {deleteNote: false} | ({deleteNote: true; expectedVersion: number} & NoteDeletionReasonInput);

/**
 * An admin's decision on a request to delete a note: delete it (from the version they read, with the reason given) or
 * keep it. The request comes back decided; its procedure (the note gone, or no longer waiting), lists and timelines
 * refresh.
 */
export function useDecideNoteDeletion(requestId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (decision: NoteDeletionDecision) =>
      unwrap(
        api.POST('/api/procedures/note-deletion-requests/{id}/decision', {
          params: {path: {id: requestId}},
          body: decision.deleteNote
            ? {deleteNote: true, expectedVersion: decision.expectedVersion, reason: decision.reason, detail: detailOrNull(decision.detail)}
            : {deleteNote: false},
        }),
      ),
    onSuccess: async (decided) => {
      queryClient.setQueryData(noteDeletionRequestKey(requestId), decided);
      await queryClient.invalidateQueries({queryKey: procedureQuery(decided.procedureId ?? '').queryKey});
      await queryClient.invalidateQueries({queryKey: ['procedures', 'search']});
      await queryClient.invalidateQueries({queryKey: procedureKeys.timelines});
    },
  });
}

/** Deletes a procedure (admins, and only once it's cancelled or archived). */
export function useDeleteProcedure(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unwrap(api.DELETE(PROCEDURE, {params: {path: {id}}})),
    onSuccess: async () => {
      // Its own query would only 404 if refetched, so drop it rather than refresh it.
      queryClient.removeQueries({queryKey: ['procedures', id]});
      await queryClient.invalidateQueries({queryKey: ['procedures']});
      await queryClient.invalidateQueries({queryKey: ['patients']});
    },
  });
}

/** Decisions 13 and 20: only an admin can delete a procedure, and only one the API allows (cancelled or archived). */
export const canDeleteProcedure = (procedure: Procedure, isAdmin: boolean) => isAdmin && (procedure.canBeDeleted ?? false);

export const PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
