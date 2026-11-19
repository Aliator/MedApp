import {screen} from '@testing-library/react';
import {useState} from 'react';
import themeSource from '../../themes/medapp/medappTheme.ts?raw';
import {isDisabled, renderWithApp} from '../../test/render';
import {FIELD_STATUS_LINE_HEIGHT} from './fieldSpace';
import {NHS_NUMBER_MESSAGES, NhsNumberField, nhsNumberProblem, type NhsNumberFieldProps} from './NhsNumberField';

// Synthetic test numbers only (the 999 range is never issued to real patients).
const VALID = '9990000018';
const WRONG_CHECK_DIGIT = '9990000019';

function Field(props: Partial<NhsNumberFieldProps> & {initial?: string}) {
  const [value, setValue] = useState(props.initial ?? '');
  return <NhsNumberField value={value} onChange={setValue} {...props} />;
}

const input = () => screen.getByRole('textbox', {name: /NHS number/});

describe('nhsNumberProblem', () => {
  it('accepts a blank number, and a valid one with or without spaces', () => {
    expect(nhsNumberProblem('', true)).toBeNull();
    expect(nhsNumberProblem('  ', true)).toBeNull();
    expect(nhsNumberProblem(VALID, true)).toBeNull();
    expect(nhsNumberProblem('999 000 0018', true)).toBeNull();
  });

  it('says at once when there are letters, too many digits or a wrong check digit', () => {
    expect(nhsNumberProblem('999-000', false)).toBe(NHS_NUMBER_MESSAGES.notDigits);
    expect(nhsNumberProblem('99900000188', false)).toBe(NHS_NUMBER_MESSAGES.wrongLength);
    expect(nhsNumberProblem(WRONG_CHECK_DIGIT, false)).toBe(NHS_NUMBER_MESSAGES.notValid);
    expect(nhsNumberProblem('9990000140', false)).toBe(NHS_NUMBER_MESSAGES.notValid);
  });

  it('says a placeholder is one, and not just invalid', () => {
    expect(nhsNumberProblem('0000000000', false)).toBe(NHS_NUMBER_MESSAGES.placeholder);
    expect(nhsNumberProblem('111 111 1111', false)).toBe(NHS_NUMBER_MESSAGES.placeholder);
    expect(nhsNumberProblem('0123456789', false)).toBe(NHS_NUMBER_MESSAGES.placeholder);
    expect(nhsNumberProblem('9876543210', false)).toBe(NHS_NUMBER_MESSAGES.placeholder);
  });

  it('says a number is too short only once the field has been left', () => {
    expect(nhsNumberProblem('999 000', false)).toBeNull();
    expect(nhsNumberProblem('999 000', true)).toBe(NHS_NUMBER_MESSAGES.wrongLength);
  });
});

describe('NhsNumberField', () => {
  it('is labelled, says what to type, and takes a number with spaces', async () => {
    const {user} = renderWithApp(<Field />);

    await user.type(await screen.findByRole('textbox', {name: /NHS number/}), '999 000 0018');

    expect(input()).toHaveValue('999 000 0018');
    expect(input()).toHaveAccessibleDescription(/10 digits, e\.g\. 999 123 4567/);
    expect(input()).not.toHaveAttribute('aria-invalid', 'true');
  });

  it('shows a wrong check digit as soon as the tenth digit is typed', async () => {
    const {user} = renderWithApp(<Field />);

    await user.type(await screen.findByRole('textbox', {name: /NHS number/}), WRONG_CHECK_DIGIT);

    expect(input()).toHaveAccessibleDescription(new RegExp(NHS_NUMBER_MESSAGES.notValid.replace('.', '\\.')));
  });

  it('says a placeholder is one, and offers no look-up for it', async () => {
    renderWithApp(<Field initial="000 000 0000" onLookUp={() => {}} />);

    expect(await screen.findByRole('textbox', {name: /NHS number/})).toHaveAccessibleDescription(
      expect.stringContaining(NHS_NUMBER_MESSAGES.placeholder),
    );
    expect(isDisabled(screen.getByRole('button', {name: 'Look up'}))).toBe(true);
  });

  it('says a short number is too short on leaving the field, and not while typing again', async () => {
    const {user} = renderWithApp(<Field />);

    await user.type(await screen.findByRole('textbox', {name: /NHS number/}), '999');
    expect(input()).not.toHaveAccessibleDescription(/10 digits long/);

    await user.tab();
    expect(input()).toHaveAccessibleDescription(/An NHS number is 10 digits long\./);

    await user.type(input(), '0');
    expect(input()).not.toHaveAccessibleDescription(/10 digits long/);
  });

  it("shows the server's error over its own", async () => {
    renderWithApp(<Field initial={WRONG_CHECK_DIGIT} status={{type: 'error', message: 'Another patient already has this NHS number.'}} />);

    expect(await screen.findByRole('textbox', {name: /NHS number/})).toHaveAccessibleDescription(/Another patient already has this NHS number\./);
    expect(input()).not.toHaveAccessibleDescription(/valid NHS number/);
  });

  it('shows a note while there is no error', async () => {
    const {user} = renderWithApp(<Field initial={VALID} note={{type: 'success', message: 'Details filled in.'}} />);

    expect(await screen.findByRole('textbox', {name: /NHS number/})).toHaveAccessibleDescription(/Details filled in\./);
    await user.type(input(), '9');
    expect(input()).not.toHaveAccessibleDescription(/Details filled in\./);
  });

  it('has no Look up button without onLookUp', async () => {
    renderWithApp(<Field initial={VALID} />);

    await screen.findByRole('textbox', {name: /NHS number/});
    expect(screen.queryByRole('button', {name: 'Look up'})).not.toBeInTheDocument();
  });

  it('looks a valid number up, by the button or Enter, and not an invalid one', async () => {
    const onLookUp = vi.fn();
    const {user} = renderWithApp(<Field initial="999 000" onLookUp={onLookUp} />);

    const button = await screen.findByRole('button', {name: 'Look up'});
    expect(isDisabled(button)).toBe(true);
    await user.click(button);
    await user.type(input(), '{Enter}');
    expect(onLookUp).not.toHaveBeenCalled();

    await user.type(input(), '0018');
    expect(isDisabled(button)).toBe(false);
    await user.click(button);
    expect(onLookUp).toHaveBeenLastCalledWith('999 0000018');
    await user.type(input(), '{Enter}');
    expect(onLookUp).toHaveBeenCalledTimes(2);
  });

  it('shows a look-up under way, and is greyed out when disabled', async () => {
    const onLookUp = vi.fn();
    const {user, rerender} = renderWithApp(<NhsNumberField value={VALID} onChange={() => {}} onLookUp={onLookUp} isLookingUp />);

    const button = await screen.findByRole('button', {name: /Look up/});
    expect(button).toHaveAttribute('aria-busy', 'true');
    await user.type(input(), '{Enter}');
    expect(onLookUp).not.toHaveBeenCalled();
    rerender(<></>);

    renderWithApp(<NhsNumberField value={VALID} onChange={() => {}} onLookUp={onLookUp} isDisabled />);
    expect(isDisabled(await screen.findByRole('button', {name: 'Look up'}))).toBe(true);
  });

  it("lines its button up with the input using the theme's own status line", () => {
    expect(themeSource).toContain(FIELD_STATUS_LINE_HEIGHT);
  });
});
