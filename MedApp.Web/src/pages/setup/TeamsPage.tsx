import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Grid} from '@astryxdesign/core/Grid';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {NumberInput} from '@astryxdesign/core/NumberInput';
import {VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {fieldStatus} from '../../api/errors';
import {
  DEFAULT_TEAM_TARGETS,
  MAX_TARGET_DAYS,
  MIN_TARGET_DAYS,
  teamsQuery,
  teamTargets,
  TARGET_PRIORITIES,
  targetsWords,
  useSaveTeam,
  useTeamActions,
  type Team,
  type TeamTargets,
} from '../../api/teams';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import type {SetupListSearch} from '../listSearch';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {actionProblem, fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle, type PageError} from './setupList';

type TeamRow = Team & Record<string, unknown>;

type DialogState = {kind: 'edit'; team: Team | null} | {kind: 'archive' | 'delete'; team: Team} | null;

/** The whole list, archived ones too: the page filters it (`setupPage`). The route preloads it. */
export const teamsPageQuery = teamsQuery(true);

const count = (n: number | undefined, one: string, many: string) => `${n ?? 0} ${n === 1 ? one : many}`;

/**
 * What uses a team, in words: "2 templates, 2 letter templates, 14 pathways, 5 referrals, 3 members of staff", or
 * "Not used yet".
 */
export function teamUse(team: Team): string {
  const parts = [
    team.templateCount ? count(team.templateCount, 'template', 'templates') : '',
    team.letterTemplateCount ? count(team.letterTemplateCount, 'letter template', 'letter templates') : '',
    team.pathwayCount ? count(team.pathwayCount, 'pathway', 'pathways') : '',
    team.referralCount ? count(team.referralCount, 'referral', 'referrals') : '',
    team.memberCount ? count(team.memberCount, 'member of staff', 'members of staff') : '',
  ].filter(Boolean);
  return parts.length ? parts.join(', ') : 'Not used yet';
}

/**
 * The teams nobody can triage referrals to: not archived, and none of their members holds TriageReferrals
 * (`triagerCount` 0). Their referrals would wait with nobody able to accept, reject or redirect them (hazard H-40).
 */
export const untriageable = (teams: readonly Team[]) => teams.filter((s) => !s.isArchived && s.triagerCount === 0);

/** Names in a list: "Dermatology", "Dermatology and Urology", "Cardiology, Dermatology and Urology". */
const namesInWords = (names: readonly string[]) => (names.length < 2 ? (names[0] ?? '') : `${names.slice(0, -1).join(', ')} and ${names.at(-1)}`);

/** The page's warning about teams nobody can triage, in words. */
export function untriageableWords(teams: readonly Team[]): string {
  const names = untriageable(teams).map((s) => s.name ?? '');
  const these = names.length === 1 ? 'this team' : 'these teams';
  return `Nobody can triage referrals to ${namesInWords(names)}: no member of ${these} holds the Triage referrals permission. Give one a role that has it, or add a member who has.`;
}

export interface TeamsPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/** The fields the edit dialog shows its API errors on. */
const FORM_FIELDS = ['name', ...TARGET_PRIORITIES.map((p) => p.field)];

/**
 * Teams (admin): what pathway templates belong to and staff work in, such as Dermatology or Urology. Admins add
 * them, change their names and waiting-time targets (days, for each priority: the centre's own targets, which a
 * referral's clock copies when it starts), archive ones no longer wanted (templates, pathways and staff that have one keep it, but it isn't
 * offered for new ones), restore them, and delete ones nothing has used. Renaming, archiving and restoring send the
 * version shown, so a change someone made meanwhile is refused (409) with the API's words, and the list is read again.
 * Members of staff are given their teams on the Users page.
 */
export function TeamsPage({search, onSearchChange}: TeamsPageProps) {
  const {data = [], isPending, isError} = useQuery(teamsPageQuery);
  const save = useSaveTeam();
  const actions = useTeamActions();
  const announce = useAnnounce();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<DialogState>(null);
  const [name, setName] = useState('');
  const [targets, setTargets] = useState<TeamTargets>(DEFAULT_TEAM_TARGETS);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<PageError>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize);
  const close = () => setDialog(null);

  const openEditor = (team: Team | null) => {
    setName(team?.name ?? '');
    setTargets(team ? teamTargets(team) : DEFAULT_TEAM_TARGETS);
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', team});
  };
  const open = (kind: 'archive' | 'delete', team: Team) => {
    setError(null);
    setDialog({kind, team});
  };
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(actionProblem(e, action, 'team'));
    close();
  };
  const versionOf = (team: Team) => ({id: team.id ?? '', expectedVersion: team.version ?? 0});

  const editing = dialog?.kind === 'edit' ? dialog.team : null;
  const target = dialog && dialog.kind !== 'edit' ? dialog.team : null;

  const saveTeam = () =>
    save.mutate(
      editing ? {id: editing.id, name: name.trim(), targets, expectedVersion: editing.version ?? 0} : {name: name.trim(), targets},
      {
        onSuccess: () => {
          announce(editing ? 'Team saved' : 'Team added');
          close();
        },
        // A 409 (the name is taken, or someone changed it meanwhile) keeps the dialog open with the API's words. The
        // list has been read again by then (`useSaveTeam`), so saving again, having been told, starts from the
        // version that's there now.
        onError: (e) => {
          setProblem(saveProblem(e, editing ? 'save' : 'add', 'team'));
          const fresh = editing && queryClient.getQueryData(teamsPageQuery.queryKey)?.find((s) => s.id === editing.id);
          if (fresh) setDialog({kind: 'edit', team: fresh});
        },
      },
    );

  const columns: DataTableColumn<TeamRow>[] = [
    {
      key: 'name',
      header: 'Team',
      width: proportional(3, {minWidth: 140}),
      renderCell: (s) => (
        <SetupNameCell
          name={s.name ?? ''}
          secondary={teamUse(s)}
          token={
            // The lab (a team of its own kind) takes no referrals, so it needs nobody to triage them.
            s.kind === 'Lab' ? (
              <Token size="sm" label="The lab" color="blue" />
            ) : !s.isArchived && s.triagerCount === 0 ? (
              <Token size="sm" label="Nobody can triage" color="orange" icon={<Icon icon={AppIcon.Warning} />} />
            ) : undefined
          }
        />
      ),
    },
    {
      key: 'targets',
      header: 'Waiting-time targets',
      width: proportional(2, {minWidth: 120}),
      renderCell: (s) => (
        <Text type="body" maxLines={2}>
          {targetsWords(s)}
        </Text>
      ),
    },
    {key: 'templates', header: 'Templates', width: pixel(100), align: 'end', renderCell: (s) => <Text type="body">{s.templateCount ?? 0}</Text>},
    {key: 'pathways', header: 'Pathways', width: pixel(100), align: 'end', renderCell: (s) => <Text type="body">{s.pathwayCount ?? 0}</Text>},
    {key: 'members', header: 'Staff', width: pixel(80), align: 'end', renderCell: (s) => <Text type="body">{s.memberCount ?? 0}</Text>},
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (s) => <SetupStatusToken isArchived={s.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(124),
      renderCell: (s) => (
        <TableActions
          actions={[
            {label: 'Edit team', icon: AppIcon.Edit, onClick: () => openEditor(s)},
            s.isArchived
              ? {
                  label: 'Restore team',
                  icon: AppIcon.Restore,
                  onClick: () => {
                    setError(null);
                    actions.restore.mutate(versionOf(s), {onError: failed('restore')});
                  },
                }
              : {label: 'Archive team', icon: AppIcon.Archive, onClick: () => open('archive', s)},
            {
              label: s.canBeDeleted ? 'Delete team' : 'Used by templates, pathways or staff, so it can be archived but not deleted',
              icon: AppIcon.Delete,
              isDestructive: true,
              isDisabled: !s.canBeDeleted,
              onClick: () => open('delete', s),
            },
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading teams...' : setupSubtitle(total, search, 'team', 'teams');

  const summary = target && (
    <MetadataList>
      <MetadataListItem label="Team">{target.name}</MetadataListItem>
      <MetadataListItem label="Waiting-time targets">{targetsWords(target)}</MetadataListItem>
      <MetadataListItem label="Used by">{teamUse(target)}</MetadataListItem>
    </MetadataList>
  );

  return (
    <Page
      title="Teams"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage label={error.label} message={error.message} />
        ) : (
          isError ? (
            <AlertMessage label="Teams not loaded" message="The teams could not be loaded. Please try again." />
          ) : (
            untriageable(data).length > 0 && (
              <SummaryAlert
                status="warning"
                label="Nobody can triage"
                summary={namesInWords(untriageable(data).map((s) => s.name ?? ''))}
                message={untriageableWords(data)}
              />
            )
          )
        )
      }
      actions={<Button label="Add Team" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => openEditor(null)} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Team' : 'Add Team'}
            message={
              editing
                ? 'Templates, pathways and staff show the new name everywhere they have it. New targets apply to waiting-time clocks set from now on.'
                : 'Each pathway template belongs to a team, and staff can be given the ones they work in.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Team'}
            isProcessing={save.isPending}
            confirmDisabled={!name.trim() || TARGET_PRIORITIES.some(({key}) => targets[key] === null)}
            hasSaveShortcut
            onConfirm={saveTeam}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={FORM_FIELDS} />}
              {problem.message && <AlertMessage label={editing ? 'Not saved' : 'Not added'} message={problem.message} />}
              {/* Only while it's open, so its autofocus can't act in a closing dialog. */}
              {dialog?.kind === 'edit' && (
                <TextInput
                  label="Name"
                  value={name}
                  onChange={setName}
                  placeholder="e.g. Dermatology"
                  autoComplete="off"
                  isRequired
                  hasAutoFocus
                  status={fieldStatus(problem.validation, 'name')}
                />
              )}
              {dialog?.kind === 'edit' && (
                <VStack gap={1}>
                  <Text type="label" weight="semibold">
                    Waiting-time targets
                  </Text>
                  <Text type="supporting" color="secondary">
                    {`Days from a referral being received to its target date, for each priority (${MIN_TARGET_DAYS} to ${MAX_TARGET_DAYS}). They're the centre's own targets, not national rules.`}
                  </Text>
                  <Grid columns={2} gap={4}>
                    {TARGET_PRIORITIES.map(({key, label, field}) => (
                      <NumberInput
                        key={key}
                        label={label}
                        value={targets[key]}
                        onChange={(value) => setTargets((current) => ({...current, [key]: value}))}
                        min={MIN_TARGET_DAYS}
                        max={MAX_TARGET_DAYS}
                        isIntegerOnly
                        isWheelEnabled={false}
                        isRequired
                        status={fieldStatus(problem.validation, field)}
                      />
                    ))}
                  </Grid>
                </VStack>
              )}
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'archive'}
            title="Archive Team"
            message="It won't be offered for new templates or given to more staff. Templates, pathways and staff that have it keep it, and it can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => target && actions.archive.mutate(versionOf(target), {onSuccess: close, onError: failed('archive')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="No template, pathway or member of staff has this team. This action cannot be undone."
            isDanger
            confirmText="Delete Team"
            isProcessing={actions.remove.isPending}
            onConfirm={() => target && actions.remove.mutate(target.id ?? '', {onSuccess: close, onError: failed('delete')})}
            onCancel={close}>
            {summary}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No teams yet"
          description="Each pathway template belongs to a team, such as Dermatology or Urology. A team in use can only be archived."
          icon={<Icon icon={AppIcon.Teams} size="lg" />}
          actions={<Button label="Add Your First Team" variant="primary" onClick={() => openEditor(null)} />}
        />
      ) : (
        <DataTable
          data={rows as TeamRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="teams"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search teams"
              placeholder="Search teams by name, or filter by status..."
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
              title="No teams match your search"
              description="Search for archived ones too, to see and restore them."
              isCompact
              actions={<Button label="Show Archived" variant="secondary" onClick={() => onSearchChange({...search, page: undefined, status: 'any'})} />}
            />
          }
        />
      )}
    </Page>
  );
}
