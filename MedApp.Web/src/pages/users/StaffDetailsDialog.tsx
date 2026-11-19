import {TextInput} from '@astryxdesign/core/TextInput';
import {VStack} from '@astryxdesign/core/Stack';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, problemMessage} from '../../api/errors';
import {useSetStaffDetails} from '../../api/users';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';

/** A member of staff's full name and job title, as the list has them. */
export interface StaffDetailsTarget {
  username: string;
  fullName?: string | null;
  jobTitle?: string | null;
}

export interface StaffDetailsDialogProps {
  /** Whose details, or null while closed. */
  target: StaffDetailsTarget | null;
  onClose: () => void;
  /** After a save, with what to tell the admin. */
  onSaved: (message: string) => void;
}

/**
 * A member of staff's full name and job title, as they go on letters (the Users page, holders of ManageUsers;
 * docs/centre-phases-3-5-plan.md, decision 80). Starts from what the list shows, and saves both (blank removes one).
 * The API's 400 shows on its field, and anything else in its words, with the dialog left open.
 */
export function StaffDetailsDialog({target, onClose, onSaved}: StaffDetailsDialogProps) {
  // Keyed by whose details, so each opening starts from that person's.
  return <StaffDetailsForm key={target?.username ?? ''} target={target} onClose={onClose} onSaved={onSaved} />;
}

function StaffDetailsForm({target, onClose, onSaved}: StaffDetailsDialogProps) {
  const save = useSetStaffDetails();
  const [fullName, setFullName] = useState(target?.fullName ?? '');
  const [jobTitle, setJobTitle] = useState(target?.jobTitle ?? '');
  const errors = save.error instanceof ApiValidationError ? save.error.errors : null;
  const isChanged = fullName.trim() !== (target?.fullName ?? '') || jobTitle.trim() !== (target?.jobTitle ?? '');

  const close = () => {
    save.reset();
    onClose();
  };

  return (
    <ConfirmDialog
      isOpen={target !== null}
      title="Edit Details"
      message={`${target?.username ?? ''}'s full name and job title, as they go on letters they write or approve.`}
      icon={AppIcon.StaffDetails}
      confirmText="Save Details"
      confirmDisabled={!isChanged}
      isProcessing={save.isPending}
      hasSaveShortcut
      onConfirm={() =>
        target &&
        save.mutate(
          {username: target.username, fullName, jobTitle},
          {
            onSuccess: () => {
              close();
              onSaved(`${target.username}'s details have been saved.`);
            },
          },
        )
      }
      onCancel={close}>
      <VStack gap={2}>
        {save.isError && !errors && (
          <AlertMessage
            label="Details not saved"
            message={problemMessage(save.error, `The details could not be saved for ${target?.username ?? 'them'}. Please try again.`)}
          />
        )}
        {/* Only while it's open, so its autofocus can't act in a closing dialog. */}
        {target && (
          <>
            <TextInput
              label="Full name"
              description='As it goes on letters, e.g. "Dr Sam Test".'
              value={fullName}
              onChange={setFullName}
              // Someone else's name: never one saved for this admin.
              autoComplete="off"
              isOptional
              hasAutoFocus
              status={fieldStatus(errors, 'fullName')}
            />
            <TextInput
              label="Job title"
              description='e.g. "Consultant dermatologist".'
              value={jobTitle}
              onChange={setJobTitle}
              autoComplete="off"
              isOptional
              status={fieldStatus(errors, 'jobTitle')}
            />
          </>
        )}
      </VStack>
    </ConfirmDialog>
  );
}
