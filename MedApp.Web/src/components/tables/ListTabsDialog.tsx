import {createReorderable} from '@astryxdesign/core';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {PowerSearch, type PowerSearchConfig, type PowerSearchFilter} from '@astryxdesign/core/PowerSearch';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useMemo, useState} from 'react';
import type {FavouriteValues, ListTab} from '../../app/favourites';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AppIcon} from '../shared/icons';
import {DisabledReason} from '../shared/DisabledReason';

/** What the dialog shows: the tabs, one tab's name and search (a new one has no ID), or the question before a reset. */
type Mode = {kind: 'list'} | {kind: 'edit'; id?: string; name: string; filters: PowerSearchFilter[]} | {kind: 'reset'};

export interface ListTabsDialogProps {
  isOpen: boolean;
  onClose: () => void;
  /** The list's tabs, in order. */
  tabs: ListTab[];
  /** Whether the person has changed them (Reset is offered only then). */
  isCustomised: boolean;
  /** The bar's power search set-up, for a tab's search. */
  config: PowerSearchConfig;
  /** A search's tokens, and back (`filtersFrom`, `valuesFrom` with the list's fields). */
  toFilters: (values: FavouriteValues) => PowerSearchFilter[];
  toValues: (filters: readonly PowerSearchFilter[]) => FavouriteValues;
  /** The list's search now: a new tab starts with it. */
  current: FavouriteValues;
  /** What the list is, for the search's name ("Search patients"). */
  label: string;
  onAdd: (name: string, values: FavouriteValues) => void;
  onEdit: (id: string, name: string, values: FavouriteValues) => void;
  onRemove: (id: string) => void;
  onMove: (id: string, to: number) => void;
  onReset: () => void;
}

/**
 * Configuration of a list's tabs (`ListFilterBar`'s menu): add one (a name, and its search set with the same power
 * search as the list's), edit or remove one, drag them into order (or move one with the keyboard from its handle, as
 * the pathway template editor's stages), and reset to the list's defaults. Changes are saved as they're made.
 */
export function ListTabsDialog({
  isOpen,
  onClose,
  tabs,
  isCustomised,
  config,
  toFilters,
  toValues,
  current,
  label,
  onAdd,
  onEdit,
  onRemove,
  onMove,
  onReset,
}: ListTabsDialogProps) {
  const [mode, setMode] = useState<Mode>({kind: 'list'});
  const reorderable = useMemo(() => createReorderable({onReorder: ({key, to}) => onMove(String(key), to)}), [onMove]);
  const toList = () => setMode({kind: 'list'});
  const close = () => {
    toList();
    onClose();
  };

  if (mode.kind === 'edit') {
    const save = () => {
      const values = toValues(mode.filters);
      if (mode.id) onEdit(mode.id, mode.name, values);
      else onAdd(mode.name, values);
      toList();
    };
    return (
      <ConfirmDialog
        isOpen={isOpen}
        title={mode.id ? 'Edit Favourite' : 'Add Favourite'}
        message="Name the favourite, and set the search it shows."
        icon={AppIcon.Favourite}
        confirmText="Save Favourite"
        cancelText="Back"
        confirmDisabled={!mode.name.trim()}
        onConfirm={save}
        onCancel={toList}>
        <VStack gap={4}>
          <TextInput label="Name" value={mode.name} onChange={(name) => setMode({...mode, name})} isRequired hasAutoFocus autoComplete="off" />
          <VStack gap={1}>
            <Text type="label">Search</Text>
            <PowerSearch
              config={config}
              filters={mode.filters}
              onChange={(filters) => setMode({...mode, filters: [...filters]})}
              label={`The favourite's search: ${label}`}
              placeholder="No filters: the favourite shows everything. Add a filter..."
              presentation="popover"
            />
          </VStack>
        </VStack>
      </ConfirmDialog>
    );
  }

  if (mode.kind === 'reset')
    return (
      <ConfirmDialog
        isOpen={isOpen}
        title="Reset Favourites"
        message="Go back to the list's own favourites? The favourites you added, and your changes to its own, are removed."
        icon={AppIcon.Favourite}
        intent="warning"
        confirmText="Reset"
        cancelText="Back"
        onConfirm={() => {
          onReset();
          toList();
        }}
        onCancel={toList}
      />
    );

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Edit Favourites"
      message="Your favourites, shown above the list, in order. Drag one to move it, or press its handle and use the arrow keys. Changes are saved as you make them."
      icon={AppIcon.Favourite}
      confirmText="Done"
      cancelText="Close"
      onConfirm={close}
      onCancel={close}>
      <VStack gap={4}>
        <List reorderable={reorderable} hasDividers aria-label="Favourites in order">
          {tabs.map((tab, i) => (
            <ListItem
              key={tab.id}
              itemKey={tab.id}
              label={`${i + 1}. ${tab.label}`}
              description={tab.isDefault ? undefined : 'Added by you'}
              endContent={
                <HStack gap={1} vAlign="center">
                  <IconButton
                    label={`Edit ${tab.label}`}
                    tooltip="Edit"
                    icon={<Icon icon={AppIcon.Edit} />}
                    variant="ghost"
                    size="sm"
                    onClick={() => setMode({kind: 'edit', id: tab.id, name: tab.label, filters: toFilters(tab.values)})}
                  />
                  <DisabledReason reason={tabs.length === 1 ? 'A list keeps one favourite at least' : undefined}>
                    <IconButton
                      label={`Remove ${tab.label}`}
                      tooltip={tabs.length === 1 ? undefined : 'Remove'}
                      icon={<Icon icon={AppIcon.Delete} />}
                      variant="ghost"
                      size="sm"
                      isDisabled={tabs.length === 1}
                      onClick={() => onRemove(tab.id)}
                    />
                  </DisabledReason>
                </HStack>
              }
            />
          ))}
        </List>
        <HStack gap={2} wrap="wrap">
          <Button
            label="Add Favourite"
            variant="secondary"
            icon={<Icon icon={AppIcon.Add} />}
            onClick={() => setMode({kind: 'edit', name: '', filters: toFilters(current)})}
          />
          <Button label="Reset to Default" variant="ghost" isDisabled={!isCustomised} onClick={() => setMode({kind: 'reset'})} />
        </HStack>
      </VStack>
    </ConfirmDialog>
  );
}
