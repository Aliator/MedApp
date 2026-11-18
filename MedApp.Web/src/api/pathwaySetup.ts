import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

/**
 * What admins set pathways up with: stage definitions, and templates built from them (decision 17). Staff can
 * read both (New pathway lists the templates); only admins can change them.
 */
export type StageDefinition = Schemas['PatientPathwayStageDefinitionResponse'];
export type TemplateStage = Schemas['PathwayTemplateStageResponse'];

/** Whether a stage involves a visit (an appointment), as the API names it. */
export const VISIT_REQUIREMENTS = ['None', 'Suggested', 'Required'] as const;
export type VisitRequirement = (typeof VISIT_REQUIREMENTS)[number];

/**
 * A stage definition's fields: its name, the description staff see under it, how to carry it out, whether it involves
 * a visit (copied onto each pathway's stage when the pathway is created), and the SNOMED CT concept it records (a
 * code and its display term, both or neither). Both are always sent, "" when there's none, so clearing the code
 * removes the concept on a save (the API reads a missing code as "leave it as it is").
 */
export interface StageDefinitionInput {
  name: string;
  description: string;
  instructions: string;
  visitRequirement: VisitRequirement;
  snomedCode: string;
  snomedTerm: string;
}

/** A template's fields: its name, and its stages in order (a definition may appear more than once). */
export interface TemplateInput {
  name: string;
  stageDefinitionIdsInOrder: string[];
}

const DEFINITIONS = '/api/pathways/patient-pathway-stage/definitions';
const DEFINITION = '/api/pathways/patient-pathway-stage/definitions/{id}';
const TEMPLATES = '/api/pathways/patient-pathway-stage/templates';
const TEMPLATE = '/api/pathways/patient-pathway-stage/templates/{id}';

/**
 * Definitions and templates refresh together: a template shows its definitions' names, and a definition counts the
 * templates that use it.
 */
export const setupKeys = {
  definitions: ['stageDefinitions'] as const,
  templates: ['pathwayTemplates'] as const,
};

/** Every stage definition, archived ones included (they carry `isArchived`). */
export const stageDefinitionsQuery = queryOptions({
  queryKey: [...setupKeys.definitions, 'all'],
  queryFn: () => unwrap(api.GET(DEFINITIONS, {params: {query: {includeArchived: true}}})),
});

/** Every template, archived ones included (they carry `isArchived`). */
export const allTemplatesQuery = queryOptions({
  queryKey: [...setupKeys.templates, 'all'],
  queryFn: () => unwrap(api.GET(TEMPLATES, {params: {query: {includeArchived: true}}})),
});

/** One template, with its stages in order. A missing one is an ApiError with status 404. */
export function templateQuery(id: string) {
  return queryOptions({
    queryKey: [...setupKeys.templates, id],
    queryFn: () => unwrap(api.GET(TEMPLATE, {params: {path: {id}}})),
  });
}

function useRefreshSetup() {
  const queryClient = useQueryClient();
  return async () => {
    await queryClient.invalidateQueries({queryKey: setupKeys.definitions});
    await queryClient.invalidateQueries({queryKey: setupKeys.templates});
  };
}

/** Creates a stage definition (no `id`) or saves one. */
export function useSaveStageDefinition() {
  const onSuccess = useRefreshSetup();
  return useMutation({
    mutationFn: ({id, ...body}: StageDefinitionInput & {id?: string}) =>
      id ? unwrap(api.PATCH(DEFINITION, {params: {path: {id}}, body})) : unwrap(api.POST(DEFINITIONS, {body})),
    onSuccess,
  });
}

/**
 * Archive, restore and delete for stage definitions. An archived definition can't be added to templates, but the
 * templates and pathways that have it keep it. Only one no template uses can be deleted (decision 11).
 */
export function useStageDefinitionActions() {
  const onSuccess = useRefreshSetup();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST(`${DEFINITION}/archive`, path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST(`${DEFINITION}/restore`, path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE(DEFINITION, path(id))), onSuccess}),
  };
}

/** Creates a template (no `id`) or saves one, returning it. */
export function useSaveTemplate() {
  const onSuccess = useRefreshSetup();
  return useMutation({
    mutationFn: ({id, ...body}: TemplateInput & {id?: string}) =>
      id ? unwrap(api.PATCH(TEMPLATE, {params: {path: {id}}, body})) : unwrap(api.POST(TEMPLATES, {body})),
    onSuccess,
  });
}

/**
 * Archive, restore and delete for templates. An archived template isn't offered by New pathway, but the
 * pathways made from it keep their stages. Only one never used can be deleted (decision 17).
 */
export function useTemplateActions() {
  const onSuccess = useRefreshSetup();
  const path = (id: string) => ({params: {path: {id}}});
  return {
    archive: useMutation({mutationFn: (id: string) => unwrap(api.POST(`${TEMPLATE}/archive`, path(id))), onSuccess}),
    restore: useMutation({mutationFn: (id: string) => unwrap(api.POST(`${TEMPLATE}/restore`, path(id))), onSuccess}),
    remove: useMutation({mutationFn: (id: string) => unwrap(api.DELETE(TEMPLATE, path(id))), onSuccess}),
  };
}
