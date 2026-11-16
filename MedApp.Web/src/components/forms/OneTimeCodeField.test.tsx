import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {OneTimeCodeField, type OneTimeCodeFieldProps} from './OneTimeCodeField';

function Field(props: Partial<OneTimeCodeFieldProps>) {
  const [value, setValue] = useState('');
  return <OneTimeCodeField value={value} onChange={setValue} {...props} />;
}

describe('OneTimeCodeField', () => {
  it.each([
    ['authenticator', /code from your authenticator app/i, 'one-time-code', 'numeric'],
    ['recovery', /recovery code/i, 'off', 'text'],
    ['either', /authenticator code or recovery code/i, 'one-time-code', 'text'],
  ] as const)('asks for a %s code, with the keyboard and AutoFill to suit', async (kind, label, autoComplete, inputMode) => {
    renderWithApp(<Field kind={kind} />);

    const input = await screen.findByLabelText(label);
    expect(input).toHaveAttribute('autocomplete', autoComplete);
    expect(input).toHaveAttribute('inputmode', inputMode);
    expect(input).toHaveAttribute('autocapitalize', 'off');
    expect(input).toHaveAttribute('spellcheck', 'false');
  });

  it('takes what is typed, under its own label, with its error', async () => {
    const {user} = renderWithApp(<Field label="Code" status={{type: 'error', message: 'Not right.'}} />);

    await user.type(await screen.findByLabelText(/^code/i), '123 456');

    expect(screen.getByLabelText(/^code/i)).toHaveValue('123 456');
    expect(screen.getAllByText('Not right.').length).toBeGreaterThan(0);
  });

  it('runs onEnter on Enter', async () => {
    const onEnter = vi.fn();
    const {user} = renderWithApp(<Field onEnter={onEnter} />);

    await user.type(await screen.findByLabelText(/code from your authenticator app/i), '1{Enter}');

    expect(onEnter).toHaveBeenCalledOnce();
  });
});
