import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import {resultActionLabel, resultSearchQuery, resultViewLabel, type ResultSummary} from '../../api/results';
import {teamsQuery} from '../../api/teams';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {ageWords, NeedsClinicianToken, ResultPatientCell, ResultStatusToken, ResultTitleCell} from '../../components/results/ResultCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar, type ListFilterFavourite, type ListFilterField, type ListFilterValues} from '../../components/tables/ListFilterBar';
import {TextCell} from '../../components/tables/TextCell';
import {useClinicPicker} from '../../components/teams/ClinicPicker';
import {formatDateTime} from '../../utils/dates';
import {resultsSortOf, resultsViewOf, resultViewsFor, type ResultSortColumn, type ResultsSearch, type ResultsView} from './resultsSearch';

type ResultRow = ResultSummary & Record<string, unknown>;

/** What each view holds, in its empty list. Ordered by dates only: MedApp never reads a report. */
const VIEW_DESCRIPTIONS: Record<ResultsView, string> = {
  AwaitingMine: 'Results you are responsible for that await your sign-off, oldest report first.',
  Awaiting: "Everyone's results awaiting sign-off, oldest report first.",
  NeedsClinician: 'Awaiting sign-off, and their responsible clinician can no longer sign results off, oldest report first.',
  SignedOff: 'Signed off, newest first.',
  All: 'Every result, newest report first.',
};

/** The empty view's words. */
const VIEW_EMPTY: Record<ResultsView, string> = {
  AwaitingMine: 'Nothing awaiting your sign-off',
  Awaiting: 'Nothing awaiting sign-off',
  NeedsClinician: 'Every result awaiting sign-off has a clinician',
  SignedOff: 'Nothing signed off',
  All: 'No results yet',
};

const viewLabel = (view: ResultsView) => (view === 'All' ? 'All results' : resultViewLabel(view));

/**
 * The view's page of results, for the clinic picker's team (else a linked team), patient, sort and page size (the
 * route's loader fetches the same).
 */
export function resultsListQuery(view: ResultsView, search: ResultsSearch, pageSize: number, teamId?: string) {
  return resultSearchQuery({
    View: view,
    TeamId: teamId ?? search.team,
    Search: search.patient,
    Sort: search.sort,
    Ascending: search.sort ? (search.asc ?? true) : undefined,
    Page: search.page ?? 1,
    PageSize: pageSize,
  });
}

export interface ResultsPageProps {
  /** The view, team, patient, page and sort, from the URL. */
  search: ResultsSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: ResultsSearch) => void;
}

/**
 * Results awaiting sign-off and signed off (`/results`; decisions 91 to 97). It opens on its tabs: **Awaiting my
 * sign-off** (holders of SignOffResults, where they start), **Awaiting sign-off (everyone)** (where everyone else
 * starts), **Needs a clinician** (awaiting, and the responsible clinician can no longer sign off) and **Signed off**;
 * the person can change the tabs, and search by the patient's name (`ListFilterBar`; removing the view shows every
 * result). The clinic picker beside the title (admins, and staff in more than one team) narrows it to one team. Each row is the patient (name, NHS
 * number, date of birth), the result (opening its page), its pathway, when it was reported and how many days ago, the
 * responsible clinician, the status and, once signed off, who, when and what they did. **No interpretation:** each
 * view's own order is by dates, a column sorts only when the person asks, and nothing is highlighted by what a report
 * says. The view, team, patient, page and sort live in the URL. Every patient shown is recorded as listed.
 */
export function ResultsPage({search, onSearchChange}: ResultsPageProps) {
  const canSignOff = useCan('SignOffResults');
  const view = resultsViewOf(search, canSignOff);
  const views = useMemo(() => resultViewsFor(canSignOff), [canSignOff]);
  const {pageSize} = useListSettings();
  // The clinic picker; choosing one replaces a team the link narrowed it to.
  const clinic = useClinicPicker('results', search.clinic, (next) => onSearchChange({...search, clinic: next, team: undefined, page: undefined}));
  const {data, isPending, isError} = useQuery(resultsListQuery(view, search, pageSize, clinic.teamId));
  const teams = useQuery({...teamsQuery(true), enabled: !clinic.teamId && search.team !== undefined});
  const isNarrow = useIsNarrow();
  const {sort, asc} = resultsSortOf(search, view);
  const total = data?.totalCount ?? 0;
  const teamName = clinic.teamName ?? teams.data?.find((s) => s.id?.toLowerCase() === search.team)?.name;
  const fields = useMemo<ListFilterField[]>(
    () => [
      {kind: 'text', key: 'patient', label: 'Patient'},
      {kind: 'choice', key: 'view', label: 'Showing', options: views},
    ],
    [views],
  );
  const favourites = useMemo<ListFilterFavourite[]>(() => views.map((v) => ({label: v.label, values: {view: v.value}})), [views]);

  const barValues: ListFilterValues = {patient: search.patient, view: view === 'All' ? undefined : view};
  const fromBar = (values: ListFilterValues) =>
    onSearchChange({
      view: typeof values.view === 'string' ? (values.view as ResultsView) : 'All',
      team: search.team,
      clinic: search.clinic,
      patient: typeof values.patient === 'string' ? values.patient : undefined,
      sort: search.sort,
      asc: search.asc,
    });

  const columns: DataTableColumn<ResultRow>[] = [
    {key: 'patient', header: 'Patient', sortable: true, width: proportional(2, {minWidth: 140}), renderCell: (r) => <ResultPatientCell result={r} />},
    {key: 'title', header: 'Result', sortable: true, width: proportional(2, {minWidth: 140}), renderCell: (r) => <ResultTitleCell result={r} />},
    {
      key: 'pathway',
      header: 'Pathway',
      sortable: true,
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
      sortable: true,
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
      sortable: true,
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

  const noun = total === 1 ? 'result' : 'results';
  return (
    <Page
      title="Results"
      subtitle={isPending ? 'Loading results...' : `${viewLabel(view)}: ${total} ${noun}${teamName ? ` in ${teamName}` : ''}`}
      fill
      message={isError && <AlertMessage label="Results not loaded" message="The results could not be loaded. Please try again." />}
      actions={clinic.picker}>
      <DataTable
        data={(data?.items ?? []) as ResultRow[]}
        columns={isNarrow ? columns.filter((c) => c.key !== 'pathway') : columns}
        idKey="id"
        isLoading={isPending}
        skeletonRows={pageSize}
        search={
          <ListFilterBar
            fields={fields}
            values={barValues}
            onChange={fromBar}
            list="results"
            isUnfiltered={!search.patient && search.view === undefined}
            label="Search results"
            placeholder="Search results by the patient's name, or filter by view..."
            textKey="patient"
            favourites={favourites}
          />
        }
        sort={{column: sort, ascending: asc}}
        onSortChange={(next) => onSearchChange({...search, page: undefined, sort: next.column as ResultSortColumn, asc: next.ascending})}
        pagination={{
          page: search.page ?? 1,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          search.patient ? (
            <EmptyState
              title="No results match your search"
              icon={<Icon icon={AppIcon.Search} size="lg" />}
              actions={<Button label="Clear Search" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, patient: undefined})} />}
              isCompact
            />
          ) : (
            <EmptyState title={VIEW_EMPTY[view]} description={VIEW_DESCRIPTIONS[view]} icon={<Icon icon={AppIcon.Results} size="lg" />} isCompact />
          )
        }
      />
    </Page>
  );
}
