import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {DEFAULT_MIN_COLUMN_WIDTH, pixel, Table, useTableSortable, type TableColumn} from '@astryxdesign/core/Table';
import {useEffect, useLayoutEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode} from 'react';
import {useAnnounce} from '../../app/announcements';
import {PageControls} from './PageControls';
import {RowCards} from './RowCards';
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
  /** Content at the top of the card, above the table, e.g. Home's switch between its lists. */
  top?: ReactNode;
  /** A search above the card, as wide as it (e.g. a `ListFilterBar`); the card fills the height left under it. */
  search?: ReactNode;
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

/** Astryx's floor for a column with no width (not exported). */
const FLEXIBLE_COLUMN_MIN_WIDTH = 60;

/** The narrowest a column can be: a pixel column's width, or a proportional (or width-less) column's floor. */
function minColumnWidth(column: TableColumn<Record<string, unknown>>): number {
  const width = column.width;
  if (!width) return FLEXIBLE_COLUMN_MIN_WIDTH;
  if (width.type === 'pixel') return width.value;
  return width.minWidth ?? DEFAULT_MIN_COLUMN_WIDTH;
}

/**
 * The columns at exact widths for the space the table has: each at its narrowest, plus an equal share of whatever
 * space is left, so the table only scrolls sideways when even its narrowest columns don't fit. (Astryx's own
 * sizing shares the width out by proportion and keeps every share above its column's floor, so it scrolls sooner,
 * and pixel columns never grow.) Measured from the area the table sits in, and again whenever it resizes; until
 * then, the columns as given.
 */
/**
 * Narrower than this, a table shows cards whatever its columns' floors: squeezed that far (a phone, or 400% zoom),
 * every column is too narrow to read, even where they'd all technically fit.
 */
const MIN_TABLE_WIDTH = 480;

function useFittedColumns<T extends Record<string, unknown>>(
  columns: TableColumn<T>[],
  scroller: React.RefObject<HTMLElement | null>,
): {fitted: TableColumn<T>[]; isTooNarrow: boolean} {
  const [space, setSpace] = useState<number | undefined>(undefined);

  useLayoutEffect(() => {
    const element = scroller.current;
    if (!element) return;
    const measure = () => {
      // The table sits inside a padded section: its room is that section's, less the padding.
      const inner = element.firstElementChild;
      const style = inner ? getComputedStyle(inner) : null;
      const padding = style ? parseFloat(style.paddingLeft) + parseFloat(style.paddingRight) : 0;
      setSpace(Math.floor((inner ?? element).clientWidth - padding));
    };
    measure();
    if (typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [scroller]);

  return useMemo(() => {
    if (!space || space <= 0) return {fitted: columns, isTooNarrow: false};
    const mins = columns.map((column) => minColumnWidth(column as TableColumn<Record<string, unknown>>));
    const total = mins.reduce((sum, min) => sum + min, 0);
    const share = Math.floor(Math.max(0, space - total) / columns.length);
    return {
      fitted: columns.map((column, i) => ({...column, width: pixel(mins[i] + share)})),
      isTooNarrow: total > space || space < MIN_TABLE_WIDTH,
    };
  }, [columns, space]);
}

/** Tells screen readers what a paged list found, each time its results arrive (`useAnnounce`). */
function useResultsAnnouncement(pagination: DataTablePagination | undefined) {
  const announce = useAnnounce();
  const total = pagination?.totalItems;
  const page = pagination?.page;
  const pages = pagination ? Math.max(1, Math.ceil(pagination.totalItems / pagination.pageSize)) : 0;
  useEffect(() => {
    if (total === undefined) return;
    // "Found", never "results", which could be read as clinical results.
    const found = total === 0 ? 'Nothing found' : `${total} found`;
    announce(pages > 1 ? `${found}, page ${page} of ${pages}` : found);
  }, [announce, total, page, pages]);
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
 *
 * When its columns don't fit even at their narrowest (a half view, or a narrow window), or it's narrower than
 * `MIN_TABLE_WIDTH` (a phone, or 400% zoom), it shows each row as a small card instead (`RowCards`), so it never
 * scrolls sideways.
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
  top,
  search,
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
  useResultsAnnouncement(isLoading ? undefined : pagination);
  const scroller = useRef<HTMLElement>(null);
  const rowHeight = useRowHeight(scroller, pagination?.pageSize, [isLoading, data.length === 0, showPaging]);
  const rowHeightStyle = rowHeight ? ({'--medapp-table-row-height': `${rowHeight}px`} as CSSProperties) : undefined;
  const {fitted, isTooNarrow} = useFittedColumns(columns, scroller);

  let table: ReactNode;
  if (isTooNarrow) {
    // Too narrow for its columns, even at their narrowest: each row as a small card instead (no sideways scrolling).
    table = (
      <RowCards
        data={data}
        columns={columns}
        idKey={idKey}
        isLoading={isLoading}
        skeletonRows={pagination?.pageSize ?? skeletonRows}
        sort={sort}
        onSortChange={onSortChange}
        emptyState={emptyState}
      />
    );
  } else if (isLoading) {
    const rows: SkeletonRow[] = Array.from({length: pagination?.pageSize ?? skeletonRows}, (_, i) => ({skeletonId: i}));
    const skeletonColumns: TableColumn<SkeletonRow>[] = fitted.map((column, c) => ({
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
        columns={fitted}
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

  const card = (
    <Card padding={0} width="100%" height="100%">
      <VStack gap={0} height="100%">
        {top}
        <StackItem size="fill">
          <HStack gap={0} height="100%">
            {edge?.side === 'start' && edgePanel}
            <StackItem size="fill" isScrollable ref={scroller} style={!isTooNarrow && (data.length > 0 || isLoading) ? rowHeightStyle : undefined}>
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
              <PageControls
                page={pagination.page}
                totalPages={Math.max(1, Math.ceil(pagination.totalItems / pagination.pageSize))}
                onPageChange={pagination.onPageChange}
                label="Table pagination"
              />
            </HStack>
          </Section>
        )}
      </VStack>
    </Card>
  );

  if (!search) return card;
  return (
    <VStack gap={3} height="100%">
      {search}
      <StackItem size="fill">{card}</StackItem>
    </VStack>
  );
}
