import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {ApiValidationError, fieldStatus, otherMessages, problemMessage, type ValidationErrors} from '../../api/errors';
import {compareLetterTemplates, letterTemplatesQuery, letterTemplateTeam, type LetterTemplate} from '../../api/letterTemplates';
import {letterKindWords, REFERRAL_LETTER_KINDS, useCreateLetter, type Letter} from '../../api/letters';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';

/** What a letter is written about: one of a patient's pathways, or one of their referrals. */
export type LetterAbout = {pathwayId: string; referralId?: undefined} | {referralId: string; pathwayId?: undefined};

/**
 * The templates a letter about this can start from: not archived, for its team or every team, and from a
 * referral only replies to it (a referral response or a request for more information), in the API's order (every
 * team's first, then by name). Never ranked or suggested (decision 69).
 */
export function templatesForLetter(templates: readonly LetterTemplate[], teamId: string | null | undefined, about: LetterAbout): LetterTemplate[] {
  return templates
    .filter((t) => !t.isArchived)
    .filter((t) => !t.teamId || t.teamId === teamId)
    .filter((t) => !about.referralId || REFERRAL_LETTER_KINDS.includes(t.kind ?? ''))
    .sort(compareLetterTemplates);
}

export interface WriteLetterDialogProps {
  about: LetterAbout;
  /** The pathway's or referral's team: its templates, and those for every team, are offered. */
  teamId: string | null | undefined;
  /** Who the letter is about, as the banner shows them. */
  patient?: BannerPatient | null;
  onCancel: () => void;
  /** The new draft, as the API returned it. */
  onCreated: (letter: Letter) => void;
}

/**
 * Write Letter (decision 100): choose a template, then the draft is made (copying its text, with the patient's
 * practice and the patient as recipients) and its page opens. The patient shows as `PatientBanner` does (compact),
 * so it's started on the right one. Nothing is chosen to begin with. A refusal shows in the dialog in the API's words.
 * Mount it only while open.
 */
export function WriteLetterDialog({about, teamId, patient, onCancel, onCreated}: WriteLetterDialogProps) {
  const templates = useQuery(letterTemplatesQuery(false));
  const choices = templatesForLetter(templates.data ?? [], teamId, about);
  const create = useCreateLetter();
  const [templateId, setTemplateId] = useState('');
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [error, setError] = useState<string | null>(null);

  const confirm = () => {
    setErrors(null);
    setError(null);
    create.mutate(
      {templateId, ...(about.pathwayId ? {pathwayId: about.pathwayId} : {referralId: about.referralId})},
      {
        onSuccess: onCreated,
        onError: (e) => {
          if (e instanceof ApiValidationError) {
            setErrors(e.errors);
            setError(otherMessages(e.errors, ['templateId']).join(' ') || null);
          } else setError(problemMessage(e, 'The letter could not be started. Please try again.'));
        },
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen
      title="Write Letter"
      message={
        about.referralId
          ? 'Choose a template: a reply to the referral, or a request for more information. The letter starts as your draft.'
          : 'Choose a template. The letter starts as your draft, addressed to the patient\'s practice with a copy to the patient.'
      }
      icon={AppIcon.Letters}
      confirmText="Start Letter"
      confirmDisabled={!templateId}
      isProcessing={create.isPending}
      onConfirm={confirm}
      onCancel={onCancel}>
      <VStack gap={4} hAlign="stretch">
        {patient && <PatientBanner patient={patient} isCompact />}
        <AnchoredSelector
          label="Template"
          placeholder={templates.isPending ? 'Loading...' : 'Choose a template'}
          options={choices.map((t) => ({
            value: t.id ?? '',
            label: t.name ?? '',
            description: `${letterKindWords(t.kind)} · ${letterTemplateTeam(t)}`,
          }))}
          value={templateId || undefined}
          onChange={(value: string) => setTemplateId(value)}
          isRequired
          isDisabled={templates.isPending || create.isPending}
          status={fieldStatus(errors, 'templateId')}
        />
        {templates.isSuccess && choices.length === 0 && (
          <Text type="body" color="secondary">
            {about.referralId
              ? 'There are no templates for replies to referrals in this team. An admin adds them in Letter Templates.'
              : 'There are no letter templates for this team. An admin adds them in Letter Templates.'}
          </Text>
        )}
        {templates.isError && <AlertMessage label="Templates not loaded" />}
        {error && <AlertMessage label="Letter not started" message={error} />}
      </VStack>
    </ConfirmDialog>
  );
}
