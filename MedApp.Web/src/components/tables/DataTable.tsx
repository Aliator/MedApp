import {Card} from '@astryxdesign/core/Card';
import {Pagination} from '@astryxdesign/core/Pagination';
import {Section} from '@astryxdesign/core/Section';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Table, useTableSortable, type TableColumn} from '@astryxdesign/core/Table';
import {useMemo, type ReactNode} from 'react';
import type {TableSort} from './sorting';

export type DataTableColumn<T extends Record<string, unknown>> = TableColumn<T>;

/** Server-side paging: the table shows one page of rows the API returned, and page controls below it. */
export interface DataTablePagination {
  /** The current page, from 1. */
  page: number;
  pageSize: number;
  /** Rows matching the search across every page (the API's totalCount). */
  totalItems: number;
  onPageChange: (page: number) => void;
}

export interface DataTableProps<T extends Record<string, unknown>> {
  /** The rows to show: the current page from the API, or every row when there's no pagination. */
  data: readonly T[];
  /** Columns; mark sortable ones with `sortable: true` (the sort key is the column key). */
  columns: DataTableColumn<T>[];
  idKey: keyof T & string;
  /** Shows skeleton rows instead of data. */
  isLoading?: boolean;
  skeletonRows?: number;
  /** The current sort, shown in the column header. */
  sort?: TableSort | null;
  /** Called when a sortable header is clicked; ascending, then descending, then ascending again. */
  onSortChange?: (sort: TableSort) => void;
  /** Page controls for server-side paging. Omit to show every row with no controls. */
  pagination?: DataTablePagination;
  /** Shown when there are no rows, e.g. an EmptyState with an action. */
  emptyState?: ReactNode;
  /**
   * A narrow panel inside the card, beside the table (its header and rows, not the page controls) and divided
   * from it, e.g. Home's switch to the other task list. The page controls run under both, so they stay in the
   * same place whether there's a panel or not.
   */
  edge?: {side: 'start' | 'end'; content: ReactNode};
}

type SkeletonRow = {skeletonId: number} & Record<string, unknown>;

/**
 * A data table (replaces Blazor's DataTable and SortableHeader): sortable headers, page controls, skeleton
 * rows while loading, and an empty state. Sorting and paging are controlled, and done by the API, so the page
 * keeps them in the URL and the table only shows what it is given.
 *
 * It sits in a card that fills the height its parent gives it (e.g. a `Page` with `fill`): rows at the top,
 * page controls pinned to the bottom, and empty space between when there are few rows. Rows that don't fit
 * scroll inside the card.
 */
export function DataTable<T extends Record<string, unknown>>({
  data,
  columns,
  idKey,
  isLoading = false,
  skeletonRows = 5,
  sort,
  onSortChange,
  pagination,
  emptyState,
  edge,
}: DataTableProps<T>) {
  const sortable = useTableSortable<T>({
    sort: sort ? [{sortKey: sort.column, direction: sort.ascending ? 'ascending' : 'descending'}] : [],
    onSortChange: (next) => {
      const first = next[0];
      if (first) onSortChange?.({column: first.sortKey, ascending: first.direction === 'ascending'});
    },
  });
  const plugins = useMemo(() => (onSortChange ? {sortable} : undefined), [onSortChange, sortable]);
  const showPaging = !isLoading && pagination !== undefined && pagination.totalItems > 0;

  let table: ReactNode;
  if (isLoading) {
    const rows: SkeletonRow[] = Array.from({length: skeletonRows}, (_, i) => ({skeletonId: i}));
    const skeletonColumns: TableColumn<SkeletonRow>[] = columns.map((column, c) => ({
      key: column.key,
      header: column.header,
      width: column.width,
      align: column.align,
      renderCell: (row) => <Skeleton height={16} width="80%" index={row.skeletonId * columns.length + c} />,
    }));
    table = <Table data={rows} columns={skeletonColumns} idKey="skeletonId" aria-busy="true" />;
  } else {
    table = (
      <Table
        data={data as T[]}
        columns={columns}
        idKey={idKey}
        plugins={plugins}
        emptyState={emptyState}
        rowIndexStart={pagination ? (pagination.page - 1) * pagination.pageSize + 1 : undefined}
        rowCount={pagination?.totalItems}
      />
    );
  }

  const edgePanel = edge && (
    <StackItem>
      <Section variant="transparent" padding={0} width={44} height="100%" dividers={[edge.side === 'start' ? 'end' : 'start']}>
        {edge.content}
      </Section>
    </StackItem>
  );

  return (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        <StackItem size="fill">
          <HStack gap={0} height="100%">
            {edge?.side === 'start' && edgePanel}
            <StackItem size="fill" isScrollable>
              {/* A little room between the card's edges and the first and last columns (Table has no prop for it). */}
              <Section variant="transparent" paddingBlock={0} paddingInline={4}>
                {table}
              </Section>
            </StackItem>
            {edge?.side === 'end' && edgePanel}
          </HStack>
        </StackItem>
        {showPaging && (
          <Section variant="muted" padding={3} dividers={['top']}>
            <HStack hAlign="end">
              <Pagination
                page={pagination.page}
                pageSize={pagination.pageSize}
                totalItems={pagination.totalItems}
                onChange={pagination.onPageChange}
                label="Table pagination"
              />
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );
}
