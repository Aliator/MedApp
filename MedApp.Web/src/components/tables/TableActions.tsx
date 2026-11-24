import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack} from '@astryxdesign/core/Stack';
import {ActionsMenu} from '../shared/ActionsMenu';
import {useIsCompactRow} from './RowCards';

export interface TableAction {
  /** Names the button for screen readers and its tooltip, e.g. "Edit". */
  label: string;
  icon: IconType;
  /** Navigates here (e.g. a patient's page)… */
  href?: string;
  /** …or runs this (e.g. opening a confirmation). */
  onClick?: () => void;
  isDisabled?: boolean;
  isDestructive?: boolean;
}

/**
 * A row's action buttons (replaces Blazor's TableActions and ActionButton): icon buttons with tooltips; more than two
 * in a narrow card (a phone) are one Actions menu.
 */
export function TableActions({actions}: {actions: TableAction[]}) {
  const isCompact = useIsCompactRow();
  if (isCompact && actions.length > 2) return <ActionsMenu actions={actions} />;
  return (
    <HStack gap={1} vAlign="center" hAlign="end">
      {actions.map((action) => (
        <IconButton
          key={action.label}
          label={action.label}
          tooltip={action.label}
          icon={<Icon icon={action.icon} />}
          variant={action.isDestructive ? 'destructive' : 'ghost'}
          size="sm"
          href={action.href}
          onClick={action.onClick}
          isDisabled={action.isDisabled}
        />
      ))}
    </HStack>
  );
}
