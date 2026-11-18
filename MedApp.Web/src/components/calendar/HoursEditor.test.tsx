import {screen, within} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {siteRows, staffRows, type HoursRow} from '../../utils/workingHours';
import {HoursEditor} from './HoursEditor';

const SITES = [
  {value: 'main', label: 'Main site'},
  {value: 'north', label: 'Northgate outreach clinic'},
];

function Editor({initial, sites, onChange}: {initial: HoursRow[]; sites?: typeof SITES; onChange: (rows: HoursRow[]) => void}) {
  const [rows, setRows] = useState(initial);
  return (
    <HoursEditor
      rows={rows}
      onChange={(next) => {
        setRows(next);
        onChange(next);
      }}
      onLabel={sites ? 'Works' : 'Open'}
      sites={sites}
    />
  );
}

const day = (name: string) => within(screen.getByRole('group', {name}));

describe('HoursEditor', () => {
  it("turns a person's Saturday on, with its times and site, and says what is wrong beside the day", async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<Editor initial={staffRows(undefined, 'main')} sites={SITES} onChange={onChange} />);
    await screen.findByRole('group', {name: 'Saturday'});

    expect(day('Saturday').getByText('Not working')).toBeInTheDocument();
    expect(day('Saturday').queryByRole('combobox')).not.toBeInTheDocument();
    await user.click(day('Saturday').getByRole('switch', {name: 'Works on Saturday'}));

    expect(day('Saturday').getByRole('combobox', {name: 'Saturday from'})).toHaveTextContent('08:00');
    expect(day('Saturday').getByRole('combobox', {name: 'Saturday at'})).toHaveTextContent('Main site');
    await user.click(day('Saturday').getByRole('combobox', {name: 'Saturday to'}));
    await user.click(screen.getByRole('option', {name: '07:00'}));
    expect(day('Saturday').getByText('Ends before it starts.')).toBeInTheDocument();
    await user.click(day('Saturday').getByRole('combobox', {name: 'Saturday at'}));
    await user.click(screen.getByRole('option', {name: 'Northgate outreach clinic'}));

    expect(onChange).toHaveBeenLastCalledWith(
      expect.arrayContaining([{day: 'Saturday', isOn: true, from: '08:00', to: '07:00', siteId: 'north'}]),
    );
  });

  it("asks a site's day for times only, and says when it is closed", async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<Editor initial={siteRows(undefined)} onChange={onChange} />);
    await screen.findByRole('group', {name: 'Sunday'});

    expect(day('Sunday').getByText('Not open')).toBeInTheDocument();
    await user.click(day('Tuesday').getByRole('switch', {name: 'Open on Tuesday'}));
    expect(day('Tuesday').getByRole('combobox', {name: 'Tuesday to'})).toBeInTheDocument();
    expect(day('Tuesday').queryByRole('combobox', {name: 'Tuesday at'})).not.toBeInTheDocument();
    await user.click(day('Tuesday').getByRole('combobox', {name: 'Tuesday to'}));
    await user.click(screen.getByRole('option', {name: '20:00'}));
    expect(onChange).toHaveBeenLastCalledWith(expect.arrayContaining([expect.objectContaining({day: 'Tuesday', isOn: true, to: '20:00'})]));
  });

  it('asks for a site when a day has none', async () => {
    renderWithApp(<Editor initial={staffRows(undefined).map((r) => (r.day === 'Monday' ? {...r, isOn: true} : r))} sites={SITES} onChange={vi.fn()} />);
    expect(within(await screen.findByRole('group', {name: 'Monday'})).getByText('Choose a site.')).toBeInTheDocument();
  });
});
