import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Tab, TabList} from '@astryxdesign/core/TabList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {resultActionLabel, resultSearchQuery, resultViewLabel, type ResultSummary, type ResultView} from '../../api/results';
import {specialtiesQuery} from '../../api/specialties';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {ageWords, NeedsClinicianToken, ResultPatientCell, ResultStatusToken, ResultTitleCell} from '../../components/results/ResultCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {resultsViewOf, resultViewsFor, type ResultsSearch} from './resultsSearch';

type ResultRow = ResultSummary & Record<string, unknown>;

/** What each view holds, under the tabs. Ordered by dates only: MedApp never reads a report. */
const VIEW_DESCRIPTIONS: Record<ResultView, string> = {
  AwaitingMine: 'Results you are responsible for that await your sign-off, oldest report first.',
  Awaiting: "Everyone's results awaiting sign-off, oldest report first.",
  NeedsClinician: 'Awaiting sign-off, and their responsible clinician can no longer sign results off, oldest report first.',
  SignedOff: 'Signed off, newest first.',
};

/** The empty view's words. */
const VIEW_EMPTY: Record<ResultView, string> = {
  AwaitingMine: 'Nothing awaiting your sign-off',
  Awaiting: 'Nothing awaiting sign-off',
  NeedsClinician: 'Every result awaiting sign-off has a clinician',
  SignedOff: 'Nothing signed off',
};

/** The view's page of results, for its specialty and page size (the route's loader fetches the same). */
export function resultsListQuery(view: ResultView, search: ResultsSearch, pageSize: number) {
  return resultSearchQuery({View: view, SpecialtyId: search.specialty, Page: search.page ?? 1, PageSize: pageSize});
}

const tabId = (view: ResultView) => `results-tab-${view}`;
const panelId = 'results-view-panel';

export interface ResultsPageProps {
  /** The view, specialty and page, from the URL. */
  search: ResultsSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: ResultsSearch) => void;
}

/**
 * Results awaiting sign-off and signed off (`/results`; decisions 91 to 97), as tabs: **Awaiting my sign-off** (holders
 * of SignOffResults, where they start), **Awaiting sign-off (everyone)** (where everyone else starts), **Needs a
 * clinician** (awaiting, and the responsible clinician can no longer sign off) and **Signed off**, optionally for one
 * specialty. Each row is the patient (name, NHS number, date of birth), the result (opening its page), its pathway,
 * when it was reported and how many days ago, the responsible clinician, the status and, once signed off, who, when
 * and what they did. **No interpretation:** rows are ordered by dates only and nothing is highlighted by what a report
 * says. The view, specialty and page live in the URL. Every patient shown is recorded as listed.
 */
export function ResultsPage({search, onSearchChange}: ResultsPageProps) {
  const canSignOff = useCan('SignOffResults');
  const view = resultsViewOf(search, canSignOff);
  const views = resultViewsFor(canSignOff);
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(resultsListQuery(view, search, pageSize));
  const specialties = useQuery(specialtiesQuery(true));
  const isNarrow = useIsNarrow();
  const total = data?.totalCount ?? 0;
  const specialtyName = specialties.data?.find((s) => s.id === search.specialty)?.name;
  const specialtyOptions = [
    {value: '', label: 'All specialties'},
    ...(specialties.data ?? []).filter((s) => !s.isArchived || s.id === search.specialty).map((s) => ({value: s.id ?? '', label: s.name ?? ''})),
  ];

  const columns: DataTableColumn<ResultRow>[] = [
    {key: 'patient', header: 'Patient', width: proportional(2, {minWidth: 140}), renderCell: (r) => <ResultPatientCell result={r} />},
    {key: 'title', header: 'Result', width: proportional(2, {minWidth: 140}), renderCell: (r) => <ResultTitleCell result={r} />},
    {
      key: 'pathway',
      header: 'Pathway',
      width: proportional(1.5, {minWidth: 100}),
      renderCell: (r) =>
        r.pathwayId ? (
          <Text type="body" maxLines={2}>
            <Link href={`/pathways/${r.pathwayId}`}>{r.pathwayTitle}</Link>
          </Text>
        ) : (
          <TextCell text="—" />
        ),
    },
    {
      key: 'reported',
      header: 'Reported',
      width: pixel(150),
      renderCell: (r) => (
        <VStack gap={0} hAlign="start">
          <Text type="body">{formatDateTime(r.reportedAtUtc ?? '')}</Text>
          <Text type="supporting" color="secondary">
            {ageWords(r.ageDays)}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'responsible',
      header: 'Responsible clinician',
      width: proportional(1.2, {minWidth: 120}),
      renderCell: (r) => (
        <VStack gap={1} hAlign="start">
          <TextCell text={r.responsibleUsername} />
          {r.needsClinician && <NeedsClinicianToken />}
        </VStack>
      ),
    },
    {
      key: 'status',
      header: view === 'SignedOff' ? 'Signed off' : 'Status',
      width: view === 'SignedOff' ? proportional(1.5, {minWidth: 140}) : pixel(140),
      renderCell: (r) =>
        view === 'SignedOff' ? (
          <VStack gap={0} hAlign="start">
            <Text type="body" maxLines={2}>
              {resultActionLabel(r.action)}
            </Text>
            <Text type="supporting" color="secondary" maxLines={2}>
              {`${r.signedOffByUsername ?? ''}, ${formatDateTime(r.signedOffAtUtc ?? '')}`}
            </Text>
          </VStack>
        ) : (
          <ResultStatusToken status={r.status} />
        ),
    },
  ];

  return (
    <Page
      title="Results"
      subtitle={isPending ? 'Loading results...' : `${total} ${total === 1 ? 'result' : 'results'}${specialtyName ? ` in ${specialtyName}` : ''}`}
      fill
      message={isError && <AlertMessage message="The results could not be loaded. Please try again." />}>
      <VStack gap={4} height="100%">
        <HStack gap={4} vAlign="end" wrap="wrap" justify="end">
          <AnchoredSelector
            label="Specialty"
            options={specialtyOptions}
            value={search.specialty ?? ''}
            onChange={(specialty: string) => onSearchChange({view: search.view, specialty: specialty || undefined})}
            width={240}
            containerStyle={NO_STATUS_LINE}
          />
        </HStack>
        <TabList
          value={view}
          onChange={(next) => onSearchChange({view: next as ResultView, specialty: search.specialty})}
          role="tablist"
          hasDivider>
          {views.map((v) => (
            <Tab key={v.value} id={tabId(v.value)} value={v.value} label={v.label} panelId={panelId} />
          ))}
        </TabList>
        <Text type="supporting" color="secondary">
          {VIEW_DESCRIPTIONS[view]}
        </Text>
        <StackItem size="fill">
          <VStack id={panelId} role="tabpanel" aria-label={resultViewLabel(view)} height="100%">
            <DataTable
              data={(data?.items ?? []) as ResultRow[]}
              columns={isNarrow ? columns.filter((c) => c.key !== 'pathway') : columns}
              idKey="id"
              isLoading={isPending}
              skeletonRows={pageSize}
              pagination={{
                page: search.page ?? 1,
                pageSize,
                totalItems: total,
                onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
              }}
              emptyState={<EmptyState title={VIEW_EMPTY[view]} icon={<Icon icon={AppIcon.Results} size="lg" />} isCompact />}
            />
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}
