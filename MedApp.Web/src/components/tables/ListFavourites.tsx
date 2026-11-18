import {DropdownMenu, type DropdownMenuItemData, type DropdownMenuOption} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useEffect, useState} from 'react';
import {useSavedFavourites, type FavouriteValues} from '../../app/favourites';
import {NO_STATUS_LINE} from '../forms/fieldSpace';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AppIcon} from '../shared/icons';

/** A quick look-up the list offers (its own, not one someone saved): a name, and the filters it sets. */
export interface QuickFavourite {
  label: string;
  values: FavouriteValues;
}

export interface ListFavouritesProps {
  /** Which list's saved searches these are, e.g. `patients`. */
  list: string;
  /** The list's own quick look-ups. */
  quick?: QuickFavourite[];
  /** The list's search now, as the bar holds it (every field's key, set or not). */
  current: FavouriteValues;
  /** A search as the bar would hold it, for comparing (so a saved one matches the list showing it). */
  normalize: (values: FavouriteValues) => FavouriteValues;
  /** Shows a favourite: sets its filters (and clears the rest). */
  onApply: (values: FavouriteValues) => void;
  /** Whether the list opened with no filters in its URL: the moment it takes its starting search, if it has one. */
  isUnfiltered: boolean;
}

/** A search, as a key that's the same for the same filters (whatever order, and leaving out unset ones). */
const keyOf = (values: FavouriteValues) =>
  JSON.stringify(
    Object.entries(values)
      .filter(([, v]) => v !== undefined && v !== '')
      .sort(([a], [b]) => a.localeCompare(b)),
  );

const nameOf = (name: string) => name.trim() || 'Untitled favourite';

/**
 * The star beside a list's search: its quick look-ups, the searches you've saved (kept with your account), saving
 * the search you have, and managing them (rename, delete, and the one the list starts on). Nothing sits beside the
 * star: which favourite the list is showing, if any, is in the star's name ("Favourites (showing My open work)") and
 * its tooltip, and the bar's own tokens show the filters.
 */
export function ListFavourites({
  list,
  quick = [],
  current,
  normalize,
  onApply,
  isUnfiltered,
}: ListFavouritesProps) {
  const favourites = useSavedFavourites(list);
  const [dialog, setDialog] = useState<'save' | 'manage' | null>(null);
  const [name, setName] = useState('');
  const [isStart, setIsStart] = useState(false);

  // Opened with no filters: it takes its starting search, if it has one. Only then, so clearing the search later
  // clears it.
  useEffect(() => {
    if (isUnfiltered && favourites.start) onApply(favourites.start.values);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const currentKey = keyOf(current);
  const isEmpty = currentKey === '[]';
  const quickShowing = quick.find((q) => keyOf(normalize(q.values)) === currentKey)?.label;
  const savedShowing = favourites.saved.find((f) => keyOf(normalize(f.values)) === currentKey)?.name;
  // A saved search someone named wins over a quick filter with the same filters.
  const showing = savedShowing ?? quickShowing;
  const isSaved = favourites.saved.some((f) => keyOf(normalize(f.values)) === currentKey);

  const items: DropdownMenuOption[] = [];
  if (quick.length)
    items.push({
      type: 'section',
      title: 'Quick filters',
      items: quick.map((q): DropdownMenuItemData => ({id: `quick-${q.label}`, label: q.label, onClick: () => onApply(q.values)})),
    });
  if (favourites.saved.length)
    items.push({
      type: 'section',
      title: 'Your favourites',
      items: favourites.saved.map(
        (f): DropdownMenuItemData => ({
          id: f.id,
          label: nameOf(f.name),
          description: favourites.start?.id === f.id ? 'Starting search' : undefined,
          icon: AppIcon.Favourite,
          onClick: () => onApply(f.values),
        }),
      ),
    });
  if (items.length) items.push({type: 'divider'});
  items.push(
    {
      id: 'save',
      label: 'Save this search…',
      description: isEmpty ? 'Add a filter first' : isSaved ? 'Already saved' : undefined,
      icon: AppIcon.Add,
      isDisabled: isEmpty || isSaved,
      onClick: () => {
        setName('');
        setIsStart(false);
        setDialog('save');
      },
    },
    {
      id: 'manage',
      label: 'Manage favourites…',
      icon: AppIcon.Edit,
      isDisabled: favourites.saved.length === 0,
      onClick: () => setDialog('manage'),
    },
  );

  return (
    <HStack gap={2} vAlign="center">
      <DropdownMenu
        items={items}
        alignment="end"
        renderTrigger={(trigger) => (
          <IconButton
            {...trigger}
            label={showing ? `Favourites (showing ${showing})` : 'Favourites'}
            icon={<Icon icon={AppIcon.Favourite} />}
            variant="secondary"
          />
        )}
      />
      <ConfirmDialog
        isOpen={dialog === 'save'}
        title="Save Favourite"
        message="Save this list's search, to come back to it from the star."
        icon={AppIcon.Favourite}
        confirmText="Save"
        confirmDisabled={!name.trim()}
        onConfirm={() => {
          favourites.add(name, current, isStart);
          setDialog(null);
        }}
        onCancel={() => setDialog(null)}>
        {dialog === 'save' && (
          <VStack gap={4}>
            <TextInput label="Name" value={name} onChange={setName} isRequired hasAutoFocus autoComplete="off" />
            <Switch label="Start the list on it" description="Opening this list with no filters shows this search." value={isStart} onChange={setIsStart} />
          </VStack>
        )}
      </ConfirmDialog>
      <ConfirmDialog
        isOpen={dialog === 'manage'}
        title="Manage Favourites"
        message="Rename them, choose the one the list starts on, or delete them. Changes are saved as you make them."
        icon={AppIcon.Favourite}
        confirmText="Done"
        cancelText="Close"
        onConfirm={() => setDialog(null)}
        onCancel={() => setDialog(null)}>
        {dialog === 'manage' && (
          <VStack gap={4}>
            {favourites.saved.length === 0 && (
              <Text type="body" color="secondary">
                No favourites saved.
              </Text>
            )}
            {favourites.saved.map((f) => (
              <HStack key={f.id} gap={3} vAlign="end">
                <StackItem size="fill">
                  <TextInput
                    label="Name"
                    value={f.name}
                    onChange={(value) => favourites.rename(f.id, value)}
                    autoComplete="off"
                    containerStyle={NO_STATUS_LINE}
                  />
                </StackItem>
                <Switch
                  label="Starting search"
                  value={favourites.start?.id === f.id}
                  onChange={(on) => favourites.setStart(on ? f.id : undefined)}
                />
                <IconButton label={`Delete ${nameOf(f.name)}`} icon={<Icon icon={AppIcon.Delete} />} variant="ghost" onClick={() => favourites.remove(f.id)} />
              </HStack>
            ))}
          </VStack>
        )}
      </ConfirmDialog>
    </HStack>
  );
}
