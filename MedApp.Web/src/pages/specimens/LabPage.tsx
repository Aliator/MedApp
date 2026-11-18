import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Tab, TabList} from '@astryxdesign/core/TabList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState, type FormEvent} from 'react';
import {ApiError, problemMessage} from '../../api/errors';
import {
  laboratoriesQuery,
  SPECIMEN_VIEWS,
  specimenSearchQuery,
  specimenViewLabel,
  useFindSpecimen,
  type SpecimenSummary,
  type SpecimenView,
} from '../../api/specimens';
import {useListSettings} from '../../app/listSettings';
import {useCan} from '../../auth/auth';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {FIELD_STATUS_LINE_HEIGHT, NO_STATUS_LINE} from '../../components/forms/fieldSpace';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {OverdueAtLabToken, SpecimenNumberCell, SpecimenPatientCell, SpecimenStatusToken} from '../../components/specimens/SpecimenCells';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {TextCell} from '../../components/tables/TextCell';
import {formatDateTime} from '../../utils/dates';
import {labViewOf, type LabSearch} from './labSearch';

type SpecimenRow = SpecimenSummary & Record<string, unknown>;

/** The time each view is about, for its column: when it was taken, sent, received, reported or closed, or expected by. */
const VIEW_TIMES: Record<SpecimenView, {header: string; at: (s: SpecimenSummary) => string | null | undefined}> = {
  ToSend: {header: 'Taken', at: (s) => s.takenAtUtc},
  Expected: {header: 'Sent', at: (s) => s.sentAtUtc},
  InLab: {header: 'Received', at: (s) => s.receivedAtUtc},
  Overdue: {header: 'Expected by', at: (s) => s.expectedByUtc},
  RejectedAndLost: {header: 'Closed', at: (s) => s.closedAtUtc},
  Reported: {header: 'Reported', at: (s) => s.reportedAtUtc},
};

/** What each view holds, under the tabs. */
const VIEW_DESCRIPTIONS: Record<SpecimenView, string> = {
  ToSend: 'Taken and not yet sent, oldest first.',
  Expected: 'Sent and not yet received by the lab, oldest first.',
  InLab: 'Received by the lab and not yet reported, oldest first.',
  Overdue: "Not reported by the type's expected days, oldest first.",
  RejectedAndLost: 'Rejected by the lab or lost in the last 30 days, oldest first.',
  Reported: 'Reported in the last 7 days, oldest first.',
};

/** The empty view's words. */
const VIEW_EMPTY: Record<SpecimenView, string> = {
  ToSend: 'Nothing waiting to be sent',
  Expected: 'Nothing on its way to the lab',
  InLab: 'Nothing in the lab',
  Overdue: 'Nothing overdue',
  RejectedAndLost: 'Nothing rejected or lost in the last 30 days',
  Reported: 'Nothing reported in the last 7 days',
};

/** The view's page of specimens, for its lab and page size (the route's loader fetches the same). */
export function labListQuery(view: SpecimenView, search: LabSearch, pageSize: number) {
  return specimenSearchQuery({View: view, LaboratoryId: search.lab, Page: search.page ?? 1, PageSize: pageSize});
}

const tabId = (view: SpecimenView) => `lab-tab-${view}`;
const panelId = 'lab-view-panel';

export interface LabPageProps {
  /** The view, lab and page, from the URL. */
  search: LabSearch;
  /** Updates the URL; the route passes navigate({search}). */
  onSearchChange: (search: LabSearch) => void;
}

/**
 * The lab's worklist (`/lab`; decision 90): find a specimen by its number, typed or scanned from its label by a keyboard
 * scanner (which types it and presses Enter), and the views of where specimens are: To send, Expected (sent, not
 * received), In the lab (received, not reported), Overdue, Rejected and lost (30 days) and Reported (7 days), each
 * oldest first, optionally for one lab. Each row is the number (opening the specimen), the patient with their NHS
 * number, the lab, the status with Overdue at lab, and the view's time. The view, lab and page live in the URL; the
 * number found never does (it goes in the request's body). Every patient shown is recorded as listed.
 */
export function LabPage({search, onSearchChange}: LabPageProps) {
  const canProcess = useCan('ProcessSpecimens');
  const view = labViewOf(search, canProcess);
  const {pageSize} = useListSettings();
  const {data, isPending, isError} = useQuery(labListQuery(view, search, pageSize));
  const labs = useQuery(laboratoriesQuery(true));
  const isNarrow = useIsNarrow();
  const time = VIEW_TIMES[view];
  const total = data?.totalCount ?? 0;
  const labName = labs.data?.find((lab) => lab.id === search.lab)?.name;

  const columns: DataTableColumn<SpecimenRow>[] = [
    {key: 'number', header: 'Specimen', width: proportional(1.5, {minWidth: 120}), renderCell: (s) => <SpecimenNumberCell specimen={s} />},
    {key: 'patient', header: 'Patient', width: proportional(2, {minWidth: 120}), renderCell: (s) => <SpecimenPatientCell specimen={s} />},
    {key: 'lab', header: 'Lab', width: proportional(1.5, {minWidth: 80}), renderCell: (s) => <TextCell text={s.laboratoryName} maxLines={2} />},
    {
      key: 'status',
      header: 'Status',
      width: pixel(156),
      renderCell: (s) => (
        <VStack gap={1} hAlign="start">
          <SpecimenStatusToken status={s.status} />
          {s.isOverdueAtLab && <OverdueAtLabToken />}
        </VStack>
      ),
    },
    {key: 'time', header: time.header, width: pixel(150), renderCell: (s) => <TextCell text={formatDateTime(time.at(s) ?? '')} />},
  ];

  return (
    <Page
      title="Lab"
      subtitle={isPending ? 'Loading specimens...' : `${total} ${total === 1 ? 'specimen' : 'specimens'}${labName ? ` for ${labName}` : ''}`}
      fill
      message={isError && <AlertMessage message="The specimens could not be loaded. Please try again." />}>
      <VStack gap={4} height="100%">
        <HStack gap={4} vAlign="end" wrap="wrap" justify="between">
          <FindSpecimen />
          <AnchoredSelector
            label="Lab"
            options={[{value: '', label: 'All labs'}, ...(labs.data ?? []).map((lab) => ({value: lab.id ?? '', label: lab.name ?? ''}))]}
            value={search.lab ?? ''}
            onChange={(lab: string) => onSearchChange({view: search.view, lab: lab || undefined})}
            width={240}
            containerStyle={NO_STATUS_LINE}
          />
        </HStack>
        <TabList
          value={view}
          onChange={(next) => onSearchChange({view: next as SpecimenView, lab: search.lab})}
          role="tablist"
          hasDivider>
          {SPECIMEN_VIEWS.map((v) => (
            <Tab key={v} id={tabId(v)} value={v} label={specimenViewLabel(v)} panelId={panelId} />
          ))}
        </TabList>
        <Text type="supporting" color="secondary">
          {VIEW_DESCRIPTIONS[view]}
        </Text>
        <StackItem size="fill">
          <VStack id={panelId} role="tabpanel" aria-label={specimenViewLabel(view)} height="100%">
            <DataTable
              data={(data?.items ?? []) as SpecimenRow[]}
              columns={isNarrow ? columns.filter((c) => c.key !== 'lab') : columns}
              idKey="id"
              isLoading={isPending}
              skeletonRows={pageSize}
              pagination={{
                page: search.page ?? 1,
                pageSize,
                totalItems: total,
                onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
              }}
              emptyState={<EmptyState title={VIEW_EMPTY[view]} icon={<Icon icon={AppIcon.Lab} size="lg" />} isCompact />}
            />
          </VStack>
        </StackItem>
      </VStack>
    </Page>
  );
}

/**
 * Find a specimen by its number: typed, or scanned from its label by a keyboard scanner, which types the number and
 * presses Enter. The number goes in the request's body (`POST api/specimens/find`), never the URL; a number nobody has
 * says so on the field. Found, its page opens.
 */
function FindSpecimen() {
  const find = useFindSpecimen();
  const navigate = useNavigate();
  const [number, setNumber] = useState('');
  const [problem, setProblem] = useState<string | null>(null);

  const submit = (event: FormEvent) => {
    event.preventDefault();
    const typed = number.trim();
    if (!typed) return;
    setProblem(null);
    find.mutate(typed, {
      onSuccess: (specimen) => void navigate({to: '/specimens/$id', params: {id: specimen.id ?? ''}}),
      onError: (e) =>
        setProblem(
          e instanceof ApiError && e.status === 404 ? `No specimen has the number ${typed}.` : problemMessage(e, 'The specimen could not be found. Please try again.'),
        ),
    });
  };

  return (
    <form onSubmit={submit} noValidate aria-label="Find a specimen">
      <HStack gap={2} vAlign="end">
        <TextInput
          label="Find by specimen number"
          value={number}
          onChange={(value) => {
            setNumber(value);
            setProblem(null);
          }}
          placeholder="Type or scan, e.g. MA26-000123"
          startIcon={<Icon icon={AppIcon.Scan} />}
          autoComplete="off"
          autoCapitalize="characters"
          hasSpellCheck={false}
          width={280}
          status={problem ? {type: 'error', message: problem} : undefined}
        />
        <StackItem style={FIND_BUTTON}>
          <Button type="submit" label="Find" variant="primary" icon={<Icon icon={AppIcon.Search} />} isLoading={find.isPending} isDisabled={!number.trim()} />
        </StackItem>
      </HStack>
    </form>
  );
}

/** The Find button lines up with the field's input, above the line kept for its error. */
const FIND_BUTTON = {paddingBottom: FIELD_STATUS_LINE_HEIGHT} as const;
