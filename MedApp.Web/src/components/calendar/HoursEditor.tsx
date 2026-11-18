import {Divider} from '@astryxdesign/core/Divider';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {hourOptions, rowProblem, type HoursRow} from '../../utils/workingHours';
import {AnchoredSelector} from '../forms/AnchoredSelector';

export interface HoursEditorProps {
  /** The seven days, Monday first (`staffRows` or `siteRows`). */
  rows: readonly HoursRow[];
  onChange: (rows: HoursRow[]) => void;
  /** What a day that's on is: "Works" for a person, "Open" for a site. */
  onLabel: string;
  /** For a person's hours: the sites a day can be at (each day picks one). None for a site's own hours. */
  sites?: {value: string; label: string}[];
  isDisabled?: boolean;
}

/**
 * Hours for each day of the week (centre plan decisions 119 and 122): a switch per day ("Works on Saturday"), and for
 * the days that are on, from and to in 15-minute steps between 06:00 and 23:00 and, for a person, the site. A day that
 * ends before it starts, or has no site, says so beside it; the server checks again.
 */
export function HoursEditor({rows, onChange, onLabel, sites, isDisabled = false}: HoursEditorProps) {
  const change = (day: HoursRow['day'], values: Partial<HoursRow>) => onChange(rows.map((r) => (r.day === day ? {...r, ...values} : r)));

  return (
    <VStack gap={2}>
      {rows.map((row, i) => {
        const problem = rowProblem(row, sites !== undefined);
        return (
          <VStack key={row.day} gap={2} role="group" aria-label={row.day}>
            {i > 0 && <Divider />}
            <HStack gap={2} vAlign="center" wrap="wrap">
              <StackItem size="fill">
                <Switch
                  label={`${onLabel} on ${row.day}`}
                  value={row.isOn}
                  onChange={(isOn) => change(row.day, {isOn})}
                  isDisabled={isDisabled}
                />
              </StackItem>
              {!row.isOn && (
                <Text type="supporting" color="secondary">
                  Not {onLabel === 'Open' ? 'open' : 'working'}
                </Text>
              )}
            </HStack>
            {row.isOn && (
              <HStack gap={2} vAlign="start" wrap="wrap">
                <AnchoredSelector
                  label={`${row.day} from`}
                  options={hourOptions(row.from)}
                  value={row.from}
                  onChange={(from) => change(row.day, {from})}
                  isDisabled={isDisabled}
                  containerStyle={{width: 150}}
                />
                <AnchoredSelector
                  label={`${row.day} to`}
                  options={hourOptions(row.to)}
                  value={row.to}
                  onChange={(to) => change(row.day, {to})}
                  isDisabled={isDisabled}
                  status={problem && problem !== 'Choose a site.' ? {type: 'error', message: problem} : undefined}
                  containerStyle={{width: 150}}
                />
                {sites && (
                  <StackItem size="fill">
                    <AnchoredSelector
                      label={`${row.day} at`}
                      options={sites}
                      value={row.siteId}
                      onChange={(siteId) => change(row.day, {siteId})}
                      isDisabled={isDisabled}
                      status={problem === 'Choose a site.' ? {type: 'error', message: problem} : undefined}
                      containerStyle={{minWidth: 180}}
                    />
                  </StackItem>
                )}
              </HStack>
            )}
          </VStack>
        );
      })}
    </VStack>
  );
}
