import {Card} from '@astryxdesign/core/Card';
import {DropdownMenu} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Grid} from '@astryxdesign/core/Grid';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {
  emptyRecipient,
  MAX_RECIPIENTS,
  PATIENTS_UNLISTED_PRACTICE,
  RECIPIENT_KINDS,
  recipientKindWords,
  type RecipientInput,
  type RecipientKind,
} from '../../api/letters';
import {practiceChoices, practiceLabel, practicesQuery} from '../../api/practices';
import {AnchoredSelector} from '../forms/AnchoredSelector';
import {AppIcon} from '../shared/icons';

/** The GP practice choice for the patient's own practice that isn't in the directory. */
export const UNLISTED_PRACTICE_LABEL = "The patient's practice (not in the directory)";

/** "Addressed to" for the first recipient, "Copy" for the rest. */
export const recipientRole = (index: number) => (index === 0 ? 'Addressed to' : 'Copy');

export interface RecipientsEditorProps {
  recipients: readonly RecipientInput[];
  onChange: (recipients: RecipientInput[]) => void;
  /** The letter is about a referral, so its referrer can be a recipient. */
  hasReferral: boolean;
  isDisabled?: boolean;
}

/**
 * A draft's recipients, in order (decision 101): the first is the one it's addressed to, the rest get copies. Each can
 * be moved up or down, or removed, and Add Recipient adds a GP practice (chosen from the directory, or the patient's own
 * practice that isn't listed, filled from their record), the patient, the
 * referrer (for a letter about a referral) or someone else (a name and address typed, with an optional secure email
 * address). At most ten. Nothing is saved until the letter is; the API checks each recipient, and its words show in the
 * page's message.
 */
export function RecipientsEditor({recipients, onChange, hasReferral, isDisabled = false}: RecipientsEditorProps) {
  const practices = useQuery(practicesQuery(false));
  const update = (index: number, change: Partial<RecipientInput>) =>
    onChange(recipients.map((recipient, i) => (i === index ? {...recipient, ...change} : recipient)));
  const move = (index: number, by: -1 | 1) => {
    const next = [...recipients];
    [next[index], next[index + by]] = [next[index + by], next[index]];
    onChange(next);
  };
  const remove = (index: number) => onChange(recipients.filter((_, i) => i !== index));
  const kinds = RECIPIENT_KINDS.filter((kind) => kind.value !== 'Referrer' || hasReferral);
  const isFull = recipients.length >= MAX_RECIPIENTS;

  return (
    <VStack gap={3} hAlign="stretch">
      {recipients.length === 0 && (
        <Text type="body" color="secondary">
          No recipients yet. A letter needs at least one before it can be submitted.
        </Text>
      )}
      {recipients.map((recipient, index) => {
        const name = `${recipientRole(index)}: ${recipientKindWords(recipient.kind)}`;
        return (
          <Card key={recipient.key} variant="muted" padding={4} role="group" aria-label={`Recipient ${index + 1}, ${name}`}>
            <VStack gap={3} hAlign="stretch">
              <HStack gap={2} vAlign="center">
                <StackItem size="fill">
                  <Text type="body" weight="semibold">
                    {name}
                  </Text>
                </StackItem>
                <IconButton
                  label={`Move recipient ${index + 1} up`}
                  tooltip="Move up"
                  icon={<Icon icon={AppIcon.ChevronUp} />}
                  variant="ghost"
                  size="sm"
                  isDisabled={isDisabled || index === 0}
                  onClick={() => move(index, -1)}
                />
                <IconButton
                  label={`Move recipient ${index + 1} down`}
                  tooltip="Move down"
                  icon={<Icon icon={AppIcon.ChevronDown} />}
                  variant="ghost"
                  size="sm"
                  isDisabled={isDisabled || index === recipients.length - 1}
                  onClick={() => move(index, 1)}
                />
                <IconButton
                  label={`Remove recipient ${index + 1}`}
                  tooltip="Remove"
                  icon={<Icon icon={AppIcon.Delete} />}
                  variant="ghost"
                  size="sm"
                  isDisabled={isDisabled}
                  onClick={() => remove(index)}
                />
              </HStack>
              <RecipientFields
                recipient={recipient}
                practices={[
                  ...practiceChoices(practices.data ?? [], recipient.practiceId).map((p) => ({value: p.id ?? '', label: practiceLabel(p)})),
                  {value: PATIENTS_UNLISTED_PRACTICE, label: UNLISTED_PRACTICE_LABEL},
                ]}
                isPracticesLoading={practices.isPending}
                isDisabled={isDisabled}
                onChange={(change) => update(index, change)}
              />
            </VStack>
          </Card>
        );
      })}
      <HStack>
        <DropdownMenu
          items={kinds.map((kind) => ({
            id: kind.value,
            label: kind.label,
            onClick: () => onChange([...recipients, emptyRecipient(kind.value as RecipientKind)]),
          }))}
          button={{
            label: 'Add Recipient',
            variant: 'secondary',
            icon: <Icon icon={AppIcon.Add} />,
            isDisabled: isDisabled || isFull,
          }}
        />
      </HStack>
      {isFull && (
        <Text type="supporting" color="secondary">
          {`A letter can have at most ${MAX_RECIPIENTS} recipients.`}
        </Text>
      )}
    </VStack>
  );
}

interface RecipientFieldsProps {
  recipient: RecipientInput;
  practices: {value: string; label: string}[];
  isPracticesLoading: boolean;
  isDisabled: boolean;
  onChange: (change: Partial<RecipientInput>) => void;
}

/** What a recipient of each kind needs: a practice, nothing (the patient), an address, or a name and address. */
function RecipientFields({recipient, practices, isPracticesLoading, isDisabled, onChange}: RecipientFieldsProps) {
  switch (recipient.kind) {
    case 'GpPractice':
      return (
        <AnchoredSelector
          label="Practice"
          placeholder="Choose a practice from the directory"
          options={practices}
          value={recipient.practiceId || undefined}
          onChange={(practiceId: string) => onChange({practiceId})}
          isRequired
          isDisabled={isDisabled || isPracticesLoading}
        />
      );
    case 'Patient':
      return (
        <Text type="body" color="secondary">
          {recipient.name ? `${recipient.name}, at their address on record.` : 'The patient, at their address on record.'}
        </Text>
      );
    case 'Referrer':
      // From the letter's referral: its practice from the directory, or, when it names none, the address typed here.
      return recipient.practiceId ? (
        <Text type="body" color="secondary">
          {recipient.name ? `${recipient.name}, from the directory.` : "The referral's practice, from the directory."}
        </Text>
      ) : (
        <AddressFields recipient={recipient} isDisabled={isDisabled} onChange={onChange} description="The referral names no practice from the directory, so type its address." />
      );
    default:
      return (
        <VStack gap={3} hAlign="stretch">
          <TextInput label="Name" value={recipient.name} onChange={(name) => onChange({name})} isRequired isDisabled={isDisabled} autoComplete="off" />
          <AddressFields recipient={recipient} isDisabled={isDisabled} onChange={onChange} isRequired />
          <TextInput
            label="Secure email"
            description="Only an address in a domain allowed for secure email (such as nhs.net), to send it from MedApp."
            value={recipient.email}
            onChange={(email) => onChange({email})}
            type="email"
            isOptional
            isDisabled={isDisabled}
            autoComplete="off"
          />
        </VStack>
      );
  }
}

/** Up to three address lines and a postcode. */
function AddressFields({
  recipient,
  isDisabled,
  onChange,
  isRequired = false,
  description,
}: {
  recipient: RecipientInput;
  isDisabled: boolean;
  onChange: (change: Partial<RecipientInput>) => void;
  isRequired?: boolean;
  description?: string;
}) {
  return (
    <Grid columns={2} gap={3}>
      <TextArea
        label="Address"
        description={description ?? 'Up to three lines.'}
        value={recipient.addressLines}
        onChange={(addressLines) => onChange({addressLines})}
        rows={3}
        isRequired={isRequired}
        isOptional={!isRequired}
        isDisabled={isDisabled}
      />
      <TextInput label="Postcode" value={recipient.postcode} onChange={(postcode) => onChange({postcode})} isOptional isDisabled={isDisabled} autoComplete="off" />
    </Grid>
  );
}
