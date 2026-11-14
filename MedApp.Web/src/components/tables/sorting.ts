/** A table's sort: which column, and which way. The pages keep it in the URL (`sort`, `asc`), as Blazor does. */
export interface TableSort {
  column: string;
  ascending: boolean;
}

const collator = new Intl.Collator('en-GB', {sensitivity: 'base', numeric: true});

/**
 * Sorts rows by a column without changing the input. Text compares case-insensitively and numbers in numeric
 * order; empty values (null, undefined, '') always go last. Ties keep their original order.
 */
export function sortRows<T>(rows: readonly T[], sort: TableSort | null | undefined, value: (row: T, column: string) => unknown): T[] {
  if (!sort) return [...rows];
  const direction = sort.ascending ? 1 : -1;
  const isEmpty = (v: unknown) => v === null || v === undefined || v === '';

  return rows
    .map((row, index) => ({row, index, key: value(row, sort.column)}))
    .sort((a, b) => {
      const aEmpty = isEmpty(a.key);
      const bEmpty = isEmpty(b.key);
      if (aEmpty || bEmpty) return aEmpty === bEmpty ? a.index - b.index : aEmpty ? 1 : -1;
      const order =
        typeof a.key === 'number' && typeof b.key === 'number'
          ? a.key - b.key
          : collator.compare(String(a.key), String(b.key));
      return order !== 0 ? order * direction : a.index - b.index;
    })
    .map(({row}) => row);
}
