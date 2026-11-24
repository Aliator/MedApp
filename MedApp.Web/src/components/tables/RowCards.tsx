import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import type {TableColumn} from '@astryxdesign/core/Table';
import {createContext, useContext, type ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {useNarrowerThan} from '../views/useNarrowerThan';
import type {TableSort} from './sorting';

/** Below this width (rem), a card's actions go under its title, and `TableActions` folds more than two into a menu. */
export const COMPACT_ROW_REM = 30;

export const CompactRowContext = createContext(false);

/** Whether a row's cells are in a narrow card (a phone): its actions under its title, more than two in one Actions menu. */
export const useIsCompactRow = () => useContext(CompactRowContext);

export interface RowCardsProps<T extends Record<string, unknown>> {
  data: readonly T[];
  /** The table's columns: the first is each card's title, `actions` sits beside it, the rest are listed under it. */
  columns: TableColumn<T>[];
  idKey: keyof T & string;
  isLoading?: boolean;
  /** How many placeholder cards while loading. */
  skeletonRows: number;
  sort?: TableSort | null;
  onSortChange?: (sort: TableSort) => void;
  emptyState?: ReactNode;
}

/** A column's name, for its label and the sort menu (headers are text in this app; the key otherwise). */
const nameOf = <T extends Record<string, unknown>>(column: TableColumn<T>) =>
  typeof column.header === 'string' ? column.header : column.key;

/**
 * A table's rows as small cards, one under another, for when its columns don't fit even at their narrowest (a half
 * view, or a narrow window): each row's first column as its title, its actions beside that, and the other columns
 * listed under it, each with its name. Sorting moves to a Sort menu above them (the column headers are gone). On a
 * card narrower than `COMPACT_ROW_REM` (a phone), the actions go under the title, so it keeps the whole width.
 * `DataTable` switches to this by itself; use that.
 */
export function RowCards<T extends Record<string, unknown>>({
  data,
  columns,
  idKey,
  isLoading = false,
  skeletonRows,
  sort,
  onSortChange,
  emptyState,
}: RowCardsProps<T>) {
  const actions = columns.find((column) => column.key === 'actions');
  const [title, ...rest] = columns.filter((column) => column !== actions);
  const sortable = columns.filter((column) => column.sortable);
  const sortedBy = sortable.find((column) => column.key === sort?.column);
  const [measure, isCompact] = useNarrowerThan(COMPACT_ROW_REM);

  const sortMenu = onSortChange && sortable.length > 0 && (
    <HStack hAlign="end">
      <DropdownMenu
        items={sortable.map((column) => ({
          id: column.key,
          label: nameOf(column),
          // The column sorted by: its direction. Choosing it again turns it round.
          description:
            column.key === sort?.column ? (sort.ascending ? 'Ascending (choose again for descending)' : 'Descending (choose again for ascending)') : undefined,
          icon: column.key === sort?.column ? (sort.ascending ? AppIcon.SortAsc : AppIcon.SortDesc) : undefined,
          onClick: () => onSortChange({column: column.key, ascending: column.key === sort?.column ? !sort.ascending : true}),
        }))}
        alignment="end"
        renderTrigger={(trigger) => (
          <Button
            {...trigger}
            label={sortedBy ? `Sort: ${nameOf(sortedBy)}` : 'Sort'}
            aria-label={sortedBy ? `Sort by ${nameOf(sortedBy)}, ${sort?.ascending ? 'ascending' : 'descending'}` : 'Sort'}
            icon={<Icon icon={sort?.ascending === false ? AppIcon.SortDesc : AppIcon.SortAsc} />}
            variant="ghost"
            size="sm"
          />
        )}
      />
    </HStack>
  );

  if (isLoading) {
    return (
      <VStack gap={0} aria-busy="true">
        {Array.from({length: skeletonRows}, (_, i) => (
          <Section key={i} variant="transparent" paddingInline={4} paddingBlock={3} dividers={['bottom']}>
            <VStack gap={2}>
              <Skeleton height={18} width="60%" index={i * 2} />
              <Skeleton height={14} width="85%" index={i * 2 + 1} />
            </VStack>
          </Section>
        ))}
      </VStack>
    );
  }

  return (
    <VStack gap={0} ref={measure}>
      {sortMenu}
      {data.length === 0 ? (
        emptyState
      ) : (
        <VStack gap={0} role="list">
          {data.map((row) => (
            <Section key={String(row[idKey])} variant="transparent" paddingInline={4} paddingBlock={3} dividers={['bottom']} role="listitem">
              <VStack gap={2}>
                {isCompact ? (
                  <>
                    <CompactRowContext.Provider value>{title?.renderCell?.(row)}</CompactRowContext.Provider>
                    {actions && (
                      <HStack gap={2} wrap="wrap">
                        <CompactRowContext.Provider value>{actions.renderCell?.(row)}</CompactRowContext.Provider>
                      </HStack>
                    )}
                  </>
                ) : (
                  <HStack gap={2} vAlign="start">
                    <StackItem size="fill">{title?.renderCell?.(row)}</StackItem>
                    {actions?.renderCell?.(row)}
                  </HStack>
                )}
                {rest.length > 0 && (
                  <MetadataList>
                    {rest.map((column) => (
                      <MetadataListItem key={column.key} label={nameOf(column)}>
                        {column.renderCell?.(row)}
                      </MetadataListItem>
                    ))}
                  </MetadataList>
                )}
              </VStack>
            </Section>
          ))}
        </VStack>
      )}
    </VStack>
  );
}
