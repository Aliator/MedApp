import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {userSearchQuery} from './search';

/** One of the signed-in admin's monitoring rules, with the names of what it names (null for "any", or deleted). */
export type MonitoringRule = Schemas['MonitoringRuleResponse'];

/** A rule's fields, as the add and edit requests take them: null for "any". */
export interface MonitoringRuleInput {
  subjectUserId: string | null;
  event: MonitoringEvent;
  minimumPriority: string | null;
  templateId: string | null;
  stageDefinitionId: string | null;
  isEnabled: boolean;
}

/**
 * What a rule can watch for, in the order the page offers them, as the person's choice reads ("Sam … finishes a
 * stage"). For ProcedureAssigned the person is the one assigned, not who assigned them (only admins assign).
 */
export const MONITORING_EVENTS = [
  {value: 'ProcedureCreated', label: 'Creates a procedure'},
  {value: 'StageFinished', label: 'Finishes a stage', hasStage: true},
  {value: 'ProcedureCompleted', label: 'Completes a procedure'},
  {value: 'ProcedureBlocked', label: 'Blocks a procedure'},
  {value: 'ProcedureCancelled', label: 'Cancels a procedure'},
  {value: 'ProcedureAssigned', label: 'Is assigned to a procedure'},
  {value: 'StageUnlocked', label: 'Unlocks a stage (admins only)', hasStage: true},
  {value: 'DueDateChanged', label: "Changes a procedure's due date"},
] as const;

export type MonitoringEvent = (typeof MONITORING_EVENTS)[number]['value'];

export const monitoringEventOf = (value: string | null | undefined) => MONITORING_EVENTS.find((e) => e.value === value);

/** Whether a rule for this event can name a stage (the API refuses one for any other). */
export const hasStage = (event: string | null | undefined) => MONITORING_EVENTS.some((e) => e.value === event && 'hasStage' in e);

const RULES = '/api/monitoring/rules';
const RULE = '/api/monitoring/rules/{id}';

export const monitoringKey = ['monitoringRules'] as const;

/** The signed-in admin's rules, oldest first (admins see only their own). */
export const monitoringRulesQuery = queryOptions({
  queryKey: monitoringKey,
  queryFn: () => unwrap(api.GET(RULES)),
});

/** Everyone a rule can name: the first 100 accounts by username (the API's largest page). */
export const monitoringPeopleQuery = userSearchQuery({PageSize: 100, Sort: 'username', Ascending: true});

/** Adds a rule (no `id`) or replaces one, switching it on or off too. */
export function useSaveMonitoringRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({id, ...body}: MonitoringRuleInput & {id?: string}) =>
      id ? unwrap(api.PUT(RULE, {params: {path: {id}}, body})) : unwrap(api.POST(RULES, {body})),
    onSuccess: () => queryClient.invalidateQueries({queryKey: monitoringKey}),
  });
}

export function useDeleteMonitoringRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => unwrap(api.DELETE(RULE, {params: {path: {id}}})),
    onSuccess: () => queryClient.invalidateQueries({queryKey: monitoringKey}),
  });
}

/** A rule as the requests take it, from what the API returned. */
export function inputOf(rule: MonitoringRule): MonitoringRuleInput {
  return {
    subjectUserId: rule.subjectUserId ?? null,
    event: (monitoringEventOf(rule.event)?.value ?? 'ProcedureCreated') as MonitoringEvent,
    minimumPriority: rule.minimumPriority ?? null,
    templateId: rule.templateId ?? null,
    stageDefinitionId: rule.stageDefinitionId ?? null,
    isEnabled: rule.isEnabled ?? true,
  };
}
