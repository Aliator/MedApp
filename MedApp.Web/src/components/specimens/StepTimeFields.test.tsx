import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp, staffUser} from '../../test/render';
import type {UkDateTime} from '../../utils/specimenTimes';
import {StepTimeFields} from './StepTimeFields';

function Harness({onChange, status}: {onChange: (value: UkDateTime) => void; status?: {type: 'error'; message: string}}) {
  const [value, setValue] = useState<UkDateTime>({date: '2026-11-18', time: '14:05'});
  return (
    <StepTimeFields
      step="Sent"
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
      status={status}
    />
  );
}

describe('StepTimeFields', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T14:05:00Z')});
  });

  it('asks for the UK day and the 24-hour time, starting where it was given', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<Harness onChange={onChange} />, {user: staffUser});

    const day = await screen.findByRole('combobox', {name: /Sent on/});
    expect(day).toHaveValue('18 November 2026');
    expect(screen.getByText('UK time, today or up to a week back.')).toBeInTheDocument();
    const time = screen.getByRole('textbox', {name: /Time sent/});
    expect(time).toHaveValue('14:05');

    await user.clear(day);
    await user.type(day, '16/11/2026');
    await user.tab();
    expect(onChange).toHaveBeenLastCalledWith({date: '2026-11-16', time: '14:05'});

    await user.clear(time);
    await user.type(time, '09:30');
    await user.tab();
    expect(onChange).toHaveBeenLastCalledWith({date: '2026-11-16', time: '09:30'});
  });

  it("shows the API's message on the day", async () => {
    renderWithApp(<Harness onChange={vi.fn()} status={{type: 'error', message: "It can't be before it was taken."}} />, {user: staffUser});
    expect(await screen.findByText("It can't be before it was taken.")).toBeInTheDocument();
  });
});
