import {Button} from '@astryxdesign/core/Button';
import type {InputStatus} from '@astryxdesign/core/Field';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {isPlaceholderNhsNumber, isValidNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {AppIcon} from '../shared/icons';
import {FIELD_STATUS_LINE_HEIGHT} from './fieldSpace';

export const NHS_NUMBER_DESCRIPTION = '10 digits, e.g. 999 123 4567';

export const NHS_NUMBER_MESSAGES = {
  notDigits: 'An NHS number is 10 digits, with no letters or other characters.',
  wrongLength: 'An NHS number is 10 digits long.',
  notValid: "This isn't a valid NHS number. Check it's typed correctly.",
  placeholder: "This looks like a placeholder, not an NHS number. Leave it empty if it isn't known.",
} as const;

/**
 * What's wrong with a typed NHS number, if anything, as the field shows it: letters or symbols, more than 10 digits,
 * a placeholder (e.g. 0000000000) or 10 digits that fail the check digit, at once; too few digits only once the person
 * has left the field (`hasLeft`), so it doesn't complain while they're still typing. Blank is fine: it's optional.
 */
export function nhsNumberProblem(value: string, hasLeft: boolean): string | null {
  const digits = normaliseNhsNumber(value);
  if (!digits) return null;
  if (!/^\d+$/.test(digits)) return NHS_NUMBER_MESSAGES.notDigits;
  if (digits.length > 10) return NHS_NUMBER_MESSAGES.wrongLength;
  if (digits.length === 10) {
    if (isPlaceholderNhsNumber(digits)) return NHS_NUMBER_MESSAGES.placeholder;
    return isValidNhsNumber(digits) ? null : NHS_NUMBER_MESSAGES.notValid;
  }
  return hasLeft ? NHS_NUMBER_MESSAGES.wrongLength : null;
}

export interface NhsNumberFieldProps {
  /** As typed: spaces allowed. */
  value: string;
  onChange: (value: string) => void;
  /** The server's error for the field (`fieldStatus(errors, 'nhsNumber')`), which wins over the field's own check. */
  status?: InputStatus;
  /** What a look-up found (or didn't), shown under the field while it has no error. */
  note?: InputStatus;
  isDisabled?: boolean;
  /**
   * Looks the number up (in the national demographics service). Given, a Look up button sits beside the field,
   * usable once the number is valid.
   */
  onLookUp?: (nhsNumber: string) => void;
  /** A look-up is under way: the button shows it's busy. */
  isLookingUp?: boolean;
}

/**
 * A patient's NHS number: optional, typed with or without spaces. It checks the number as it's typed (10 digits and
 * the Modulus 11 check digit), so a mistyped number shows straight away rather than on saving, which matters in a
 * clinical record (the wrong-patient hazards). The server's own error, after a save, wins. With `onLookUp`, a Look
 * up button beside it fills in the patient's details from the number. Use it wherever an NHS number is typed.
 */
export function NhsNumberField({value, onChange, status, note, isDisabled = false, onLookUp, isLookingUp = false}: NhsNumberFieldProps) {
  // Set on leaving the field, and cleared on typing, so "too short" shows only once they've stopped.
  const [hasLeft, setHasLeft] = useState(false);
  const problem = nhsNumberProblem(value, hasLeft);
  const shown: InputStatus | undefined = status ?? (problem ? {type: 'error', message: problem} : note);

  return (
    // The same structure with or without the button, so the input isn't replaced (losing what's being typed, and
    // the focus) when look-ups turn out to be set up.
    <HStack gap={2} vAlign="end">
      <StackItem size="fill">
        <TextInput
          label="NHS number"
          description={NHS_NUMBER_DESCRIPTION}
          value={value}
          onChange={(next) => {
            setHasLeft(false);
            onChange(next);
          }}
          onBlur={() => setHasLeft(true)}
          onEnter={onLookUp && isValidNhsNumber(value) && !isLookingUp ? () => onLookUp(value) : undefined}
          inputMode="numeric"
          autoComplete="off"
          hasAutoCorrect={false}
          hasSpellCheck={false}
          isOptional
          isDisabled={isDisabled}
          status={shown}
        />
      </StackItem>
      {onLookUp && (
        // Lined up with the input, above the line kept for the field's message.
        <HStack style={{paddingBottom: FIELD_STATUS_LINE_HEIGHT}}>
          <Button
            label="Look up"
            tooltip="Fill in the name and date of birth from the NHS number"
            variant="secondary"
            icon={<Icon icon={AppIcon.LookUp} />}
            isDisabled={isDisabled || !isValidNhsNumber(value)}
            isLoading={isLookingUp}
            onClick={() => onLookUp(value)}
          />
        </HStack>
      )}
    </HStack>
  );
}
