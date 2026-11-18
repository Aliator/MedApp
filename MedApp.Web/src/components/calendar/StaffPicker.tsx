import type {ComponentProps} from 'react';
import type {StaffMember} from '../../api/calendar';
import {AppIcon} from '../shared/icons';
import {AnchoredSelector} from '../forms/AnchoredSelector';

/** The picker's choices: yours first, named `ownLabel`, then everyone else by username. */
export function staffPickerOptions(staff: readonly StaffMember[] | undefined, myId: string | undefined, ownLabel: string) {
  return [
    ...(myId ? [{value: myId, label: ownLabel}] : []),
    ...(staff ?? [])
      .filter((s) => s.userId !== myId)
      .map((s) => ({value: s.userId ?? '', label: s.username ?? ''}))
      .sort((a, b) => a.label.localeCompare(b.label)),
  ];
}

export interface StaffPickerProps {
  /** The field's name for screen readers (its label is hidden), e.g. "Whose calendar". */
  label: string;
  /** Everyone who can have a calendar (`staffQuery`). */
  staff: readonly StaffMember[] | undefined;
  /** The signed-in user's staff ID, offered first as `ownLabel`. */
  myId: string | undefined;
  /** What your own choice says, e.g. "My calendar" or "My templates". */
  ownLabel: string;
  /** The person shown (a user ID). */
  value: string;
  /** Someone else was picked (their ID), or yourself (undefined, so a URL leaves `staff` out for your own). */
  onChange: (staffUserId: string | undefined) => void;
  isLoading?: boolean;
  isDisabled?: boolean;
  width?: ComponentProps<typeof AnchoredSelector>['width'];
}

/**
 * Picks whose calendar (or calendar templates) a page shows: yourself first, then everyone else by username, with a
 * search once there are more than 8. A toolbar field: its label is hidden, so `label` names it for screen readers.
 */
export function StaffPicker({label, staff, myId, ownLabel, value, onChange, isLoading, isDisabled, width}: StaffPickerProps) {
  const options = staffPickerOptions(staff, myId, ownLabel);
  return (
    <AnchoredSelector
      label={label}
      isLabelHidden
      value={value}
      onChange={(next) => onChange(next === myId ? undefined : next)}
      options={options}
      hasSearch={options.length > 8}
      isLoading={isLoading}
      isDisabled={isDisabled}
      startIcon={AppIcon.Users}
      width={width}
    />
  );
}
