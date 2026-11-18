import {keepPreviousData, queryOptions} from '@tanstack/react-query';
import {api, unwrap, type Schemas} from './client';

export type PathwayInsights = Schemas['PathwayInsightsResponse'];
export type OverdueByAssignee = Schemas['OverdueByAssigneeResponse'];
export type OverdueByPriority = Schemas['OverdueByPriorityResponse'];
export type WeeklyThroughput = Schemas['WeeklyThroughputResponse'];
export type TemplateStageTimes = Schemas['TemplateStageTimesResponse'];
export type StageTime = Schemas['StageTimeResponse'];

/**
 * The admins' overview of pathway work (`GET api/pathways/insights`): the last `weeks` UK weeks, every template
 * or one, every specialty or one. Keyed under ['pathways'], so a change to any pathway refreshes it; while a new
 * period, template or specialty loads, the last one stays on screen.
 */
export function insightsQuery({weeks, templateId, specialtyId}: {weeks: number; templateId?: string; specialtyId?: string}) {
  const query = {Weeks: weeks, ...(templateId ? {TemplateId: templateId} : {}), ...(specialtyId ? {SpecialtyId: specialtyId} : {})};
  return queryOptions({
    queryKey: ['pathways', 'insights', query],
    queryFn: () => unwrap(api.GET('/api/pathways/insights', {params: {query}})),
    placeholderData: keepPreviousData,
  });
}
