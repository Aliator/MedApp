import {CheckboxList, CheckboxListItem} from '@astryxdesign/core/CheckboxList';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import type {ReactNode} from 'react';
import {ConfirmDialog, type ConfirmDialogProps} from './ConfirmDialog';

export interface SelectionItem {
  value: string;
  label: string;
}

export interface SelectionDialogProps extends Omit<ConfirmDialogProps, 'children'> {
  /** The choices, e.g. the roles a user can be given. */
  items: SelectionItem[];
  /** The values currently ticked. */
  selected: string[];
  onSelectedChange: (selected: string[]) => void;
  /** Names the list for screen readers, e.g. "Roles". */
  listLabel: string;
  /** Shown above the list, e.g. what the user has now. */
  info?: ReactNode;
  /** Shown instead of the list when there's nothing to choose, e.g. "This user already has every role". */
  emptyTitle?: string;
  emptyDescription?: string;
  /** Confirming with nothing ticked is a choice too, e.g. a user with no roles. Off by default. */
  canConfirmNone?: boolean;
}

/**
 * Pick some items from a list, then confirm (replaces Blazor's SelectionModal), e.g. a user's roles. Confirm is
 * disabled until something is ticked, unless `canConfirmNone`.
 */
export function SelectionDialog({
  items,
  selected,
  onSelectedChange,
  listLabel,
  info,
  emptyTitle = 'Nothing to choose',
  emptyDescription,
  canConfirmNone = false,
  confirmDisabled = false,
  isProcessing = false,
  ...confirm
}: SelectionDialogProps) {
  return (
    <ConfirmDialog
      {...confirm}
      isProcessing={isProcessing}
      confirmDisabled={confirmDisabled || items.length === 0 || (selected.length === 0 && !canConfirmNone)}>
      {info}
      {items.length === 0 ? (
        <EmptyState title={emptyTitle} description={emptyDescription} isCompact />
      ) : (
        <CheckboxList
          label={listLabel}
          isLabelHidden
          value={selected}
          onChange={onSelectedChange}
          isDisabled={isProcessing}
          hasDividers>
          {items.map((item) => (
            <CheckboxListItem key={item.value} value={item.value} label={item.label} />
          ))}
        </CheckboxList>
      )}
    </ConfirmDialog>
  );
}
