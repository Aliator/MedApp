import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {Collapsible} from '@astryxdesign/core/Collapsible';
import {Icon} from '@astryxdesign/core/Icon';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {
  dataRequestReviewItemsQuery,
  MAX_DATA_REQUEST_REASON,
  reviewItemKindWords,
  SERIOUS_HARM_TEST,
  useDataRequestActions,
  type DataRequest,
  type DataRequestReviewItem,
} from '../../api/dataRequests';
import {allMessages, ApiValidationError, problemMessage, type ValidationErrors} from '../../api/errors';
import {formatDateTime} from '../../utils/dates';
import {DialogFrame} from '../modals/DialogFrame';
import {PatientBanner, type BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {DisabledReason} from '../shared/DisabledReason';

/** Plain text keeps the lines it was written with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/** An item's key: its kind and ID (two kinds could share an ID). */
const keyOf = (item: Pick<DataRequestReviewItem, 'kind' | 'itemId'>) => `${item.kind}:${item.itemId}`;

/** Whether an item is withheld, and why, as the clinician has it. */
interface Choice {
  isWithheld: boolean;
  reason: string;
}

/** Each item's choice as it starts: withheld (with its reason) where the last review withheld it, or it starts withheld. */
export function startingChoices(items: readonly DataRequestReviewItem[]): Record<string, Choice> {
  return Object.fromEntries(items.map((item) => [keyOf(item), {isWithheld: item.isWithheld === true, reason: item.withheldReason ?? ''}]));
}

/** What the review sends: the ticked items, each with its reason. */
export function withheldOf(items: readonly DataRequestReviewItem[], choices: Record<string, Choice>) {
  return items
    .filter((item) => choices[keyOf(item)]?.isWithheld)
    .map((item) => ({kind: item.kind ?? '', itemId: item.itemId ?? '', reason: choices[keyOf(item)].reason.trim()}));
}

export interface DataRequestReviewDialogProps {
  request: DataRequest;
  patient?: BannerPatient | null;
  onCancel: () => void;
  onDone: (request: DataRequest) => void;
}

/**
 * The review of a data request (holders of ManagePrivacy, while it's open: `canReview`; they take an appropriate health
 * professional's opinion outside MedApp): every item of the record they may withhold (notes, files, letters, results, MDT items and referrals), with its kind, date, title,
 * who wrote it, whether it's deleted, and its text folded under it. **The reviewer decides; MedApp suggests nothing:**
 * an item starts withheld only where the last review withheld it, or where the API says it starts so (a note about
 * another patient), with its reason. Ticking Withhold asks for a reason. Save sends the ticked items, replacing any
 * earlier review, from the version the page showed; a 400 shows its messages, a 409 or 403 the API's words. Mount it
 * only while open: the items' text is read when it opens and not kept after.
 */
export function DataRequestReviewDialog({request, patient, onCancel, onDone}: DataRequestReviewDialogProps) {
  const id = request.id ?? '';
  const items = useQuery(dataRequestReviewItemsQuery(id));
  const review = useDataRequestActions(id).review;
  const [choices, setChoices] = useState<Record<string, Choice> | null>(null);
  const [errors, setErrors] = useState<ValidationErrors | null>(null);
  const [error, setError] = useState<string | null>(null);
  const list = items.data ?? [];
  const current = choices ?? startingChoices(list);
  const withheld = withheldOf(list, current);
  const isMissingReason = withheld.some((item) => !item.reason);
  const isSaving = review.isPending;

  const choose = (item: DataRequestReviewItem, change: Partial<Choice>) =>
    setChoices({...current, [keyOf(item)]: {...(current[keyOf(item)] ?? {isWithheld: false, reason: ''}), ...change}});

  const save = () => {
    setErrors(null);
    setError(null);
    review.mutate(
      {withheld, expectedVersion: request.version ?? 0},
      {
        onSuccess: onDone,
        onError: (e) => {
          if (e instanceof ApiValidationError) setErrors(e.errors);
          else setError(problemMessage(e, 'The review could not be saved. Please try again.'));
        },
      },
    );
  };

  const count = withheld.length;
  return (
    <DialogFrame
      isOpen
      title="Review Data Request"
      icon={<Icon icon={AppIcon.SignOff} size="lg" color="accent" />}
      onClose={onCancel}
      width={720}
      purpose={isSaving ? 'required' : 'form'}
      footerStart={
        items.isSuccess && (
          <Text type="supporting" color="secondary">
            {count === 0 ? 'Nothing withheld' : `${count} withheld`}
          </Text>
        )
      }
      footer={
        <>
          <Button label="Cancel" variant="secondary" isDisabled={isSaving} onClick={onCancel} />
          <DisabledReason reason={isMissingReason ? 'Give a reason for each item withheld.' : undefined}>
            <Button
              label="Save Review"
              variant="primary"
              icon={<Icon icon={AppIcon.Save} />}
              isLoading={isSaving}
              isDisabled={!items.isSuccess || isMissingReason}
              onClick={save}
            />
          </DisabledReason>
        </>
      }>
      <VStack gap={4} hAlign="stretch">
        {patient && <PatientBanner patient={patient} isCompact />}
        <Text type="body">{SERIOUS_HARM_TEST}</Text>
        {errors && <AlertMessage label="Check the review" message={errors.title} details={allMessages(errors)} />}
        {error && <AlertMessage label="Review not saved" message={error} />}
        {items.isPending ? (
          <HStack gap={2} vAlign="center">
            <Spinner size="sm" aria-hidden />
            <Text type="body" color="secondary">
              Loading the record...
            </Text>
          </HStack>
        ) : items.isError ? (
          <AlertMessage label="Items not loaded" message={problemMessage(items.error, "The record's items could not be loaded. Please try again.")} />
        ) : list.length === 0 ? (
          <Text type="body" color="secondary">
            Nothing in the record could be withheld: there are no notes, files, letters, results, MDT items or referrals.
          </Text>
        ) : (
          list.map((item) => (
            <ReviewItem
              key={keyOf(item)}
              item={item}
              choice={current[keyOf(item)] ?? {isWithheld: false, reason: ''}}
              isDisabled={isSaving}
              onChange={(change) => choose(item, change)}
            />
          ))
        )}
      </VStack>
    </DialogFrame>
  );
}

/** One item: what it is, its text folded under it, and Withhold with its reason. */
function ReviewItem({
  item,
  choice,
  isDisabled,
  onChange,
}: {
  item: DataRequestReviewItem;
  choice: Choice;
  isDisabled: boolean;
  onChange: (change: Partial<Choice>) => void;
}) {
  const kind = reviewItemKindWords(item.kind);
  const at = item.atUtc ? formatDateTime(item.atUtc) : '';
  const facts = [at, item.by ? `by ${item.by}` : ''].filter(Boolean).join(', ');
  return (
    <Card padding={4} width="100%">
      <VStack gap={3} hAlign="stretch" role="group" aria-label={`${kind}: ${item.title}${at ? `, ${at}` : ''}`}>
        <VStack gap={1} hAlign="start">
          <HStack gap={2} vAlign="center" wrap="wrap">
            <Token size="sm" label={kind} color="gray" />
            {item.isDeleted && <Token size="sm" label="Deleted" color="orange" />}
            <Text type="body" weight="semibold">
              {item.title}
            </Text>
          </HStack>
          {facts && (
            <Text type="supporting" color="secondary">
              {facts}
            </Text>
          )}
        </VStack>
        {item.text ? (
          <Collapsible
            chevronPosition="end"
            trigger={
              <Text type="body" weight="semibold">
                Its text
              </Text>
            }>
            <VStack gap={0} paddingBlockStart={2}>
              <Text type="body" style={PRESERVE_LINES}>
                {item.text}
              </Text>
            </VStack>
          </Collapsible>
        ) : (
          <Text type="supporting" color="secondary">
            No text held.
          </Text>
        )}
        <CheckboxInput
          label={`Withhold this ${kind.toLowerCase()}`}
          value={choice.isWithheld}
          onChange={(isWithheld) => onChange({isWithheld})}
          isDisabled={isDisabled}
        />
        {choice.isWithheld && (
          <TextArea
            label="Why it's withheld"
            description="The serious harm it would be likely to cause, or the third party it would identify."
            value={choice.reason}
            onChange={(reason) => onChange({reason})}
            rows={2}
            maxLength={MAX_DATA_REQUEST_REASON}
            isRequired
            isDisabled={isDisabled}
          />
        )}
      </VStack>
    </Card>
  );
}
