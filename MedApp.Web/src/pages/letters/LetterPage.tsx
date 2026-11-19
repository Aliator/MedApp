import {Button} from '@astryxdesign/core/Button';
import {CheckboxInput} from '@astryxdesign/core/CheckboxInput';
import {DropdownMenu, type DropdownMenuItemData} from '@astryxdesign/core/DropdownMenu';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextArea} from '@astryxdesign/core/TextArea';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useRef, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {attachmentContentUrl} from '../../api/attachments';
import {ApiError, ApiValidationError, fieldStatus, otherMessages, problemMessage, type ValidationErrors} from '../../api/errors';
import {letterMergeFieldsQuery} from '../../api/letterTemplates';
import {
  isDraftChanged,
  letterDraftInput,
  letterHref,
  letterKindWords,
  letterPreviewUrl,
  letterQuery,
  letterStatusWords,
  MAX_LETTER_BODY,
  useLetterActions,
  type Letter,
  type LetterDraftInput,
  type LetterRecipient,
} from '../../api/letters';
import {DownloadLink} from '../../components/attachments/Attachments';
import {useSaveShortcut} from '../../components/forms/SaveShortcut';
import {LetterStatusToken} from '../../components/letters/LetterCells';
import {
  ApproveLetterDialog,
  DiscardLetterDialog,
  RecordSentDialog,
  ReturnLetterDialog,
  SendSecureEmailDialog,
  SubmitLetterDialog,
  WithdrawLetterDialog,
} from '../../components/letters/LetterDialogs';
import {RecipientList} from '../../components/letters/RecipientList';
import {RecipientsEditor} from '../../components/letters/RecipientsEditor';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {patientDetailsSection} from '../../components/patients/DetailsPatient';
import {PatientFlagBanners} from '../../components/patients/PatientFlagBanners';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {RichText} from '../../components/shared/RichText';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {FoldingCard, type FoldingSection} from '../../components/views/FoldingCard';
import {formatDateTime} from '../../utils/dates';

/** The dialog open, if any: a letter's, or a recipient's. */
type Dialog =
  | {kind: 'submit' | 'approve' | 'return' | 'discard' | 'withdraw'}
  | {kind: 'send' | 'recordSent'; recipient: LetterRecipient}
  | null;

/** What's announced when each is done. */
const DONE: Record<NonNullable<Dialog>['kind'], string> = {
  submit: 'Letter submitted for approval',
  approve: 'Letter approved',
  return: 'Letter returned to its author',
  discard: 'Draft discarded',
  withdraw: 'Letter withdrawn',
  send: 'Letter sent by secure email',
  recordSent: 'Letter recorded as sent',
};

/** The headings a body has (its Markdown `#` lines), in order: what the template gave it to fill in. */
export function bodyHeadings(body: string): string[] {
  return body
    .split('\n')
    .map((line) => /^#{1,6}\s+(.+?)\s*#*\s*$/.exec(line.trim())?.[1])
    .filter((heading): heading is string => Boolean(heading));
}

/** The draft being edited, and the version it started from (sent as `expectedVersion`, so another tab's save is a 409). */
interface Draft {
  version: number;
  input: LetterDraftInput;
}

/** The fields of a recipient, whose errors show with the recipients (a 400 names them without saying which). */
const RECIPIENT_FIELDS = ['recipients', 'kind', 'practiceId', 'name', 'addressLines', 'postcode', 'email'];
/** The letter's own fields, whose errors show on them. */
const TEXT_FIELDS = ['letterhead', 'body', 'closing', 'largePrint'];

/** Plain text keeps the lines it was typed with. */
const PRESERVE_LINES = {whiteSpace: 'pre-wrap'} as const;

/**
 * One letter (`/letters/$id`, decisions 100 to 105): Details (the patient first, never folding) above the letter and,
 * beside them, its recipients (one card of folding sections in a narrow window); the patient's flags in the message.
 *
 * - **A draft its author can edit** (`canEdit`): the letterhead, the body (Markdown, with the template's headings to
 *   fill in) and closing, large print, and the recipients (`RecipientsEditor`), saved together with Save (or Ctrl+S)
 *   from the version it started from; a 409 (another tab saved first) shows the API's words with Reload. Merge fields
 *   with no value for the patient are listed: approval is refused until they have one or are removed.
 * - **Its actions follow the response's flags only:** Download Preview (`canPreview`), Submit for Approval
 *   (`canSubmit`, choosing an approver), Approve (`canApprove`, after a confirmation showing the patient and every
 *   recipient with their address: hazard H-48), Return (`canReturn`, with a comment), Discard Draft (`canDiscard`),
 *   Withdraw (`canWithdraw`, listing who it was already sent to) and Write a Correction (`canWriteCorrection`, which
 *   opens the new draft). Submitting and approving wait until changes are saved.
 * - **Once approved:** Download PDF, and each recipient's dispatch, with Send by Secure Email or Record as Sent.
 */
export function LetterPage({id}: {id: string}) {
  const isNarrow = useIsNarrow();
  const announce = useAnnounce();
  const navigate = useNavigate();
  const {data: letter, error, isPending, refetch} = useQuery(letterQuery(id));
  const mergeFields = useQuery({...letterMergeFieldsQuery, enabled: (letter?.missingMergeFields?.length ?? 0) > 0});
  const actions = useLetterActions(id);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [dialog, setDialog] = useState<Dialog>(null);
  const [saveErrors, setSaveErrors] = useState<ValidationErrors | null>(null);
  // What failed (the headline) and the API's words.
  const [actionError, setActionError] = useState<{label: string; message: string} | null>(null);
  const [open, setOpen] = useState({letter: true, recipients: true});
  const toggle = (part: keyof typeof open) => setOpen((current) => ({...current, [part]: !current[part]}));
  const formRef = useRef<HTMLElement>(null);
  const isMissing = error instanceof ApiError && error.status === 404;

  const input = letter ? (draft?.input ?? letterDraftInput(letter)) : null;
  const isChanged = letter && input && letter.canEdit ? isDraftChanged(letter, input) : false;
  const setInput = (change: Partial<LetterDraftInput>) => {
    if (!letter || !input) return;
    setDraft({version: draft?.version ?? letter.version ?? 0, input: {...input, ...change}});
  };

  const save = () => {
    if (!letter?.canEdit || !input || !isChanged || actions.save.isPending) return false;
    setSaveErrors(null);
    setActionError(null);
    actions.save.mutate(
      {expectedVersion: draft?.version ?? letter.version ?? 0, input},
      {
        onSuccess: () => {
          setDraft(null);
          announce('Letter saved');
        },
        onError: (e) => {
          if (e instanceof ApiValidationError) setSaveErrors(e.errors);
          else setActionError({label: 'Letter not saved', message: problemMessage(e, 'The letter could not be saved. Please try again.')});
        },
      },
    );
  };
  useSaveShortcut(formRef, letter?.canEdit ? save : undefined);

  const reload = () => {
    setActionError(null);
    setSaveErrors(null);
    setDraft(null);
    void refetch();
  };

  const writeCorrection = () => {
    setActionError(null);
    actions.correction.mutate(undefined, {
      onSuccess: (correction) => {
        announce('Correction started');
        void navigate({to: '/letters/$id', params: {id: correction.id ?? ''}});
      },
      onError: (e) =>
        setActionError({label: 'Correction not started', message: problemMessage(e, 'The correction could not be started. Please try again.')}),
    });
  };

  const done = (kind: NonNullable<Dialog>['kind']) => () => {
    announce(DONE[kind]);
    setDialog(null);
    if (kind === 'discard') void navigate({to: '/letters', replace: true});
  };

  const flags = letter?.patientFlags;
  const hasFlags = Boolean(flags && (flags.isRestricted || flags.isDisputed || flags.isAnonymised));
  const title = letter ? letterKindWords(letter.kind) : 'Letter';

  return (
    <Page
      title={title}
      subtitle={letter ? `${letter.templateName} · ${letterStatusWords(letter)}` : undefined}
      backHref="/letters"
      backLabel="Back to letters"
      isLoading={isPending}
      loadingMessage="Loading letter..."
      fill={letter !== undefined}
      // No banner: the patient is at the top of Details, which never folds and comes first (as on the pathway page).
      message={
        isMissing ? (
          <AlertMessage label="Letter not found" message="It may have been discarded." />
        ) : error ? (
          <AlertMessage label="Letter not loaded" />
        ) : actionError ? (
          <VStack gap={2} hAlign="stretch">
            <AlertMessage label={actionError.label} message={actionError.message} onDismiss={() => setActionError(null)} />
            <HStack>
              <Button label="Reload" variant="secondary" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={reload} />
            </HStack>
          </VStack>
        ) : hasFlags && flags ? (
          <PatientFlagBanners patient={flags} />
        ) : (
          letter?.readOnlyReason && <SummaryAlert status="info" label="Read-only letter" message={letter.readOnlyReason} />
        )
      }
      actions={letter && headerActions(letter)}
      dialogs={letter && dialogs(letter)}>
      {letter &&
        (isNarrow ? (
          <FoldingCard sections={[detailsSection(letter), letterSection(letter), recipientsSection(letter)]} footerActions={footer(letter)} />
        ) : (
          <TwoColumns
            start={<FoldingCard sections={[detailsSection(letter), letterSection(letter)]} footerActions={footer(letter)} />}
            end={<FoldingCard sections={[recipientsSection(letter)]} />}
          />
        ))}
    </Page>
  );

  /** Save, for the author's draft, with whether there are changes to save. */
  function footer(letter: Letter) {
    if (!letter.canEdit) return undefined;
    return (
      <>
        {isChanged && (
          <Text type="supporting" color="secondary">
            Unsaved changes
          </Text>
        )}
        <Button
          label="Save"
          variant="primary"
          icon={<Icon icon={AppIcon.Save} />}
          onClick={save}
          isLoading={actions.save.isPending}
          isDisabled={!isChanged}
        />
      </>
    );
  }

  function headerActions(letter: Letter) {
    const waitsForSave = isChanged ? 'Save your changes first.' : undefined;
    const isDraftLike = letter.status === 'Draft' || letter.status === 'AwaitingApproval';
    const more: DropdownMenuItemData[] = [
      ...(letter.canDiscard
        ? [{id: 'discard', label: 'Discard Draft', icon: AppIcon.Delete, variant: 'destructive' as const, onClick: () => setDialog({kind: 'discard'})}]
        : []),
      ...(letter.canWithdraw
        ? [
            {
              id: 'withdraw',
              label: 'Withdraw',
              description: 'An error was found after approval',
              icon: AppIcon.Withdraw,
              variant: 'destructive' as const,
              onClick: () => setDialog({kind: 'withdraw'}),
            },
          ]
        : []),
    ];
    return (
      <>
        {letter.canPreview && isDraftLike && (
          <Button label="Download Preview" variant="secondary" icon={<Icon icon={AppIcon.FilePdf} />} href={letterPreviewUrl(letter.id ?? id)} as={DownloadLink} />
        )}
        {letter.pdfAttachmentId && (
          <Button label="Download PDF" variant="secondary" icon={<Icon icon={AppIcon.Download} />} href={attachmentContentUrl(letter.pdfAttachmentId)} as={DownloadLink} />
        )}
        {letter.canSubmit && (
          <Button
            label="Submit for Approval"
            variant={letter.canApprove ? 'secondary' : 'primary'}
            icon={<Icon icon={AppIcon.Send} />}
            isDisabled={Boolean(waitsForSave)}
            tooltip={waitsForSave}
            onClick={() => setDialog({kind: 'submit'})}
          />
        )}
        {letter.canApprove && (
          <Button
            label="Approve"
            variant="primary"
            icon={<Icon icon={AppIcon.Success} />}
            isDisabled={Boolean(waitsForSave)}
            tooltip={waitsForSave}
            onClick={() => setDialog({kind: 'approve'})}
          />
        )}
        {letter.canReturn && <Button label="Return" variant="secondary" icon={<Icon icon={AppIcon.Back} />} onClick={() => setDialog({kind: 'return'})} />}
        {letter.canWriteCorrection && (
          <Button
            label="Write a Correction"
            variant="primary"
            icon={<Icon icon={AppIcon.Edit} />}
            isLoading={actions.correction.isPending}
            onClick={writeCorrection}
          />
        )}
        {more.length > 0 && <DropdownMenu items={more} alignment="end" button={{label: 'More', variant: 'secondary', icon: <Icon icon={AppIcon.Menu} />}} />}
      </>
    );
  }

  function dialogs(letter: Letter) {
    if (!dialog) return null;
    const props = {letter, onCancel: () => setDialog(null), onDone: done(dialog.kind)};
    switch (dialog.kind) {
      case 'submit':
        return <SubmitLetterDialog {...props} />;
      case 'approve':
        return <ApproveLetterDialog {...props} />;
      case 'return':
        return <ReturnLetterDialog {...props} />;
      case 'discard':
        return <DiscardLetterDialog {...props} />;
      case 'withdraw':
        return <WithdrawLetterDialog {...props} />;
      case 'send':
        return <SendSecureEmailDialog {...props} recipient={dialog.recipient} />;
      default:
        return <RecordSentDialog {...props} recipient={dialog.recipient} />;
    }
  }

  /** The letter itself: the editor for its author's draft, otherwise its text as written. */
  function letterSection(letter: Letter): FoldingSection {
    return {
      key: 'letter',
      title: 'Letter',
      isOpen: open.letter,
      onToggle: () => toggle('letter'),
      children: (
        <Section variant="transparent" padding={6} ref={formRef}>
          <VStack gap={4} hAlign="stretch">
            {letter.isReturned && letter.returnComment && (
              <AlertMessage
                status="warning"
                label="Returned for changes"
                message={`Returned by ${letter.returnedByUsername ?? 'the approver'}${letter.returnedAtUtc ? ` on ${formatDateTime(letter.returnedAtUtc)}` : ''}: ${letter.returnComment}`}
              />
            )}
            <MissingMergeFields letter={letter} labels={mergeFields.data} />
            {letter.canEdit && input ? editor(input) : <LetterText letter={letter} />}
          </VStack>
        </Section>
      ),
    };
  }

  function editor(input: LetterDraftInput) {
    const headings = bodyHeadings(input.body);
    const isSaving = actions.save.isPending;
    const others = otherMessages(saveErrors, [...TEXT_FIELDS, ...RECIPIENT_FIELDS]);
    return (
      <VStack gap={4} hAlign="stretch">
        {others.length > 0 && <AlertMessage label="Letter not saved" message={saveErrors?.title ?? 'The letter could not be saved.'} details={others} />}
        <TextArea
          label="Letterhead"
          description="The centre's name, address and phone, as plain lines."
          value={input.letterhead}
          onChange={(letterhead) => setInput({letterhead})}
          rows={4}
          isDisabled={isSaving}
          status={fieldStatus(saveErrors, 'letterhead')}
        />
        <TextArea
          label="Body"
          description={
            headings.length > 0
              ? `Start a line with # for a heading; wrap words in ** for bold. Write under each heading: ${headings.join(', ')}.`
              : 'Start a line with # for a heading; wrap words in ** for bold. Fields in {{ }}, such as {{patient.name}}, are filled in when it is approved.'
          }
          value={input.body}
          onChange={(body) => setInput({body})}
          rows={18}
          maxLength={MAX_LETTER_BODY}
          isRequired
          isDisabled={isSaving}
          status={fieldStatus(saveErrors, 'body')}
        />
        <TextArea
          label="Closing"
          description="The sign-off, as plain lines."
          value={input.closing}
          onChange={(closing) => setInput({closing})}
          rows={4}
          isDisabled={isSaving}
          status={fieldStatus(saveErrors, 'closing')}
        />
        <CheckboxInput
          label="Large print"
          description="16 point throughout, for someone who needs it (the Accessible Information Standard)."
          value={input.largePrint}
          onChange={(largePrint) => setInput({largePrint})}
          isDisabled={isSaving}
        />
      </VStack>
    );
  }

  function detailsSection(letter: Letter): FoldingSection {
    return patientDetailsSection({patientId: letter.patientId, patient: letter.patient, patientName: letter.patientName}, <LetterDetails letter={letter} />);
  }

  function recipientsSection(letter: Letter): FoldingSection {
    const isApproved = Boolean(letter.approvedAtUtc) || letter.status === 'Approved';
    const recipientErrors = RECIPIENT_FIELDS.flatMap((field) => saveErrors?.fields[field] ?? []);
    return {
      key: 'recipients',
      title: 'Recipients',
      isOpen: open.recipients,
      onToggle: () => toggle('recipients'),
      children: (
        <Section variant="transparent" padding={6}>
          {letter.canEdit && input ? (
            <VStack gap={3} hAlign="stretch">
              {recipientErrors.length > 0 && <AlertMessage label="Check the recipients" details={recipientErrors} />}
              <RecipientsEditor
                recipients={input.recipients}
                onChange={(recipients) => setInput({recipients})}
                hasReferral={Boolean(letter.referralId)}
                isDisabled={actions.save.isPending}
              />
            </VStack>
          ) : (
            <VStack gap={3} hAlign="stretch">
              {isApproved && letter.status === 'Approved' && !letter.secureEmailAvailable && (
                <Text type="body" color="secondary">
                  Secure email from MedApp isn't set up here: send each from the PDF, then record how.
                </Text>
              )}
              <RecipientList
                recipients={letter.recipients ?? []}
                showsDispatch={isApproved}
                onSendSecureEmail={(recipient) => setDialog({kind: 'send', recipient})}
                onRecordSent={(recipient) => setDialog({kind: 'recordSent', recipient})}
              />
            </VStack>
          )}
        </Section>
      ),
    };
  }
}

/** The merge fields with no value for this patient now: approval is refused until they have one or are removed. */
function MissingMergeFields({letter, labels}: {letter: Letter; labels?: {name?: string | null; label?: string | null}[]}) {
  const missing = letter.missingMergeFields ?? [];
  if (missing.length === 0 || letter.status === 'Approved' || letter.status === 'Withdrawn') return null;
  const named = missing.map((name) => {
    const label = labels?.find((field) => field.name === name)?.label;
    return label ? `${label} ({{${name}}})` : `{{${name}}}`;
  });
  return (
    <AlertMessage
      status="warning"
      label="Merge fields have no value"
      message="These merge fields have no value for this patient, so it can't be approved until they have one (on the patient's record) or are taken out of the letter:"
      details={named}
    />
  );
}

/** The letter as written: its letterhead and closing as plain lines, its body as formatted Markdown. */
function LetterText({letter}: {letter: Letter}) {
  return (
    <VStack gap={4} hAlign="stretch">
      {letter.largePrint && (
        <HStack>
          <Token size="sm" label="Large print" color="blue" />
        </HStack>
      )}
      {letter.letterhead && (
        <Text type="body" color="secondary" style={PRESERVE_LINES}>
          {letter.letterhead}
        </Text>
      )}
      <RichText>{letter.body ?? ''}</RichText>
      {letter.closing && (
        <Text type="body" style={PRESERVE_LINES}>
          {letter.closing}
        </Text>
      )}
    </VStack>
  );
}

/** What it's about, where it is, and who did what. */
function LetterDetails({letter}: {letter: Letter}) {
  const at = (value: string | null | undefined) => (value ? formatDateTime(value) : null);
  return (
    <VStack gap={4} hAlign="stretch">
      <MetadataList columns={2} label={{position: 'top'}}>
        <MetadataListItem label="Status">
          <LetterStatusToken letter={letter} />
        </MetadataListItem>
        <MetadataListItem label="About">
          {letter.pathwayId ? (
            <Link href={`/pathways/${letter.pathwayId}`}>{letter.pathwayTitle || 'Its pathway'}</Link>
          ) : letter.referralId ? (
            <Link href={`/referrals/${letter.referralId}`}>The referral</Link>
          ) : (
            '—'
          )}
        </MetadataListItem>
        <MetadataListItem label="Template">{letter.templateName || '—'}</MetadataListItem>
        <MetadataListItem label="Author">{letter.authorUsername || '—'}</MetadataListItem>
        <MetadataListItem label="Approver">{letter.approverUsername || '—'}</MetadataListItem>
        <MetadataListItem label="Started">{at(letter.createdAt) ?? '—'}</MetadataListItem>
        {letter.submittedAtUtc && <MetadataListItem label="Submitted">{at(letter.submittedAtUtc)}</MetadataListItem>}
        {letter.approvedAtUtc && <MetadataListItem label="Approved">{at(letter.approvedAtUtc)}</MetadataListItem>}
        {letter.withdrawnAtUtc && (
          <MetadataListItem label="Withdrawn">{`${at(letter.withdrawnAtUtc)}${letter.withdrawnByUsername ? ` by ${letter.withdrawnByUsername}` : ''}`}</MetadataListItem>
        )}
      </MetadataList>
      {letter.withdrawnReason && (
        <VStack gap={1} hAlign="stretch">
          <Text type="body" weight="semibold">
            Why it was withdrawn
          </Text>
          <Text type="body" style={PRESERVE_LINES}>
            {letter.withdrawnReason}
          </Text>
        </VStack>
      )}
      {letter.replacesLetterId && (
        <Text type="body">
          <Link href={letterHref(letter.replacesLetterId)}>It corrects a withdrawn letter</Link>
        </Text>
      )}
      {letter.replacedByLetterId && (
        <Text type="body">
          <Link href={letterHref(letter.replacedByLetterId)}>Its correction</Link>
        </Text>
      )}
    </VStack>
  );
}
