import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type ProcedureInsights = Schemas['ProcedureInsightsResponse'];
export type OverdueByAssignee = Schemas['OverdueByAssigneeResponse'];
export type OverdueByPriority = Schemas['OverdueByPriorityResponse'];
export type WeeklyThroughput = Schemas['WeeklyThroughputResponse'];
export type TemplateStageTimes = Schemas['TemplateStageTimesResponse'];
export type StageTime = Schemas['StageTimeResponse'];

/**
 * The admins' overview of procedure work (`GET api/procedures/insights`): the last `weeks` UK weeks, every template
 * or one. Keyed under ['procedures'], so a change to any procedure refreshes it; while a new period or template loads,
 * the last one stays on screen.
 */
export function insightsQuery({weeks, templateId}: {weeks: number; templateId?: string}) {
  const query = {Weeks: weeks, ...(templateId ? {TemplateId: templateId} : {})};
  return queryOptions({
    queryKey: ['procedures', 'insights', query],
    queryFn: () => unwrap(api.GET('/api/procedures/insights', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}
