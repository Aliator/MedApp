import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {
  consentFormsQuery,
  useConsentFormActions,
  useSaveConsentForm,
  type ConsentForm,
} from '../../api/consent';
import {fieldStatus} from '../../api/errors';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {SetupNameCell, SetupStatusToken} from '../../components/pathways/SetupCells';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ActionsList, ActionsListItem} from '../../components/tables/ActionsList';
import {TableActions} from '../../components/tables/TableActions';
import {PanelCard} from '../../components/views/PanelCard';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {actionProblem, type PageError} from './setupList';

/** The page's query: every form, archived ones too. Its route's loader preloads the same. */
export const consentFormsPageQuery = consentFormsQuery(true);

type Dialog = {kind: 'edit'; form: ConsentForm | null} | {kind: 'delete'; form: ConsentForm} | null;

/**
 * Consent Forms (`/admin/consent-forms`, holders of ManageSetup; decision 145): the forms the centre uses for consent,
 * each a name and the version printed on it. Add, edit, archive one no longer used (records keep their copy of its name
 * and version), restore, and delete one no record has used (`canBeDeleted`). The API's refusals show in its words.
 */
export function ConsentFormsPage() {
  const announce = useAnnounce();
  const forms = useQuery(consentFormsPageQuery);
  const save = useSaveConsentForm();
  const actions = useConsentFormActions();
  const [dialog, setDialog] = useState<Dialog>(null);
  const [name, setName] = useState('');
  const [version, setVersion] = useState('');
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);
  const [error, setError] = useState<PageError>(null);
  const close = () => setDialog(null);
  const editing = dialog?.kind === 'edit' ? dialog.form : null;
  const deleting = dialog?.kind === 'delete' ? dialog.form : null;
  const items = [...(forms.data ?? []).filter((f) => !f.isArchived), ...(forms.data ?? []).filter((f) => f.isArchived)];

  const edit = (form: ConsentForm | null) => {
    setName(form?.name ?? '');
    setVersion(form?.formVersion ?? '');
    setProblem(NO_PROBLEM);
    setError(null);
    setDialog({kind: 'edit', form});
  };

  const submit = () =>
    save.mutate(
      {id: editing?.id, name: name.trim(), formVersion: version.trim()},
      {
        onSuccess: () => {
          announce(editing ? 'Consent form saved' : 'Consent form added');
          close();
        },
        onError: (e) => setProblem(saveProblem(e, editing ? 'save' : 'add', 'consent form')),
      },
    );

  const setArchived = (form: ConsentForm, archive: boolean) => {
    setError(null);
    (archive ? actions.archive : actions.restore).mutate(form.id ?? '', {
      onSuccess: () => announce(`${form.name ?? 'It'} ${archive ? 'archived' : 'restored'}`),
      onError: (e) => setError(actionProblem(e, archive ? 'archive' : 'restore', 'consent form')),
    });
  };

  const add = <Button label="Add Consent Form" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Add} />} onClick={() => edit(null)} />;

  return (
    <Page
      title="Consent Forms"
      subtitle={forms.data ? `${forms.data.filter((f) => !f.isArchived).length} active forms` : 'Loading...'}
      fill
      message={error && <AlertMessage label={error.label} message={error.message} />}
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog?.kind === 'edit'}
            title={editing ? 'Edit Consent Form' : 'Add Consent Form'}
            message={
              editing
                ? 'Consent already recorded keeps the name and version it was recorded with.'
                : 'A consent form the centre uses, with the version printed on it.'
            }
            icon={editing ? AppIcon.Edit : AppIcon.Add}
            confirmText={editing ? 'Save' : 'Add Consent Form'}
            confirmDisabled={!name.trim() || !version.trim()}
            isProcessing={save.isPending}
            hasSaveShortcut
            onConfirm={submit}
            onCancel={close}>
            <VStack gap={2}>
              {problem.validation && <ValidationAlert errors={problem.validation} fields={['name', 'formVersion']} />}
              {problem.message && <AlertMessage label={editing ? 'Not saved' : 'Not added'} message={problem.message} />}
              <TextInput
                label="Name"
                value={name}
                onChange={setName}
                placeholder="e.g. Consent for a skin procedure"
                autoComplete="off"
                isRequired
                hasAutoFocus
                status={fieldStatus(problem.validation, 'name')}
              />
              <TextInput
                label="Version"
                description="As printed on the form."
                value={version}
                onChange={setVersion}
                placeholder="e.g. 3"
                autoComplete="off"
                isRequired
                status={fieldStatus(problem.validation, 'formVersion')}
              />
            </VStack>
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog?.kind === 'delete'}
            title="Confirm Delete"
            message="No consent has been recorded on this form. This action cannot be undone."
            isDanger
            confirmText="Delete Consent Form"
            isProcessing={actions.remove.isPending}
            onConfirm={() =>
              deleting &&
              actions.remove.mutate(deleting.id ?? '', {
                onSuccess: () => {
                  announce('Consent form deleted');
                  close();
                },
                onError: (e) => {
                  setError(actionProblem(e, 'delete', 'consent form'));
                  close();
                },
              })
            }
            onCancel={close}>
            {deleting && (
              <MetadataList>
                <MetadataListItem label="Consent form">{deleting.name}</MetadataListItem>
                <MetadataListItem label="Version">{deleting.formVersion}</MetadataListItem>
              </MetadataList>
            )}
          </ConfirmDialog>
        </>
      }>
      <PanelCard title="Consent Forms" isBodyPadded={false} footerActions={add}>
        {forms.isError ? (
          <VStack padding={6}>
            <AlertMessage label="Consent forms not loaded" message="The consent forms could not be loaded. Please try again." />
          </VStack>
        ) : forms.isPending || items.length === 0 ? (
          <VStack padding={6}>
            <Text type="body" color="secondary">
              {forms.isPending ? 'Loading consent forms...' : 'No consent forms yet. Add the forms the clinic uses.'}
            </Text>
          </VStack>
        ) : (
          <ActionsList edge="bleed" hasDividers aria-label="Consent forms">
            {items.map((form) => (
              <ActionsListItem
                key={form.id}
                label={<SetupNameCell name={form.name ?? ''} secondary={`Version ${form.formVersion ?? ''}`} />}
                actions={
                  <HStack gap={2} vAlign="center">
                    <SetupStatusToken isArchived={form.isArchived ?? false} />
                    <TableActions
                      actions={[
                        {label: `Edit ${form.name}`, icon: AppIcon.Edit, onClick: () => edit(form)},
                        form.isArchived
                          ? {label: `Restore ${form.name}`, icon: AppIcon.Restore, onClick: () => setArchived(form, false)}
                          : {label: `Archive ${form.name}`, icon: AppIcon.Archive, onClick: () => setArchived(form, true)},
                        {
                          label: form.canBeDeleted ? `Delete ${form.name}` : `${form.name} has been used, so it can be archived but not deleted`,
                          icon: AppIcon.Delete,
                          isDestructive: true,
                          isDisabled: !form.canBeDeleted,
                          onClick: () => {
                            setError(null);
                            setDialog({kind: 'delete', form});
                          },
                        },
                      ]}
                    />
                  </HStack>
                }
              />
            ))}
          </ActionsList>
        )}
      </PanelCard>
    </Page>
  );
}
