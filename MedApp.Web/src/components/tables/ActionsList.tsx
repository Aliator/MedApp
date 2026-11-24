import {List, ListItem, type ListProps} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {createContext, useContext, type ReactNode} from 'react';
import {useNarrowerThan} from '../views/useNarrowerThan';
import {COMPACT_ROW_REM, CompactRowContext} from './RowCards';

const IsCompactListContext = createContext(false);

/**
 * A list whose rows end with their status and actions (the setup lists that aren't tables: consent forms, hospitals,
 * sample types). On a list narrower than `COMPACT_ROW_REM` (a phone), each row's actions go under its label, so the
 * name keeps the width, and `TableActions` folds more than two into one menu, as in `RowCards`. Rows are
 * `ActionsListItem`s.
 */
export function ActionsList({children, ...props}: ListProps) {
  const [measure, isCompact] = useNarrowerThan(COMPACT_ROW_REM);
  return (
    <IsCompactListContext.Provider value={isCompact}>
      <List ref={measure} {...props}>
        {children}
      </List>
    </IsCompactListContext.Provider>
  );
}

/** A row of an `ActionsList`: its label, and its actions at the end (under the label in a narrow list). */
export function ActionsListItem({label, actions}: {label: ReactNode; actions: ReactNode}) {
  const isCompact = useContext(IsCompactListContext);
  if (!isCompact) return <ListItem label={label} endContent={actions} />;
  return (
    <ListItem
      label={
        <VStack gap={2}>
          {label}
          <HStack gap={2} vAlign="center" wrap="wrap">
            <CompactRowContext.Provider value>{actions}</CompactRowContext.Provider>
          </HStack>
        </VStack>
      }
    />
  );
}
