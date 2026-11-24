import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {Switch} from '@astryxdesign/core/Switch';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {retentionReviewQuery, type RetentionReviewItem} from '../../api/patients';
import {useListSettings} from '../../app/listSettings';
import {Page} from '../../components/pages/Page';
import {AnonymiseDialog} from '../../components/patients/AnonymiseDialog';
import {KeepRecordDialog} from '../../components/patients/KeepRecordDialog';
import {ActionsMenu} from '../../components/shared/ActionsMenu';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {useIsCompactRow} from '../../components/tables/RowCards';
import {TextCell} from '../../components/tables/TextCell';
import {formatDate} from '../../utils/dates';
import type {RetentionListSearch} from '../listSearch';

type ItemRow = RetentionReviewItem & Record<string, unknown>;

export interface RetentionReviewPageProps {
  search: RetentionListSearch;
  onSearchChange: (search: RetentionListSearch) => void;
}

/** The list's page, for its search and page size (the route's loader fetches it too). */
export function retentionListQuery(search: RetentionListSearch, pageSize: number) {
  return retentionReviewQuery({Page: search.page ?? 1, PageSize: pageSize, IncludeKept: search.kept ? true : undefined});
}

/** What set each record's period: the adult or child rule, or its pathways' longer team class. */
const CLASS_LABELS: Record<string, string> = {
  Adult: 'Adult',
  Child: 'Child',
  Oncology: 'Oncology',
  Maternity: 'Maternity',
  MentalHealth: 'Mental health',
};

/**
 * The retention review list (admin): patients whose records are past their minimum retention period (from the
 * schedule: adults 8 years after their care ended, children until their 25th birthday, or 26th if they were 17, or
 * longer when one of their pathways' templates has a team class: oncology, maternity or mental health), the
 * longest overdue first. Nothing is ever deleted automatically: for each, an admin reviews the record, keeps it with a
 * reason until a later review, or anonymises it. Records being kept can be listed too.
 */
export function RetentionReviewPage({search, onSearchChange}: RetentionReviewPageProps) {
  const {pageSize} = useListSettings();
  const page = search.page ?? 1;
  const {data, isPending, isError} = useQuery(retentionListQuery(search, pageSize));
  const total = data?.totalCount ?? 0;
  const rows = (data?.items ?? []) as ItemRow[];
  const [keeping, setKeeping] = useState<RetentionReviewItem | null>(null);
  const [anonymising, setAnonymising] = useState<RetentionReviewItem | null>(null);

  const columns: DataTableColumn<ItemRow>[] = [
    {
      key: 'patient',
      header: 'Patient',
      width: proportional(2, {minWidth: 120}),
      renderCell: (item) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold" maxLines={1}>
            <Link href={`/patients/${item.patientId}`}>{`${item.firstName} ${item.lastName}`}</Link>
          </Text>
          <Text type="supporting" color="secondary" maxLines={1}>
            {item.dateOfBirth ? `Born ${formatDate(item.dateOfBirth)}` : ''}
          </Text>
        </VStack>
      ),
    },
    {
      key: 'class',
      header: 'Retention class',
      width: pixel(130),
      renderCell: (item) => <TextCell text={CLASS_LABELS[item.retentionClass ?? ''] ?? item.retentionClass ?? ''} />,
    },
    {
      key: 'concluded',
      header: 'Care ended',
      width: pixel(130),
      renderCell: (item) => <TextCell text={item.conclusionOfTreatment ? formatDate(item.conclusionOfTreatment) : ''} />,
    },
    {
      key: 'due',
      header: 'Due for review',
      width: proportional(1, {minWidth: 120}),
      renderCell: (item) => (
        <VStack gap={0}>
          <Text type="body" maxLines={1}>
            {item.dueOn ? formatDate(item.dueOn) : ''}
          </Text>
          {item.keptUntil && (
            <Text type="supporting" color="secondary" maxLines={2}>
              {`Kept until ${formatDate(item.keptUntil)}${item.keptReason ? `: ${item.keptReason}` : ''}`}
            </Text>
          )}
        </VStack>
      ),
    },
    {
      key: 'actions',
      header: 'Actions',
      width: pixel(330),
      renderCell: (item) => <RetentionActions item={item} onKeep={() => setKeeping(item)} onAnonymise={() => setAnonymising(item)} />,
    },
  ];

  const noun = total === 1 ? 'record' : 'records';
  const subtitle = isPending ? 'Loading the records due for review...' : `${total} ${noun} past their retention period`;

  return (
    <Page
      title="Retention Review"
      subtitle={subtitle}
      fill
      message={
        isError ? <AlertMessage label="Records not loaded" message="The records due for review could not be loaded. Please try again." /> : undefined
      }
      actions={
        <Switch
          label="Include records being kept"
          value={search.kept === true}
          onChange={(kept: boolean) => onSearchChange({kept: kept || undefined})}
        />
      }
      dialogs={
        <>
          <KeepRecordDialog
            key={`keep-${keeping?.patientId ?? ''}`}
            patient={keeping && {...keeping, patientId: keeping.patientId ?? ''}}
            onDone={() => setKeeping(null)}
            onCancel={() => setKeeping(null)}
          />
          <AnonymiseDialog
            key={`anonymise-${anonymising?.patientId ?? ''}`}
            patient={
              anonymising && {
                id: anonymising.patientId ?? '',
                firstName: anonymising.firstName,
                lastName: anonymising.lastName,
                dateOfBirth: anonymising.dateOfBirth,
              }
            }
            defaultReason="RetentionPeriodEnded"
            onDone={() => setAnonymising(null)}
            onCancel={() => setAnonymising(null)}
          />
        </>
      }>
      <DataTable
        data={rows}
        columns={columns}
        idKey="patientId"
        isLoading={isPending}
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title="No records are due for review"
            description="Records appear here once their minimum retention period has passed. Nothing is deleted automatically."
            isCompact
          />
        }
      />
    </Page>
  );
}

/**
 * A record's Review, Keep and Anonymise. Each button's name says whose record it's for (the visible text stays short),
 * so a screen reader doesn't hear the same "Anonymise" on every row. In a narrow card (a phone), Keep and Anonymise
 * are in one Actions menu, so the patient's name keeps the card's width.
 */
function RetentionActions({item, onKeep, onAnonymise}: {item: RetentionReviewItem; onKeep: () => void; onAnonymise: () => void}) {
  const isCompact = useIsCompactRow();
  const whose = `the record of ${item.firstName} ${item.lastName}`;
  const review = (
    <Button label={`Review ${whose}`} variant="secondary" size="sm" icon={<Icon icon={AppIcon.View} />} href={`/patients/${item.patientId}`}>
      Review
    </Button>
  );
  if (isCompact)
    return (
      <HStack gap={2}>
        {review}
        <ActionsMenu
          name={`Actions for ${whose}`}
          actions={[
            {label: 'Keep', icon: AppIcon.Archive, onClick: onKeep},
            {label: 'Anonymise', icon: AppIcon.Delete, isDestructive: true, onClick: onAnonymise},
          ]}
        />
      </HStack>
    );
  return (
    <HStack gap={2}>
      {review}
      <Button label={`Keep ${whose}`} variant="secondary" size="sm" icon={<Icon icon={AppIcon.Archive} />} onClick={onKeep}>
        Keep
      </Button>
      <Button label={`Anonymise ${whose}`} variant="destructive" size="sm" icon={<Icon icon={AppIcon.Delete} />} onClick={onAnonymise}>
        Anonymise
      </Button>
    </HStack>
  );
}
