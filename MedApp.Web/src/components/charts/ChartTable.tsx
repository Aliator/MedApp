import {Link} from '@astryxdesign/core/Link';
import {proportional, Table, type TableColumn} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';

/** A column of numbers: `item[key]`, headed `label`, in words by `format` (or the table's `formatValue`). */
export interface ChartTableColumn {
  key: string;
  label: string;
  format?: (value: number) => string;
}

export interface ChartTableProps<T extends object> {
  data: readonly T[];
  /** The field naming each row, as in the chart. */
  categoryKey: keyof T & string;
  /** The first column's heading, e.g. "Assigned to" or "Week". */
  categoryHeader: string;
  /** The chart's series (a `ChartSeries` is one), and any more numbers worth a column. */
  series: readonly ChartTableColumn[];
  formatValue?: (value: number) => string;
  formatCategory?: (value: string) => string;
  /** A row's link, e.g. to the list its count is of: the same place its bar goes when clicked. */
  hrefFor?: (item: T) => string | undefined;
  /** Names the table for screen readers, e.g. "Overdue pathways by assignee". */
  label: string;
}

type Row = Record<string, unknown> & {rowKey: string; item: object};

/**
 * A chart's numbers as a table: a row per category, a column per series, with each row's name linking where its bar
 * does. `ChartCard` shows it in place of the chart, for anyone who'd rather read the numbers, can't see the chart or
 * can't point at its bars.
 */
export function ChartTable<T extends object>({
  data,
  categoryKey,
  categoryHeader,
  series,
  formatValue = String,
  formatCategory = String,
  hrefFor,
  label,
}: ChartTableProps<T>) {
  const rows: Row[] = data.map((item, index) => ({rowKey: String(index), item}));
  const value = (row: Row, key: string) => (row.item as Record<string, unknown>)[key];
  const columns: TableColumn<Row>[] = [
    {
      key: 'category',
      header: categoryHeader,
      width: proportional(2, {minWidth: 120}),
      renderCell: (row) => {
        const name = formatCategory(String(value(row, categoryKey) ?? ''));
        const href = hrefFor?.(row.item as T);
        return href ? <Link href={href}>{name}</Link> : <Text type="body">{name}</Text>;
      },
    },
    ...series.map(
      (s): TableColumn<Row> => ({
        key: s.key,
        header: s.label,
        align: 'end',
        width: proportional(1, {minWidth: 72}),
        renderCell: (row) => (
          <Text type="body" hasTabularNumbers>
            {(s.format ?? formatValue)(Number(value(row, s.key) ?? 0))}
          </Text>
        ),
      }),
    ),
  ];
  return <Table data={rows} columns={columns} idKey="rowKey" density="compact" aria-label={label} />;
}
