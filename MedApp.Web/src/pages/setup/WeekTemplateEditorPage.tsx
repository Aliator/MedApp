import {Button} from '@astryxdesign/core/Button';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Selector} from '@astryxdesign/core/Selector';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useEffect, useMemo, useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {dayTemplatesQuery, useSaveWeekTemplate, weekTemplateQuery, type DayTemplate, type WeekTemplate} from '../../api/calendar';
import {ApiError, fieldStatus} from '../../api/errors';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {NO_PROBLEM, saveProblem, type SaveProblem} from '../saveProblem';
import {clockTime, WEEKDAYS, type WeekdayKey} from './calendarSetup';
import {useTemplatesOwner, type TemplatesOwner} from './calendarTemplatesOwner';
import {calendarTemplatesHref} from './calendarTemplatesSearch';

/** A weekday with no day template: no template blocks, so free for appointments all day. */
const NONE = 'none';

type Days = Record<WeekdayKey, string>;

/**
 * A new week template (no `id`) for `staff` (none: your own), or one to edit. This part loads it, whose it is and
 * their day templates (a week template uses only its owner's); an unknown one goes back to the list. The editor
 * mounts once they're here, so it starts from the template. Only its owner and admins can add or change one: anyone
 * else sees it read-only, or is told they can't add one.
 */
export function WeekTemplateEditorPage({id, staff}: {id?: string; staff?: string}) {
  const navigate = useNavigate();
  const template = useQuery({...weekTemplateQuery(id ?? ''), enabled: id !== undefined});
  const owner = useTemplatesOwner(template.data?.ownerUserId ?? staff);
  // The owner's day templates: for one being edited, once it says whose it is.
  const isOwnerKnown = owner.ownerId !== undefined && (id === undefined || template.data !== undefined);
  const days = useQuery({...dayTemplatesQuery(owner.ownerId ?? ''), enabled: isOwnerKnown});
  const back = calendarTemplatesHref('week', owner.staffParam);
  const isMissing = template.error instanceof ApiError && template.error.status === 404;

  useEffect(() => {
    if (isMissing) void navigate({to: back, replace: true});
  }, [isMissing, navigate, back]);

  // The staff list too, which says whose it is and whether it's yours.
  const isLoading = owner.staff.isPending || (id !== undefined && template.isPending) || isMissing || (isOwnerKnown && days.isPending);
  const canAdd = id !== undefined || owner.canEdit;
  if (!isLoading && days.data && owner.ownerId && canAdd && (id === undefined || template.data)) {
    return <WeekTemplateEditor template={template.data ?? null} dayTemplates={days.data} owner={owner} ownerId={owner.ownerId} back={back} />;
  }
  // Your own, but you're not in the staff list (or it couldn't be loaded).
  const ownerFailed = !isLoading && owner.ownerId === undefined;
  const failed = days.isError || (template.isError && !isMissing) || ownerFailed;
  return (
    <Page
      title={id ? 'Edit Week Template' : 'New Week Template'}
      subtitle={owner.isOwn ? undefined : owner.title}
      backHref={back}
      backLabel="Back to week templates"
      isLoading={isLoading}
      loadingMessage="Loading week template..."
      message={
        failed ? (
          <AlertMessage message="The week template could not be loaded. Please try again." />
        ) : (
          !canAdd && <AlertMessage status="info" message={`Only ${owner.ownerName || 'their owner'} or an admin can add to ${owner.title}.`} />
        )
      }
    />
  );
}

/** A day template's blocks in a line, for the weekday it's chosen for: "Lunch 12:00 to 13:00, Admin time 16:00 to 17:00". */
function blocksSummary(template: DayTemplate | undefined): string {
  if (!template) return 'No template blocks: free for appointments all day.';
  const blocks = template.blocks ?? [];
  if (blocks.length === 0) return 'No blocks: free for appointments all day.';
  return blocks.map((b) => `${b.activityName} ${clockTime(b.startTime)} to ${clockTime(b.endTime)}`).join(', ');
}

/**
 * The week template editor (decision 9): its name, and a day template (or none) for each weekday, Monday to Friday,
 * each with its blocks in words. "Same every day" copies Monday's to the rest of the week. Saturday and Sunday aren't
 * working days, so they're only shown. Saving changes asks first, saying they apply from today and past days keep
 * what they had. Its day templates are the owner's. Without `canEdit` (someone else's, for staff) it's read-only.
 */
function WeekTemplateEditor({
  template,
  dayTemplates,
  owner,
  ownerId,
  back,
}: {
  template: WeekTemplate | null;
  dayTemplates: DayTemplate[];
  owner: TemplatesOwner;
  /** Whose it is (or will be, for a new one). */
  ownerId: string;
  back: string;
}) {
  const navigate = useNavigate();
  const save = useSaveWeekTemplate();
  const announce = useAnnounce();
  const [name, setName] = useState(template?.name ?? '');
  const [days, setDays] = useState<Days>(
    () =>
      Object.fromEntries(
        WEEKDAYS.map(({key, name: dayName}) => [key, template?.days?.find((d) => d.dayOfWeek === dayName)?.dayTemplateId ?? NONE]),
      ) as Days,
  );
  const [isConfirming, setIsConfirming] = useState(false);
  const [problem, setProblem] = useState<SaveProblem>(NO_PROBLEM);

  const byId = useMemo(() => new Map(dayTemplates.map((d) => [d.id ?? '', d])), [dayTemplates]);
  const backToList = () => void navigate({to: back});
  const usedBy = template?.staffCount ?? 0;
  const canEdit = template ? template.canEdit === true : owner.canEdit;
  // Fields can't change while saving, or ever on a template the user can't change.
  const isLocked = save.isPending || !canEdit;
  const whose = owner.isOwn ? 'your' : owner.ownerName ? `${owner.ownerName}'s` : 'their';
  const nameOf = (id: string) => (id === NONE ? 'No template' : (byId.get(id)?.name ?? 'Unknown'));

  /** The active day templates, and the one the day has if it's archived (so it still shows). */
  const optionsFor = (current: string) => [
    {value: NONE, label: 'No template'},
    ...dayTemplates
      .filter((d) => !d.isArchived || d.id === current)
      .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? '', undefined, {sensitivity: 'base'}))
      .map((d) => ({value: d.id ?? '', label: d.isArchived ? `${d.name} (archived)` : (d.name ?? '')})),
  ];

  const sameEveryDay = () => {
    setDays((current) => Object.fromEntries(WEEKDAYS.map(({key}) => [key, current.monday])) as Days);
    announce(`${nameOf(days.monday)} on every weekday`);
  };
  const isSameEveryDay = WEEKDAYS.every(({key}) => days[key] === days.monday);

  const submit = () =>
    save.mutate(
      {
        id: template?.id,
        ownerUserId: ownerId,
        name: name.trim(),
        ...Object.fromEntries(WEEKDAYS.map(({key}) => [key, days[key] === NONE ? null : days[key]])),
      },
      {
        onSuccess: () => {
          announce(template ? 'Week template saved' : 'Week template created');
          backToList();
        },
        onError: (e) => {
          setProblem(saveProblem(e, template ? 'save' : 'add', 'week template'));
          setIsConfirming(false);
        },
      },
    );

  const onSave = () => {
    setProblem(NO_PROBLEM);
    // A new one is nobody's default yet; changes to one in use apply from today, so they're confirmed.
    if (template) setIsConfirming(true);
    else submit();
  };

  return (
    <Page
      title={!canEdit ? 'Week Template' : template ? 'Edit Week Template' : 'New Week Template'}
      subtitle={[template?.name, owner.isOwn ? undefined : owner.title].filter(Boolean).join(' · ') || undefined}
      onBack={backToList}
      backLabel="Back to week templates"
      fill
      message={
        problem.validation ? (
          <ValidationAlert errors={problem.validation} fields={['name']} />
        ) : problem.message ? (
          <AlertMessage message={problem.message} />
        ) : (
          !canEdit && <AlertMessage status="info" message={`Only ${owner.ownerName || 'its owner'} or an admin can change this week template.`} />
        )
      }
      dialogs={
        <ConfirmDialog
          isOpen={isConfirming}
          title="Save Week Template"
          message={`Changes apply from today; past days keep what they had.${
            usedBy > 0 ? ` It's ${whose} default week, so ${whose} calendar changes too.` : ''
          }`}
          icon={AppIcon.Save}
          confirmText="Save"
          isProcessing={save.isPending}
          onConfirm={submit}
          onCancel={() => setIsConfirming(false)}>
          <MetadataList>
            <MetadataListItem label="Week template">{name.trim() || 'No name'}</MetadataListItem>
            {WEEKDAYS.map(({key, name: dayName}) => (
              <MetadataListItem key={key} label={dayName}>
                {nameOf(days[key])}
              </MetadataListItem>
            ))}
          </MetadataList>
        </ConfirmDialog>
      }>
      <FormCard
        fill
        saveText={template ? 'Save' : 'Create Week Template'}
        onSave={canEdit ? onSave : undefined}
        onCancel={backToList}
        showFooter={canEdit}
        isProcessing={save.isPending}>
        <FormSection title="Details" useGrid={false}>
          <TextInput
            label="Name"
            description={`What's chosen as ${whose} default week.`}
            value={name}
            onChange={setName}
            placeholder="e.g. Full time"
            autoComplete="off"
            isRequired
            isDisabled={isLocked}
            status={fieldStatus(problem.validation, 'name')}
          />
        </FormSection>
        <FormSection title="Days" useGrid={false}>
          <VStack gap={2}>
            {/* Above the days, so every day's field is the same width. */}
            {canEdit && (
              <HStack hAlign="end">
                <Button
                  label="Same Every Day"
                  tooltip="Use Monday's day template for Tuesday to Friday too"
                  variant="secondary"
                  isDisabled={save.isPending || isSameEveryDay}
                  onClick={sameEveryDay}
                />
              </HStack>
            )}
            {WEEKDAYS.map(({key, name: dayName}) => (
              <Selector
                key={key}
                label={dayName}
                description={blocksSummary(days[key] === NONE ? undefined : byId.get(days[key]))}
                options={optionsFor(days[key])}
                value={days[key]}
                onChange={(value) => setDays((current) => ({...current, [key]: value}))}
                isDisabled={isLocked}
              />
            ))}
            {['Saturday', 'Sunday'].map((dayName) => (
              <VStack key={dayName} gap={1}>
                <Text type="body" weight="semibold">
                  {dayName}
                </Text>
                <Text type="supporting" color="secondary">
                  Not a working day: nothing can be booked or blocked.
                </Text>
              </VStack>
            ))}
          </VStack>
        </FormSection>
      </FormCard>
    </Page>
  );
}
