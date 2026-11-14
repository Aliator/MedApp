import {Card} from '@astryxdesign/core/Card';
import {Pagination} from '@astryxdesign/core/Pagination';
import {Section} from '@astryxdesign/core/Section';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Table, useTableSortable, type TableColumn} from '@astryxdesign/core/Table';
import {useLayoutEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode} from 'react';
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
  /** How many skeleton rows, without paging; with it, a whole page of them. */
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
 * The shortest a row gets: two lines (a cell's main line and its smaller second line) and the cell padding. In a
 * window too short for a whole page of rows this tall, the rows scroll inside the card.
 */
export const MIN_ROW_HEIGHT = 60;

/**
 * The height that shares the table's space equally between a whole page of rows, under the header, or
 * `MIN_ROW_HEIGHT` if that's taller. Measured from the scrolling area, and again whenever it resizes.
 */
function useRowHeight(scroller: React.RefObject<HTMLElement | null>, rows: number | undefined, deps: readonly unknown[]) {
  const [height, setHeight] = useState<number | undefined>(undefined);

  useLayoutEffect(() => {
    const element = scroller.current;
    if (!element || !rows) return;
    const fit = () => {
      const header = element.querySelector('thead')?.getBoundingClientRect().height ?? 0;
      const share = Math.floor((element.clientHeight - header) / rows);
      setHeight(Math.max(MIN_ROW_HEIGHT, share));
    };
    fit();
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(fit);
    observer.observe(element);
    return () => observer.disconnect();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [scroller, rows, ...deps]);

  return height;
}

/**
 * A data table (replaces Blazor's DataTable and SortableHeader): sortable headers, page controls, skeleton
 * rows while loading, and an empty state. Sorting and paging are controlled, and done by the API, so the page
 * keeps them in the URL and the table only shows what it is given.
 *
 * It sits in a card that fills the height its parent gives it (e.g. a `Page` with `fill`), with the page controls
 * pinned to the bottom. With paging, every row is the same height: a whole page of rows shares the card's height
 * equally, so a full page fills it exactly whatever the window's height, and a short last page keeps the same row
 * height. Cells keep to the row's two lines (TextCell, and maxLines on Text), truncating what doesn't fit.
 * Rows never get shorter than `MIN_ROW_HEIGHT`; in a window too short for a page of those, they scroll inside
 * the card.
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
      // Astryx goes from descending to unsorted; a list always has an order, so that click goes back to ascending.
      else if (sort) onSortChange?.({column: sort.column, ascending: true});
    },
  });
  const plugins = useMemo(() => (onSortChange ? {sortable} : undefined), [onSortChange, sortable]);
  const showPaging = !isLoading && pagination !== undefined && pagination.totalItems > 0;
  const scroller = useRef<HTMLElement>(null);
  const rowHeight = useRowHeight(scroller, pagination?.pageSize, [isLoading, data.length === 0, showPaging]);
  const rowHeightStyle = rowHeight ? ({'--medapp-table-row-height': `${rowHeight}px`} as CSSProperties) : undefined;

  let table: ReactNode;
  if (isLoading) {
    const rows: SkeletonRow[] = Array.from({length: pagination?.pageSize ?? skeletonRows}, (_, i) => ({skeletonId: i}));
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
            <StackItem size="fill" isScrollable ref={scroller} style={data.length > 0 || isLoading ? rowHeightStyle : undefined}>
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
