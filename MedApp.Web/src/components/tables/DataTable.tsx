import {Skeleton} from '@astryxdesign/core/Skeleton';
import {Table, useTablePagination, useTableSortable, type TableColumn} from '@astryxdesign/core/Table';
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
}

type SkeletonRow = {skeletonId: number} & Record<string, unknown>;

/**
 * A data table (replaces Blazor's DataTable and SortableHeader): sortable headers, page controls, skeleton
 * rows while loading, and an empty state. Sorting and paging are controlled, and done by the API, so the page
 * keeps them in the URL and the table only shows what it is given.
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
}: DataTableProps<T>) {
  const sortable = useTableSortable<T>({
    sort: sort ? [{sortKey: sort.column, direction: sort.ascending ? 'ascending' : 'descending'}] : [],
    onSortChange: (next) => {
      const first = next[0];
      if (first) onSortChange?.({column: first.sortKey, ascending: first.direction === 'ascending'});
    },
  });
  // Hooks can't be conditional, so the plugin always exists; it's only passed to the table with `pagination`.
  const paging = useTablePagination<T>({
    page: pagination?.page ?? 1,
    pageSize: pagination?.pageSize,
    totalItems: pagination?.totalItems ?? 0,
    onPageChange: (page) => pagination?.onPageChange(page),
    align: 'end',
  });
  const plugins = useMemo(() => {
    const active: Record<string, typeof sortable> = {};
    if (onSortChange) active.sortable = sortable;
    if (pagination && pagination.totalItems > 0) active.pagination = paging;
    return Object.keys(active).length > 0 ? active : undefined;
  }, [onSortChange, sortable, pagination, paging]);

  if (isLoading) {
    const rows: SkeletonRow[] = Array.from({length: skeletonRows}, (_, i) => ({skeletonId: i}));
    const skeletonColumns: TableColumn<SkeletonRow>[] = columns.map((column, c) => ({
      key: column.key,
      header: column.header,
      width: column.width,
      align: column.align,
      renderCell: (row) => <Skeleton height={16} width="80%" index={row.skeletonId * columns.length + c} />,
    }));
    return <Table data={rows} columns={skeletonColumns} idKey="skeletonId" aria-busy="true" />;
  }

  return (
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
