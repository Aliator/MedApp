import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {siteHoursQuery, useSaveSiteHours, type Site} from '../../api/sites';
import {HoursEditor} from '../../components/calendar/HoursEditor';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {formatDate} from '../../utils/dates';
import {rowProblem, siteInput, siteRows, type HoursRow} from '../../utils/workingHours';
import {saveProblem} from '../saveProblem';
import {problemText} from './StaffHoursPanel';

export interface SiteHoursDialogProps {
  /** The site whose opening hours to change; none: closed. Give it a `key` of the site, so each opening starts afresh. */
  site: Site | null;
  onClose: () => void;
}

/**
 * A site's opening hours (centre plan decisions 119 and 122), from the Sites and Rooms page: the days it's open and
 * when, or the default (Monday to Friday, 08:00 to 18:00). An evening clinic is a day open until 20:00; a Saturday
 * clinic a Saturday morning. Staff working there work inside them: the API refuses (409, naming who) hours that would
 * leave someone's outside, and an archived site's. Changes apply from today; past days keep what they had.
 */
export function SiteHoursDialog({site, onClose}: SiteHoursDialogProps) {
  const siteId = site?.id ?? '';
  const hours = useQuery({...siteHoursQuery(siteId), enabled: site !== null});
  const save = useSaveSiteHours();
  const announce = useAnnounce();
  // What's been changed; until then, the hours as loaded (the page opens it afresh each time: its `key`).
  const [edited, setEdited] = useState<{rows?: HoursRow[]; isDefault?: boolean}>({});
  const [problem, setProblem] = useState<string | null>(null);
  const data = hours.data;
  const rows = edited.rows ?? siteRows(data);
  const isDefault = edited.isDefault ?? !data?.hasOwnHours;
  const setRows = (next: HoursRow[]) => setEdited((e) => ({...e, rows: next}));
  const setIsDefault = (next: boolean) => setEdited((e) => ({...e, isDefault: next}));

  const invalid = !isDefault && rows.some((r) => rowProblem(r, false) !== null);

  const submit = () => {
    setProblem(null);
    save.mutate(
      {siteId, days: isDefault ? [] : siteInput(rows)},
      {
        onSuccess: () => {
          announce('Opening hours saved');
          onClose();
        },
        onError: (e) => setProblem(problemText(saveProblem(e, 'save', 'opening hours'))),
      },
    );
  };

  return (
    <ConfirmDialog
      isOpen={site !== null}
      title="Opening Hours"
      message={`When ${site?.name ?? 'the site'} is open, from today. Staff working there work inside these hours; past days keep what they had.`}
      icon={AppIcon.Calendar}
      confirmText="Save"
      isProcessing={save.isPending}
      confirmDisabled={!data || invalid}
      hasSaveShortcut
      onConfirm={submit}
      onCancel={onClose}>
      <VStack gap={3}>
        {hours.isError && <AlertMessage message="The opening hours could not be loaded. Please try again." />}
        {problem && <AlertMessage message={problem} />}
        {data?.hasOwnHours && data.since && (
          <Text type="supporting" color="secondary">{`These hours since ${formatDate(data.since)}.`}</Text>
        )}
        <Switch
          label="Use the default hours: Monday to Friday, 08:00 to 18:00"
          value={isDefault}
          onChange={setIsDefault}
          isDisabled={!data || save.isPending}
        />
        {!isDefault && <HoursEditor rows={rows} onChange={setRows} onLabel="Open" isDisabled={save.isPending} />}
      </VStack>
    </ConfirmDialog>
  );
}
