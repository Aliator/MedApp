import {Grid} from '@astryxdesign/core/Grid';
import {Heading} from '@astryxdesign/core/Text';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {useQuery} from '@tanstack/react-query';
import {useRouter} from '@tanstack/react-router';
import {useState} from 'react';
import {type OverdueByAssignee, type OverdueByPriority, type StageTime, type WeeklyThroughput} from '../../api/insights';
import {allTemplatesQuery} from '../../api/pathwaySetup';
import {specialtiesQuery} from '../../api/specialties';
import {BarChart, type ChartSeries} from '../../components/charts/BarChart';
import {ChartCard, type ChartView} from '../../components/charts/ChartCard';
import {ChartTable} from '../../components/charts/ChartTable';
import {SERIES_COLOURS} from '../../components/charts/chartColours';
import {StatTile} from '../../components/charts/StatTile';
import {NO_FIELD_STATUS_LINE} from '../../components/forms/fieldSpace';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {formatShortDate} from '../../utils/dates';
import {DEFAULT_INSIGHTS_WEEKS, INSIGHTS_WEEKS, type InsightsSearch, type InsightsWeeks} from '../listSearch';
import {
  assigneeHref,
  assigneeNames,
  formatHours,
  formatWeek,
  overdueByAssigneeSummary,
  overdueByPrioritySummary,
  priorityHref,
  pathwaysHref,
  stageTimesSummary,
  throughputSummary,
} from './insightsText';
import {insightsPageQuery} from './insightsPageQuery';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';

export interface InsightsPageProps {
  /** The period, specialty and template, from the URL. */
  search: InsightsSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: InsightsSearch) => void;
}

const ALL_TEMPLATES = 'all';
const ALL_SPECIALTIES = 'all';
const DIMMED = {opacity: 0.6};
const LAST_WEEK = 'this time last week';

const COUNT: ChartSeries[] = [{key: 'count', label: 'Overdue', colour: SERIES_COLOURS[0]}];
const THROUGHPUT: ChartSeries[] = [
  {key: 'created', label: 'Created', colour: SERIES_COLOURS[0]},
  {key: 'completed', label: 'Discharged', colour: SERIES_COLOURS[1]},
];
const STAGE_TIME: ChartSeries[] = [
  {key: 'averageWaitingHours', label: 'Waiting to start', colour: SERIES_COLOURS[0]},
  {key: 'averageWorkingHours', label: 'Being worked on', colour: SERIES_COLOURS[1]},
];

type AssigneeRow = OverdueByAssignee & {name: string};
type StageRow = StageTime & {name: string};
type CardName = 'assignee' | 'priority' | 'throughput' | 'stages';

/**
 * Insights (admin): an overview of the pathway work, to keep an eye on it. Headline counts against this time last
 * week (open, in progress, overdue, discharged), where the overdue work is (by assignee and by priority), how many
 * were created and discharged each week, and how long each template's stages take, waiting and being worked on. A
 * period (4, 12 or 26 weeks), a specialty (the pathways' own) and a template filter everything, from the URL.
 *
 * Each chart is in a `ChartCard`, with a summary and a table of its numbers. A tile, or an overdue bar or row, opens
 * the pathways list filtered to what it counts.
 */
export function InsightsPage({search, onSearchChange}: InsightsPageProps) {
  const weeks = search.weeks ?? DEFAULT_INSIGHTS_WEEKS;
  const router = useRouter();
  const isNarrow = useIsNarrow();
  const [views, setViews] = useState<Record<CardName, ChartView>>({assignee: 'chart', priority: 'chart', throughput: 'chart', stages: 'chart'});
  const viewOf = (card: CardName) => ({view: views[card], onViewChange: (view: ChartView) => setViews({...views, [card]: view})});

  const templates = useQuery(allTemplatesQuery);
  const specialties = useQuery(specialtiesQuery(true));
  const {data, isPending, isError, isPlaceholderData} = useQuery(insightsPageQuery(search));
  // The template and specialty the numbers on screen are for: while others load, the last ones' stay, under their names.
  const templateId = data?.templateId?.toLowerCase();
  const specialtyId = data?.specialtyId?.toLowerCase();
  const scope = {templateId, specialtyId};
  const templateNameOf = (id: string | null | undefined) => templates.data?.find((t) => t.id?.toLowerCase() === id?.toLowerCase())?.name;
  const templateName = templateId ? (templateNameOf(templateId) ?? 'one template') : undefined;
  const specialtyName = specialtyId
    ? (specialties.data?.find((s) => s.id?.toLowerCase() === specialtyId)?.name ?? 'one specialty')
    : undefined;
  const open = (href: string) => void router.navigate({href});
  const isInSpecialty = (templateSpecialty: string | undefined, specialty: string | undefined) =>
    !specialty || templateSpecialty?.toLowerCase() === specialty;

  const specialtyOptions = [
    {value: ALL_SPECIALTIES, label: 'All specialties'},
    ...(specialties.data ?? []).map((s) => ({value: s.id?.toLowerCase() ?? '', label: s.isArchived ? `${s.name} (archived)` : (s.name ?? '')})),
  ];
  // With a specialty chosen, only its templates (a pathway's specialty is its template's when it was made).
  const templateOptions = [
    {value: ALL_TEMPLATES, label: 'All templates'},
    ...[...(templates.data ?? [])]
      .filter((t) => isInSpecialty(t.specialtyId, search.specialty) || t.id?.toLowerCase() === search.template)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
      .map((t) => ({value: t.id?.toLowerCase() ?? '', label: t.isArchived ? `${t.name} (archived)` : (t.name ?? '')})),
  ];

  /** A new specialty keeps the template only if it's one of the specialty's. */
  const chooseSpecialty = (value: string) => {
    const specialty = value === ALL_SPECIALTIES ? undefined : value;
    const template = templates.data?.find((t) => t.id?.toLowerCase() === search.template);
    onSearchChange({...search, specialty, template: template && isInSpecialty(template.specialtyId, specialty) ? search.template : undefined});
  };

  const filters = (
    <HStack gap={4} vAlign="end" wrap="wrap" style={NO_FIELD_STATUS_LINE}>
      <AnchoredSelector
        label="Period"
        isLabelHidden
        options={INSIGHTS_WEEKS.map((w) => ({value: String(w), label: `Last ${w} weeks`}))}
        value={String(weeks)}
        onChange={(value: string) =>
          onSearchChange({...search, weeks: Number(value) === DEFAULT_INSIGHTS_WEEKS ? undefined : (Number(value) as InsightsWeeks)})
        }
        variant="secondary"
        width={180}
      />
      <AnchoredSelector
        label="Specialty"
        isLabelHidden
        options={specialtyOptions}
        value={search.specialty ?? ALL_SPECIALTIES}
        onChange={chooseSpecialty}
        variant="secondary"
        width={220}
      />
      <AnchoredSelector
        label="Template"
        isLabelHidden
        options={templateOptions}
        value={search.template ?? ALL_TEMPLATES}
        onChange={(value: string) => onSearchChange({...search, template: value === ALL_TEMPLATES ? undefined : value})}
        variant="secondary"
        width={260}
      />
    </HStack>
  );

  const assigneeRows = data?.overdueByAssignee ?? [];
  const names = assigneeNames(assigneeRows);
  const assignees: AssigneeRow[] = assigneeRows.map((row, i) => ({...row, name: names[i]}));
  const priorities = data?.overdueByPriority ?? [];
  const throughput = data?.throughput ?? [];
  const stageTimes = data?.stageTimes ?? [];

  const subtitle = !data
    ? 'Loading insights...'
    : isPlaceholderData
      ? 'Updating...'
      : `${formatShortDate(data.from ?? '')} to ${formatShortDate(data.to ?? '')}, ${[specialtyName, templateName ?? 'all templates'].filter(Boolean).join(', ')}`;

  return (
    <Page
      title="Insights"
      subtitle={subtitle}
      fill
      isLoading={isPending}
      loadingMessage="Loading insights..."
      message={isError && <AlertMessage message="The insights could not be loaded. Please try again." />}>
      <VStack gap={4} height="100%">
        {filters}
        {data && (
          // While another period or template loads, the numbers on screen are dimmed: they're the last ones.
          <StackItem size="fill" isScrollable aria-label="Insights" aria-busy={isPlaceholderData} style={isPlaceholderData ? DIMMED : undefined}>
            <VStack gap={4}>
              <Grid columns={{minWidth: 180, max: 4}} gap={4}>
                <StatTile
                  label="Open"
                  value={data.open?.current ?? 0}
                  previous={data.open?.lastWeek ?? 0}
                  previousLabel={LAST_WEEK}
                  href={pathwaysHref({}, scope)}
                />
                <StatTile
                  label="In progress"
                  value={data.inProgress?.current ?? 0}
                  previous={data.inProgress?.lastWeek ?? 0}
                  previousLabel={LAST_WEEK}
                  href={pathwaysHref({status: 'InProgress'}, scope)}
                />
                <StatTile
                  label="Overdue"
                  value={data.overdue?.current ?? 0}
                  previous={data.overdue?.lastWeek ?? 0}
                  previousLabel={LAST_WEEK}
                  isRiseGood={false}
                  href={pathwaysHref({overdue: 'true'}, scope)}
                />
                <StatTile
                  label="Discharged this week"
                  value={data.completedThisWeek?.current ?? 0}
                  previous={data.completedThisWeek?.lastWeek ?? 0}
                  previousLabel={LAST_WEEK}
                  isRiseGood
                  // No link: the list can't show what was discharged when (it has no such filter), only what's
                  // discharged now, from any week.
                />
              </Grid>
              <Grid columns={isNarrow ? 1 : 2} gap={4}>
                <ChartCard title="Overdue by assignee" summary={overdueByAssigneeSummary(data)} {...viewOf('assignee')}>
                  {views.assignee === 'chart' ? (
                    assignees.length > 0 && (
                      <BarChart
                        data={assignees}
                        categoryKey="name"
                        series={COUNT}
                        onSelect={(row) => open(assigneeHref(row, scope))}
                        description={`Bar chart. ${overdueByAssigneeSummary(data)}`}
                      />
                    )
                  ) : (
                    <ChartTable
                      data={assignees}
                      categoryKey="name"
                      categoryHeader="Assigned to"
                      series={COUNT}
                      hrefFor={(row) => assigneeHref(row, scope)}
                      label="Overdue pathways by assignee"
                    />
                  )}
                </ChartCard>
                <ChartCard title="Overdue by priority" summary={overdueByPrioritySummary(data)} {...viewOf('priority')}>
                  {views.priority === 'chart' ? (
                    <BarChart
                      data={priorities}
                      categoryKey="priority"
                      series={COUNT}
                      onSelect={(row: OverdueByPriority) => open(priorityHref(row, scope))}
                      description={`Bar chart. ${overdueByPrioritySummary(data)}`}
                    />
                  ) : (
                    <ChartTable
                      data={priorities}
                      categoryKey="priority"
                      categoryHeader="Priority"
                      series={COUNT}
                      hrefFor={(row) => priorityHref(row, scope)}
                      label="Overdue pathways by priority"
                    />
                  )}
                </ChartCard>
                <ChartCard title="Created and discharged" summary={throughputSummary(throughput)} {...viewOf('throughput')}>
                  {views.throughput === 'chart' ? (
                    <BarChart<WeeklyThroughput>
                      data={throughput}
                      categoryKey="weekStart"
                      series={THROUGHPUT}
                      layout="columns"
                      formatCategory={formatWeek}
                      description={`Column chart of pathways created and discharged each week. ${throughputSummary(throughput)}`}
                    />
                  ) : (
                    <ChartTable
                      data={throughput}
                      categoryKey="weekStart"
                      categoryHeader="Week of"
                      series={THROUGHPUT}
                      formatCategory={formatShortDate}
                      label="Pathways created and discharged each week"
                    />
                  )}
                </ChartCard>
                <ChartCard title="Time at each stage" summary={stageTimesSummary(stageTimes)} {...viewOf('stages')}>
                  <VStack gap={6}>
                    {stageTimes.map((t) => {
                      const rows: StageRow[] = (t.stages ?? []).map((s) => ({...s, name: `${s.stageOrder}. ${s.stageName}`}));
                      // Pathways made before templates were required have no template, only the name they were made under.
                      // A template is named as it's called now (pathways keep the name it had when they were made).
                      const name = t.templateId
                        ? (templateNameOf(t.templateId) ?? t.templateName ?? '')
                        : `${t.templateName || 'Pathways'} (no template)`;
                      return (
                        <VStack key={t.templateId ?? 'none'} gap={2}>
                          {/* With every template showing, each has its own chart, under its name. */}
                          {stageTimes.length > 1 && <Heading level={3}>{name}</Heading>}
                          {views.stages === 'chart' ? (
                            <BarChart
                              data={rows}
                              categoryKey="name"
                              series={STAGE_TIME}
                              isStacked
                              formatValue={formatHours}
                              description={`Stacked bar chart of the average time at each stage of ${name}, waiting to start and being worked on.`}
                            />
                          ) : (
                            <ChartTable
                              data={rows}
                              categoryKey="name"
                              categoryHeader="Stage"
                              series={[...STAGE_TIME.map((s) => ({...s, format: formatHours})), {key: 'finishedCount', label: 'Finished'}]}
                              label={`Average time at each stage of ${name}`}
                            />
                          )}
                        </VStack>
                      );
                    })}
                  </VStack>
                </ChartCard>
              </Grid>
            </VStack>
          </StackItem>
        )}
      </VStack>
    </Page>
  );
}
