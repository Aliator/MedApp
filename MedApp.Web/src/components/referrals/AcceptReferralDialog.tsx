import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {staffWithPermissionQuery} from '../../api/calendar';
import {ApiValidationError, fieldStatus, problemMessage, type ValidationErrors} from '../../api/errors';
import {pathwayTemplatesQuery, PRIORITIES, type Pathway, type PathwayTemplate} from '../../api/pathways';
import {useAcceptReferral, type AcceptReferralInput, type Referral} from '../../api/referrals';
import {useCan} from '../../auth/auth';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {RichTextField} from '../forms/RichTextField';
import {AppIcon} from '../shared/icons';
import {RichText} from '../shared/RichText';
import {TriageDialog, type TriageStep} from './TriageDialog';

/** The fields the form shows, whose 400 errors show on them. */
const FIELDS = ['templateId', 'priority', 'title', 'assigneeUserId', 'notes'] as const;

/** Where the new pathway's target date comes from: said beside the priority, and on Check and Confirm. */
export const TARGET_FROM_CLOCK =
  "The target date comes from the waiting-time clock: the team's target for the priority you choose, counted from the date received.";

/** The assignee choice that means nobody (assigning is optional, and can be done later on the pathway's page). */
const NOBODY = 'nobody';

/**
 * The templates a referral can be accepted with: its team's, not archived, with stages (a template with none
 * can't make a pathway), by name. Never ranked or suggested (decision 19): using another team's template means
 * redirecting the referral first.
 */
export function templatesFor(templates: readonly PathwayTemplate[], teamId: string | null | undefined): PathwayTemplate[] {
  return templates
    .filter((t) => t.teamId === teamId && !t.isArchived && (t.stages?.length ?? 0) > 0)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', 'en-GB', {sensitivity: 'base'}));
}

export interface AcceptReferralDialogProps {
  isOpen: boolean;
  referral: Referral;
  onCancel: () => void;
  /** Called with the new pathway once it's accepted (the page goes to it). */
  onAccepted: (pathway: Pathway) => void;
}

/**
 * Accepting a referral into a new pathway (decision 19): the template (its team's only, by name), the priority
 * (empty, with "The referrer asked for: High" beside it as plain text, so the clinician chooses rather than agrees),
 * and optionally a title (blank takes the template's name), someone to
 * assign (holders of AssignPathways only: the people who can work on pathways) and a first note. Nothing is prefilled
 * or suggested: the clinician decides and the app records. There's no target date to choose: the pathway's comes from
 * the referral's waiting-time clock, for the team's target at the priority chosen (decision 32), and the dialog
 * says so. Then Check and Confirm shows the patient, the team,
 * the template and the priority before it's sent (hazard H-41). A 400 goes back to the form with the errors on their
 * fields; a 403 or 409 (triaged by someone else meanwhile, changed, the record restricted) shows the API's words, and
 * the referral is read again. Mount it afresh for each opening (a `key`), so it starts empty.
 */
export function AcceptReferralDialog({isOpen, referral, onCancel, onAccepted}: AcceptReferralDialogProps) {
  const accept = useAcceptReferral(referral.id ?? '');
  const canAssign = useCan('AssignPathways');
  const templates = useQuery({...pathwayTemplatesQuery(), enabled: isOpen});
  const staff = useQuery({...staffWithPermissionQuery('WorkPathways'), enabled: isOpen && canAssign});
  const [step, setStep] = useState<TriageStep>('form');
  const [input, setInput] = useState({templateId: '', priority: '', title: '', assigneeUserId: '', notes: ''});
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const set = <K extends keyof typeof input>(key: K, value: (typeof input)[K]) => setInput((current) => ({...current, [key]: value}));

  const usable = templatesFor(templates.data ?? [], referral.teamId);
  const template = usable.find((t) => t.id === input.templateId);
  const people = [...(staff.data ?? [])].sort((a, b) => (a.username ?? '').localeCompare(b.username ?? ''));
  const assignee = people.find((p) => p.userId === input.assigneeUserId);
  const canContinue = Boolean(template && input.priority);

  const confirm = () => {
    setMessage(null);
    const body: AcceptReferralInput = {...input, expectedVersion: referral.version ?? 0};
    accept.mutate(body, {
      onSuccess: (pathway) => onAccepted(pathway),
      onError: (e) => {
        if (e instanceof ApiValidationError) {
          setErrors(e.errors);
          setStep('form');
          return;
        }
        setMessage(problemMessage(e, 'The referral could not be accepted. Please try again.'));
      },
    });
  };

  return (
    <TriageDialog
      isOpen={isOpen}
      title="Accept Referral"
      icon={AppIcon.Success}
      referral={referral}
      step={step}
      canContinue={canContinue}
      isProcessing={accept.isPending}
      outcome="Accepting creates the pathway for this patient from the template. The referral can't be triaged again: a wrong acceptance is corrected by cancelling the pathway."
      errors={errors}
      fields={FIELDS}
      message={message}
      onContinue={() => {
        setMessage(null);
        setStep('check');
      }}
      onBack={() => setStep('form')}
      onConfirm={confirm}
      onCancel={onCancel}
      summary={
        <>
          <MetadataListItem label="Template">{template?.name}</MetadataListItem>
          <MetadataListItem label="Priority">{input.priority}</MetadataListItem>
          <MetadataListItem label="Target date">From the waiting-time clock, for this priority</MetadataListItem>
          <MetadataListItem label="Title">{input.title.trim() || `${template?.name ?? ''} (the template's name)`}</MetadataListItem>
          {canAssign && <MetadataListItem label="Assigned to">{assignee?.username ?? 'Nobody yet'}</MetadataListItem>}
          <MetadataListItem label="First note">{input.notes.trim() ? <RichText>{input.notes}</RichText> : 'None'}</MetadataListItem>
        </>
      }>
      <VStack gap={4} hAlign="stretch">
        <AnchoredSelector
          label="Template"
          placeholder="Choose a template"
          description={`${referral.teamName}'s templates, by name.`}
          options={usable.map((t) => ({value: t.id ?? '', label: t.name ?? ''}))}
          value={input.templateId || undefined}
          onChange={(value: string) => set('templateId', value)}
          isRequired
          isDisabled={templates.isPending}
          status={fieldStatus(errors, 'templateId')}
        />
        {templates.isError && (
          <Text type="body" color="secondary">
            The templates could not be loaded. Please try again.
          </Text>
        )}
        {templates.isSuccess && usable.length === 0 && (
          <Text type="body" color="secondary">
            {`${referral.teamName} has no pathway templates yet: an admin adds them under Pathway Templates. To use another team's, redirect the referral there.`}
          </Text>
        )}
        <AnchoredSelector
          label="Priority"
          placeholder="Choose a priority"
          description={`The referrer asked for: ${referral.requestedPriority ?? 'nothing'}`}
          options={[...PRIORITIES]}
          value={input.priority || undefined}
          onChange={(value: string) => set('priority', value)}
          isRequired
          status={fieldStatus(errors, 'priority')}
        />
        <Text type="supporting" color="secondary">
          {TARGET_FROM_CLOCK}
        </Text>
        <TextInput
          label="Title"
          description="Leave it blank to use the template's name."
          value={input.title}
          onChange={(value) => set('title', value)}
          autoComplete="off"
          status={fieldStatus(errors, 'title')}
        />
        {canAssign && (
          <AnchoredSelector
            label="Assign to"
            placeholder="Nobody yet"
            description="Optional: staff can be assigned later on the pathway's page."
            options={[{value: NOBODY, label: 'Nobody yet'}, ...people.map((p) => ({value: p.userId ?? '', label: p.username ?? ''}))]}
            value={input.assigneeUserId || undefined}
            onChange={(value: string) => set('assigneeUserId', value === NOBODY ? '' : value)}
            isDisabled={staff.isPending}
            status={fieldStatus(errors, 'assigneeUserId')}
          />
        )}
        <RichTextField
          label="First note"
          description="Optional: becomes the pathway's first note, by you."
          value={input.notes}
          onChange={(value) => set('notes', value)}
          status={fieldStatus(errors, 'notes')}
        />
      </VStack>
    </TriageDialog>
  );
}
