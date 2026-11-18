import {queryOptions, useMutation, useQueryClient} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';
import {staffQuery, staffWithPermissionQuery} from './calendar';

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
 * stage"). For PathwayAssigned the person is the one assigned, not who assigned them (only admins assign).
 */
export const MONITORING_EVENTS = [
  {value: 'PathwayCreated', label: 'Creates a pathway'},
  {value: 'StageFinished', label: 'Finishes a stage', hasStage: true},
  {value: 'PathwayCompleted', label: 'Discharges a pathway'},
  {value: 'PathwayBlocked', label: 'Puts a pathway on hold'},
  {value: 'PathwayCancelled', label: 'Cancels a pathway'},
  {value: 'PathwayAssigned', label: 'Is assigned to a pathway'},
  {value: 'StageUnlocked', label: 'Unlocks a stage', hasStage: true},
  {value: 'DueDateChanged', label: "Changes a pathway's target date"},
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

/**
 * Everyone a rule can name: the staff list (everyone with a role), which holders of ManageMonitoring can read without
 * ManageUsers.
 */
export const monitoringPeopleQuery = staffQuery;

/** Who a "Stage unlocked" rule can name: holders of OverrideStages, as only they unlock stages (the API refuses anyone else). */
export const monitoringUnlockersQuery = staffWithPermissionQuery('OverrideStages');

/** Adds a rule (no `id`) or replaces one, switching it on or off too. */
export function useSaveMonitoringRule() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({id, ...body}: MonitoringRuleInput & {id?: string}) =>
      id ? unwrap(api.PUT(RULE, {params: {path: {id}}, body})) : unwrap(api.POST(RULES, {body})),
    onSuccess: () => queryClient.invalidateQueries({queryKey: monitoringKey}),
  });
}

/**
 * Switches a rule on or off, and nothing else (`PUT …/{id}/enabled`), so a rule naming something since deleted can still
 * be switched. Call it with `mutateAsync` and handle each call's outcome there: TanStack Query runs a call's own
 * callbacks only for the latest call, and two rules can be switched at once.
 */
export function useSetMonitoringRuleEnabled() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({id, isEnabled}: {id: string; isEnabled: boolean}) =>
      unwrap(api.PUT('/api/monitoring/rules/{id}/enabled', {params: {path: {id}}, body: {isEnabled}})),
    onSettled: () => queryClient.invalidateQueries({queryKey: monitoringKey}),
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
    event: (monitoringEventOf(rule.event)?.value ?? 'PathwayCreated') as MonitoringEvent,
    minimumPriority: rule.minimumPriority ?? null,
    templateId: rule.templateId ?? null,
    stageDefinitionId: rule.stageDefinitionId ?? null,
    isEnabled: rule.isEnabled ?? true,
  };
}
