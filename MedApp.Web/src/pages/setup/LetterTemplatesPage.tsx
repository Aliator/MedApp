import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {useListSettings} from '../../app/listSettings';
import {
  compareLetterTemplates,
  letterKindLabel,
  letterTemplatesQuery,
  letterTemplateTeam,
  useLetterTemplateActions,
  type LetterTemplate,
} from '../../api/letterTemplates';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {ListFilterBar} from '../../components/tables/ListFilterBar';
import {TableActions} from '../../components/tables/TableActions';
import {TextCell} from '../../components/tables/TextCell';
import type {SetupListSearch} from '../listSearch';
import {actionProblem, fromSetupBar, SETUP_FAVOURITES, SETUP_FILTER_FIELDS, setupBarValues, setupPage, setupSubtitle, type PageError} from './setupList';

type TemplateRow = LetterTemplate & Record<string, unknown>;

/** The whole list, archived ones too: the page filters it (`setupPage`). The route preloads it. */
export const letterTemplatesPageQuery = letterTemplatesQuery(true);

/** Where a template is edited, and where a new one is made. */
export const letterTemplateHref = (id: string) => `/admin/letter-templates/${id}`;
export const NEW_LETTER_TEMPLATE_HREF = '/admin/letter-templates/new';

/** A template's second line: its kind and team, "Clinic letter · Dermatology". */
export const letterTemplateSummary = (template: LetterTemplate) => `${letterKindLabel(template.kind)} · ${letterTemplateTeam(template)}`;

export interface LetterTemplatesPageProps {
  search: SetupListSearch;
  onSearchChange: (search: SetupListSearch) => void;
}

/**
 * Letter templates (decision 99): the letterhead, body and closing letters start from, each for a kind of letter and
 * a team or every team. Templates for every team come first, then by team and name. Each is added
 * and edited on its own page; one no longer wanted is archived (letters made from it keep it, but it isn't offered for
 * new ones) and can be restored; one no letter has been written from (`canBeDeleted`) can be deleted instead, after
 * asking. Archiving and restoring send the version shown, so a change made meanwhile is refused (409) with the API's
 * words, and the list is read again.
 */
export function LetterTemplatesPage({search, onSearchChange}: LetterTemplatesPageProps) {
  const {data = [], isPending, isError} = useQuery(letterTemplatesPageQuery);
  const actions = useLetterTemplateActions();
  const announce = useAnnounce();
  const [archiving, setArchiving] = useState<LetterTemplate | null>(null);
  const [deleting, setDeleting] = useState<LetterTemplate | null>(null);
  const [error, setError] = useState<PageError>(null);

  const {pageSize} = useListSettings();
  const {rows, page, total} = setupPage(data, search, pageSize, compareLetterTemplates);
  const versionOf = (template: LetterTemplate) => ({id: template.id ?? '', expectedVersion: template.version ?? 0});
  const failed = (action: 'archive' | 'restore' | 'delete') => (e: unknown) => {
    setError(actionProblem(e, action, 'letter template'));
    setArchiving(null);
    setDeleting(null);
  };
  const remove = (template: LetterTemplate) =>
    actions.remove.mutate(template.id ?? '', {
      onSuccess: () => {
        announce(`${template.name} deleted`);
        setDeleting(null);
      },
      onError: failed('delete'),
    });

  const archive = (template: LetterTemplate) =>
    actions.archive.mutate(versionOf(template), {
      onSuccess: () => {
        announce(`${template.name} archived`);
        setArchiving(null);
      },
      onError: failed('archive'),
    });
  const restore = (template: LetterTemplate) => {
    setError(null);
    actions.restore.mutate(versionOf(template), {onSuccess: () => announce(`${template.name} restored`), onError: failed('restore')});
  };

  const columns: DataTableColumn<TemplateRow>[] = [
    {
      key: 'name',
      header: 'Template',
      width: proportional(3, {minWidth: 160}),
      renderCell: (t) => <SetupNameCell name={t.name ?? ''} secondary={letterTemplateSummary(t)} />,
    },
    {key: 'kind', header: 'Kind', width: proportional(1, {minWidth: 120}), renderCell: (t) => <TextCell text={letterKindLabel(t.kind)} />},
    {
      key: 'team',
      header: 'Team',
      width: proportional(1, {minWidth: 120}),
      renderCell: (t) => <TextCell text={letterTemplateTeam(t)} />,
    },
    {key: 'status', header: 'Status', width: pixel(100), renderCell: (t) => <SetupStatusToken isArchived={t.isArchived ?? false} />},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(128),
      // A template no letter has used is deleted; the rest are archived (and restored), as letters keep their text.
      renderCell: (t) => (
        <TableActions
          actions={[
            {label: 'Edit letter template', icon: AppIcon.Edit, href: letterTemplateHref(t.id ?? '')},
            ...(t.isArchived
              ? [{label: 'Restore letter template', icon: AppIcon.Restore, onClick: () => restore(t)}]
              : t.canBeDeleted
                ? []
                : [
                    {
                      label: 'Archive letter template',
                      icon: AppIcon.Archive,
                      onClick: () => {
                        setError(null);
                        setArchiving(t);
                      },
                    },
                  ]),
            ...(t.canBeDeleted
              ? [
                  {
                    label: 'Delete letter template',
                    icon: AppIcon.Delete,
                    isDestructive: true,
                    onClick: () => {
                      setError(null);
                      setDeleting(t);
                    },
                  },
                ]
              : []),
          ]}
        />
      ),
    },
  ];

  const subtitle = isPending ? 'Loading letter templates...' : setupSubtitle(total, search, 'letter template', 'letter templates');
  const addButton = <Button label="Add Letter Template" variant="primary" icon={<Icon icon={AppIcon.Add} />} href={NEW_LETTER_TEMPLATE_HREF} />;

  return (
    <Page
      title="Letter Templates"
      subtitle={subtitle}
      fill
      message={
        error ? (
          <AlertMessage label={error.label} message={error.message} />
        ) : (
          isError && <AlertMessage label="Letter templates not loaded" message="The letter templates could not be loaded. Please try again." />
        )
      }
      actions={addButton}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={deleting !== null}
            title="Delete Letter Template"
            message="No letter has been written from it. This can't be undone."
            icon={AppIcon.Delete}
            isDanger
            confirmText="Delete"
            isProcessing={actions.remove.isPending}
            onConfirm={() => deleting && remove(deleting)}
            onCancel={() => setDeleting(null)}>
            {deleting && (
              <MetadataList>
                <MetadataListItem label="Template">{deleting.name}</MetadataListItem>
                <MetadataListItem label="For">{letterTemplateSummary(deleting)}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
          <ConfirmDialog
            isOpen={archiving !== null}
            title="Archive Letter Template"
            message="It won't be offered for new letters. Letters already written from it keep it, and it can be restored."
            icon={AppIcon.Archive}
            intent="warning"
            confirmText="Archive"
            isProcessing={actions.archive.isPending}
            onConfirm={() => archiving && archive(archiving)}
            onCancel={() => setArchiving(null)}>
            {archiving && (
              <MetadataList>
                <MetadataListItem label="Template">{archiving.name}</MetadataListItem>
                <MetadataListItem label="For">{letterTemplateSummary(archiving)}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      {!isPending && !isError && data.length === 0 ? (
        <EmptyState
          title="No letter templates yet"
          description="A letter starts from a template: a letterhead, a body with the headings its author fills in, and a closing."
          icon={<Icon icon={AppIcon.LetterTemplates} size="lg" />}
          actions={<Button label="Add Your First Letter Template" variant="primary" href={NEW_LETTER_TEMPLATE_HREF} />}
        />
      ) : (
        <DataTable
          data={rows as TemplateRow[]}
          columns={columns}
          idKey="id"
          isLoading={isPending}
          search={
            <ListFilterBar
              fields={SETUP_FILTER_FIELDS}
              values={setupBarValues(search)}
              onChange={(values) => onSearchChange({...fromSetupBar(values), page: undefined})}
              list="letter-templates"
              isUnfiltered={!search.name && search.status === undefined}
              label="Search letter templates"
              placeholder="Search letter templates by name, or filter by status..."
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
              title="No letter templates match your search"
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
