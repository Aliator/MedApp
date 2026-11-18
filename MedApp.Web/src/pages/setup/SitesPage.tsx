import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {fieldStatus} from '../../api/errors';
import {addressLine, sitesQuery, useRoomActions, useSaveRoom, useSaveSite, useSiteActions, type Room, type Site} from '../../api/sites';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle} from './setupList';

/** The page's query: every site and room, archived ones too (the list filters them). Its route's loader preloads the same. */
export const sitesPageQuery = sitesQuery({includeArchived: true});

type SiteRow = Site & Record<string, unknown>;

/** The site dialogs, and the rooms dialog with the dialogs it opens (each goes back to it when done). */
type DialogState =
  | {kind: 'site'; site: Site | null}
  | {kind: 'archive-site' | 'delete-site'; site: Site}
  | {kind: 'rooms'; siteId: string}
  | {kind: 'room'; siteId: string; room: Room | null}
  | {kind: 'delete-room'; siteId: string; room: Room}
  | null;

interface SiteForm {
  name: string;
  /** Always three, some empty. */
  lines: [string, string, string];
  postcode: string;
  phone: string;
}

const EMPTY_FORM: SiteForm = {name: '', lines: ['', '', ''], postcode: '', phone: ''};

/** The fields a site's 400 can name. */
const SITE_FIELDS = ['name', 'addressLines', 'postcode', 'phone'];
const ROOM_FIELDS = ['name'];

/** A site's details as the form starts with them. */
export function siteForm(site: Site | null): SiteForm {
  if (!site) return EMPTY_FORM;
  const lines = site.addressLines ?? [];
  return {name: site.name ?? '', lines: [lines[0] ?? '', lines[1] ?? '', lines[2] ?? ''], postcode: site.postcode ?? '', phone: site.phone ?? ''};
}

/** What the form sends: blank address lines left out, and an empty postcode or phone as none (the API removes it). */
export function siteRequest(form: SiteForm) {
  const optional = (text: string) => text.trim() || null;
  return {
    name: form.name.trim(),
    addressLines: form.lines.map((line) => line.trim()).filter(Boolean),
    postcode: optional(form.postcode),
    phone: optional(form.phone),
  };
}

/** A site's rooms in words: the open ones' names ("Clinic room 1, Clinic room 2", or "No rooms"), then how many are archived. */
export function roomsWords(site: Pick<Site, 'rooms'>): string {
  const rooms = site.rooms ?? [];
  const open = rooms.filter((r) => !r.isArchived).map((r) => r.name ?? '');
  const archived = rooms.length - open.length;
  return `${open.length > 0 ? open.join(', ') : 'No rooms'}${archived > 0 ? ` (${archived} archived)` : ''}`;
}

export interface SitesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * Sites and Rooms (admin, docs/centre-phases-3-5-plan.md decisions 118 and 121): the places the centre works from
 * (a name, an address of up to three lines, a postcode and a phone number) and each one's rooms. Admins add and edit
 * sites, archive ones no longer used (kept by what names them, but not offered for anything new; a site's rooms must be
 * archived first, and one site must always stay open), restore them, and delete ones nothing has used. Each site's
 * Rooms opens its rooms, where admins add, rename, archive, restore and delete them the same way. The API's refusals
 * (409s) show in its own words.
 */
export function SitesPage({search, onSearchChange}: SitesPageProps) {
  const {data = [], isPending, isError} = useQuery(sitesPageQuery);
  const saveSite = useSaveSite();
  const siteActions = useSiteActions();
  const saveRoom = useSaveRoom();
  const roomActions = useRoomActions();
  const announce = useAnnounce();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [form, setForm] = useState<SiteForm>(EMPTY_FORM);
  const [roomName, setRoomName] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<string | null>(null);
  const [roomError, setRoomError] = useState<string | null>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);

  // The site whose rooms are open (the rooms dialog, or a room dialog opened from it), as the list has it now.
  const roomsSiteId = dialog && 'siteId' in dialog ? dialog.siteId : null;
  const roomsSite = roomsSiteId ? data.find((s) => s.id === roomsSiteId) : undefined;
  const backToRooms = () => setDialog(roomsSiteId ? {kind: 'rooms', siteId: roomsSiteId} : null);

  const openSiteEditor = (site: Site | null) => {
    setForm(siteForm(site));
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'site', site});
  };
  const openSiteDialog = (kind: 'archive-site' | 'delete-site', site: Site) => {
    setError(null);
    setDialog({kind, site});
  };
  const openRooms = (site: Site) => {
    setError(null);
    setRoomError(null);
    setDialog({kind: 'rooms', siteId: site.id ?? ''});
  };
  const openRoomEditor = (room: Room | null) => {
    if (!roomsSiteId) return;
    setRoomName(room?.name ?? '');
    setProblem(NO_PROBLEM);
    setRoomError(null);
    setDialog({kind: 'room', siteId: roomsSiteId, room});
  };

  const editingSite = dialog?.kind === 'site' ? dialog.site : null;
  const targetSite = dialog?.kind === 'archive-site' || dialog?.kind === 'delete-site' ? dialog.site : null;
  const editingRoom = dialog?.kind === 'room' ? dialog.room : null;
  const targetRoom = dialog?.kind === 'delete-room' ? dialog.room : null;

  const siteFailed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(saveProblem(e, action, 'site').message);
    close();
  };

  const submitSite = () =>
    saveSite.mutate(
      {id: editingSite?.id, ...siteRequest(form)},
      {
        onSuccess: () => {
          announce(editingSite ? 'Site saved' : 'Site added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editingSite ? 'save' : 'add', 'site')),
      },
    );

  const submitRoom = () => {
    if (!roomsSiteId) return;
    saveRoom.mutate(
      {id: editingRoom?.id, siteId: roomsSiteId, name: roomName.trim()},
      {
        onSuccess: () => {
          announce(editingRoom ? 'Room renamed' : 'Room added');
          backToRooms();
        },
        onError: (e) => setProblem(saveProblem(e, editingRoom ? 'save' : 'add', 'room')),
      },
    );
  };

  const setRoomArchived = (room: Room, archive: boolean) => {
    setRoomError(null);
    (archive ? roomActions.archive : roomActions.restore).mutate(room.id ?? '', {
      onSuccess: () => announce(`${room.name ?? 'Room'} ${archive ? 'archived' : 'restored'}`),
      onError: (e) => setRoomError(saveProblem(e, archive ? 'archive' : 'restore', 'room').message),
    });
  };

  const columns: DataTableColumn<SiteRow>[] = [
    {
      key: 'name',
      header: 'Site',
      width: proportional(3, {minWidth: 180}),
      renderCell: (s) => <SetupNameCell name={s.name ?? ''} secondary={(s.addressLines ?? []).join(', ') || null} />,
    },
    {key: 'postcode', header: 'Postcode', width: pixel(110), renderCell: (s) => <Text type="body">{s.postcode}</Text>},
    {key: 'phone', header: 'Phone', width: pixel(150), renderCell: (s) => <Text type="body">{s.phone}</Text>},
    {
      key: 'rooms',
      header: 'Rooms',
      width: proportional(2, {minWidth: 140}),
      renderCell: (s) => (
        <Text type="body" color={(s.rooms ?? []).some((r) => !r.isArchived) ? undefined : 'secondary'} maxLines={2}>
          {roomsWords(s)}
        </Text>
      ),
    },
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (s) => <SetupStatusToken isArchived={s.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(160),
      renderCell: (s) => (
        <TableActions
          actions={[
            {label: 'Rooms', icon: AppIcon.Rooms, onClick: () => openRooms(s)},
            {label: 'Edit site', icon: AppIcon.Edit, onClick: () => openSiteEditor(s)},
            s.isArchived
              ? {
                  label: 'Restore site',
                  icon: AppIcon.Restore,
                  onClick: () => {
                    setError(null);
                    siteActions.restore.mutate(s.id ?? '', {onSuccess: () => announce('Site restored'), onError: siteFailed('restore')});
                  },
                }
              : {label: 'Archive site', icon: AppIcon.Archive, onClick: () => openSiteDialog('archive-site', s)},
            {
              label: s.canBeDeleted ? 'Delete site' : "Has rooms or closed days, or is the only open site, so it can't be deleted",
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !s.canBeDeleted,
              onClick: () => openSiteDialog('delete-site', s),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading sites...' : setupSubtitle(total, search, 'site', 'sites');

  const siteSummary = targetSite && (
    <MetadataList>
      <MetadataListItem label="Site">{targetSite.name}</MetadataListItem>
      {addressLine(targetSite) && <MetadataListItem label="Address">{addressLine(targetSite)}</MetadataListItem>}
    </MetadataList>
  );

  const roomSummary = targetRoom && (
    <MetadataList>
      <MetadataListItem label="Site">{roomsSite?.name}</MetadataListItem>
      <MetadataListItem label="Room">{targetRoom.name}</MetadataListItem>
    </MetadataList>
  );

  const setLine = (index: number, value: string) =>
    setForm((current) => {
      const lines: SiteForm['lines'] = [...current.lines];
      lines[index] = value;
      return {...current, lines};
    });

  const rooms = roomsSite?.rooms ?? [];

  return (
    <Page
      title="Sites and Rooms"
      subtitle={subtitle}
      fill
      message={
        error ? <AlertMessage message={error} /> : isError && <AlertMessage message="The sites could not be loaded. Please try again." />
      }
      actions={<Button label="Add Site" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openSiteEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'site'}
            title={editingSite ? 'Edit Site' : 'Add Site'}
            message={editingSite ? 'Everything that names the site shows the change.' : 'A place the centre works from, such as a clinic or an outreach centre.'}
            icon={editingSite ? AppIcon.Edit : AppIcon.Add}
            confirmText={editingSite ? 'Save' : 'Add Site'}
            isProcessing={saveSite.isPending}
            confirmDisabled={!form.name.trim()}
            hasSaveShortcut
            onConfirm={submitSite}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={SITE_FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={form.name}
                onChange={(name) => setForm((current) => ({...current, name}))}
                placeholder="e.g. Northgate outreach clinic"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              {([0, 1, 2] as const).map((index) => (
                <TextInput
                  key={index}
                  label={`Address line ${index + 1}`}
                  value={form.lines[index]}
                  onChange={(value) => setLine(index, value)}
                  autoComplete="off"
                  status={index === 0 ? fieldStatus(problem.validation, 'addressLines') : undefined}
                />
              ))}
              <TextInput
                label="Postcode"
                value={form.postcode}
                onChange={(postcode) => setForm((current) => ({...current, postcode}))}
                autoComplete="off"
                status={fieldStatus(problem.validation, 'postcode')}
              />
              <TextInput
                label="Phone"
                type="tel"
                value={form.phone}
                onChange={(phone) => setForm((current) => ({...current, phone}))}
                autoComplete="off"
                status={fieldStatus(problem.validation, 'phone')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive-site'}
            title="Archive Site"
            message="It won't be offered for anything new, such as rooms or closed days. What names it keeps it, and it can be restored. Its rooms must be archived first."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={siteActions.archive.isPending}
            onConfirm={() =>
              targetSite &&
              siteActions.archive.mutate(targetSite.id ?? '', {
                onSuccess: () => {
                  announce('Site archived');
                  close();
                },
                onError: siteFailed('archive'),
              })
            }
            onCancel={close}>
            {siteSummary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete-site'}
            title="Confirm Delete"
            message="The site has no rooms and no closed days. This action cannot be undone."
            isDanger
            confirmText="Delete Site"
            isProcessing={siteActions.remove.isPending}
            onConfirm={() =>
              targetSite &&
              siteActions.remove.mutate(targetSite.id ?? '', {
                onSuccess: () => {
                  announce('Site deleted');
                  close();
                },
                onError: siteFailed('delete'),
              })
            }
            onCancel={close}>
            {siteSummary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'rooms'}
            title={`Rooms at ${roomsSite?.name ?? 'the site'}`}
            message="Where appointments and other work take place. A room that has been used is archived, not deleted."
            icon={AppIcon.Rooms}
            confirmText="Add Room"
            confirmDisabled={!roomsSite || roomsSite.isArchived === true}
            cancelText="Done"
            onConfirm={() => openRoomEditor(null)}
            onCancel={close}>
            <VStack gap={2}>
              {roomError && <AlertMessage message={roomError} />}
              {roomsSite?.isArchived && <AlertMessage status="info" message="The site is archived: restore it to add rooms or restore its rooms." />}
              {rooms.length === 0 ? (
                <Text type="body" color="secondary">
                  No rooms yet.
                </Text>
              ) : (
                <List edge="flush" hasDividers aria-label="Rooms">
                  {rooms.map((room) => (
                    <ListItem
                      key={room.id}
                      label={
                        <Text type="body" weight="semibold" maxLines={1}>
                          {room.name}
                        </Text>
                      }
                      endContent={
                        <HStack gap={2} vAlign="center">
                          <SetupStatusToken isArchived={room.isArchived ?? false} />
                          <TableActions
                            actions={[
                              {label: `Rename ${room.name}`, icon: AppIcon.Edit, onClick: () => openRoomEditor(room)},
                              room.isArchived
                                ? {label: `Restore ${room.name}`, icon: AppIcon.Restore, onClick: () => setRoomArchived(room, false)}
                                : {label: `Archive ${room.name}`, icon: AppIcon.Archive, onClick: () => setRoomArchived(room, true)},
                              {
                                label: room.canBeDeleted ? `Delete ${room.name}` : `${room.name} has been used, so it can be archived but not deleted`,
                                icon: AppIcon.Delete,
                                isDestructive: true,
                                isDisabled: !room.canBeDeleted,
                                onClick: () => {
                                  setRoomError(null);
                                  setDialog({kind: 'delete-room', siteId: roomsSiteId ?? '', room});
                                },
                              },
                            ]}
                          />
                        </HStack>
                      }
                    />
                  ))}
                </List>
              )}
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'room'}
            title={editingRoom ? 'Rename Room' : 'Add Room'}
            message={`At ${roomsSite?.name ?? 'the site'}. A room stays at its site.`}
            icon={editingRoom ? AppIcon.Edit : AppIcon.Add}
            confirmText={editingRoom ? 'Save' : 'Add Room'}
            isProcessing={saveRoom.isPending}
            confirmDisabled={!roomName.trim()}
            hasSaveShortcut
            onConfirm={submitRoom}
            onCancel={backToRooms}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={ROOM_FIELDS} />}
              {problem.message && <AlertMessage message={problem.message} />}
              <TextInput
                label="Name"
                value={roomName}
                onChange={setRoomName}
                placeholder="e.g. Clinic room 1"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete-room'}
            title="Confirm Delete"
            message="Nothing has used this room. This action cannot be undone."
            isDanger
            confirmText="Delete Room"
            isProcessing={roomActions.remove.isPending}
            onConfirm={() =>
              targetRoom &&
              roomActions.remove.mutate(targetRoom.id ?? '', {
                onSuccess: () => {
                  announce(`${targetRoom.name ?? 'Room'} deleted`);
                  backToRooms();
                },
                onError: (e) => {
                  setRoomError(saveProblem(e, 'delete', 'room').message);
                  backToRooms();
                },
              })
            }
            onCancel={backToRooms}>
            {roomSummary}
          </ConfirmDialog>
        </>
      }>
      <DataTable
        data={rows as SiteRow[]}
        columns={columns}
        idKey="id"
        isLoading={isPending}
        search={
          <ListFilterBar
            fields={SETUP_FILTER_FIELDS}
            values={setupBarValues(search)}
            onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
            list="sites"
            isUnfiltered={!search.name && search.status === undefined}
            label="Search sites"
            placeholder="Search sites by name, or filter by status..."
            textKey="name"
            favourites={SETUP_FAVOURITES}
          />
        }
        pagination={{
          page,
          pageSize,
          totalItems: total,
          onPageChange: (next) => onSearchChange({...search, page: next > 1 ? next : undefined}),
        }}
        emptyState={
          <EmptyState
            title="No sites match your search"
            description="Search for archived ones too, to see and restore them."
            icon={<Icon icon={AppIcon.Sites} size="lg" />}
            isCompact
            actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
          />
        }
      />
    </Page>
  );
}
