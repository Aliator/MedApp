/**
 * Insights' numbers in words: durations, weeks, the charts' summaries, and the links from a count to the pathways
 * list it counts (its URL filters: `overdue`, `priority`, `assignee`, `status` and `template`).
 */

import type {OverdueByAssignee, OverdueByPriority, PathwayInsights, TemplateStageTimes, WeeklyThroughput} from '../../api/insights';
import {NOBODY} from '../listSearch';

/** A duration given in hours, as people say it: "45 min", "5 h 30 min", "3 d 4 h". */
export function formatHours(hours: number): string {
  const minutes = Math.round(hours * 60);
  if (minutes < 60) return `${minutes} min`;
  if (minutes < 48 * 60) {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return m ? `${h} h ${m} min` : `${h} h`;
  }
  const totalHours = Math.round(minutes / 60);
  const d = Math.floor(totalHours / 24);
  const h = totalHours % 24;
  return h ? `${d} d ${h} h` : `${d} d`;
}

/** A week by its Monday (YYYY-MM-DD), short, for an axis: "8 Jun". */
export function formatWeek(monday: string): string {
  const [year, month, day] = monday.split('-').map(Number);
  if (!year || !month || !day) return monday;
  return new Intl.DateTimeFormat('en-GB', {day: 'numeric', month: 'short', timeZone: 'UTC'}).format(new Date(Date.UTC(year, month - 1, day)));
}

const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

/** An assignee's name: the account's, "Nobody" for the unassigned, or "Deleted user" for an account that's gone. */
export const assigneeName = (row: OverdueByAssignee) => (row.userId ? (row.username ?? 'Deleted user') : 'Nobody');

/**
 * The assignees' names, in order, for a chart's bars: as `assigneeName`, but with deleted accounts numbered ("Deleted
 * user 1", "Deleted user 2"...) when there's more than one, so each bar has a name of its own.
 */
export function assigneeNames(rows: readonly OverdueByAssignee[]): string[] {
  const deleted = rows.filter((r) => r.userId && !r.username).length;
  let n = 0;
  return rows.map((row) => (row.userId && !row.username && deleted > 1 ? `Deleted user ${++n}` : assigneeName(row)));
}


/**
 * The pathways list, filtered to what a count counts: on the template (by its ID) when Insights is on one. Always
 * `mine=false`, so the list isn't narrowed to the admin's own pathways by where Account Settings has lists start.
 */
export function pathwaysHref(filters: Record<string, string>, templateId?: string | null): string {
  return `/pathways?${new URLSearchParams({...filters, mine: 'false', ...(templateId ? {templateId} : {})}).toString()}`;
}

export const assigneeHref = (row: OverdueByAssignee, templateId?: string | null) =>
  pathwaysHref({overdue: 'true', assignee: row.userId ?? NOBODY}, templateId);

export const priorityHref = (row: OverdueByPriority, templateId?: string | null) =>
  pathwaysHref({overdue: 'true', priority: row.priority ?? ''}, templateId);

export function overdueByAssigneeSummary(insights: PathwayInsights): string {
  const overdue = insights.overdue?.current ?? 0;
  if (overdue === 0) return 'Nothing is overdue.';
  const rows = insights.overdueByAssignee ?? [];
  const nobody = rows.find((r) => !r.userId)?.count ?? 0;
  const top = rows.find((r) => r.userId);
  const parts = [`${plural(overdue, 'pathway is', 'pathways are')} overdue.`];
  if (top) parts.push(`${assigneeName(top)} has the most (${top.count}).`);
  if (nobody) parts.push(`${plural(nobody, 'has', 'have')} nobody assigned.`);
  return parts.join(' ');
}

export function overdueByPrioritySummary(insights: PathwayInsights): string {
  const overdue = insights.overdue?.current ?? 0;
  if (overdue === 0) return 'Nothing is overdue.';
  const count = (priority: string) => insights.overdueByPriority?.find((r) => r.priority === priority)?.count ?? 0;
  return `Of ${overdue} overdue, ${count('Urgent')} ${count('Urgent') === 1 ? 'is' : 'are'} urgent and ${count('High')} high priority.`;
}

export function throughputSummary(weeks: readonly WeeklyThroughput[]): string {
  const created = weeks.reduce((sum, w) => sum + (w.created ?? 0), 0);
  const completed = weeks.reduce((sum, w) => sum + (w.completed ?? 0), 0);
  const thisWeek = weeks[weeks.length - 1];
  const balance =
    created === completed
      ? 'as many discharged as created'
      : created > completed
        ? `${created - completed} more created than discharged`
        : `${completed - created} more discharged than created`;
  const parts = [
    `Over ${plural(weeks.length, 'week')}, ${plural(created, 'pathway')} created and ${completed} discharged: ${balance}.`,
  ];
  if (thisWeek) parts.push(`This week so far: ${thisWeek.created ?? 0} created, ${thisWeek.completed ?? 0} discharged.`);
  return parts.join(' ');
}

/** The stage with the longest average time (waiting and working), across the templates. */
export function stageTimesSummary(templates: readonly TemplateStageTimes[]): string {
  const stages = templates.flatMap((t) =>
    (t.stages ?? []).map((s) => ({template: t.templateName ?? '', stage: s, total: (s.averageWaitingHours ?? 0) + (s.averageWorkingHours ?? 0)})),
  );
  if (stages.length === 0) return 'No stages were finished in this period.';
  const longest = stages.reduce((a, b) => (b.total > a.total ? b : a));
  const where = templates.length > 1 ? ` of ${longest.template}` : '';
  return (
    `The longest is ${longest.stage.stageName}${where}: ${formatHours(longest.total)} on average, ` +
    `${formatHours(longest.stage.averageWaitingHours ?? 0)} of it waiting to be started.`
  );
}
