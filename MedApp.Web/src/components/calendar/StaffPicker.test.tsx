import {screen} from '@testing-library/react';
import {isDisabled, renderWithApp, staffUser} from '../../test/render';
import {StaffPicker, staffPickerOptions} from './StaffPicker';

const ME = {userId: 'u-me', username: 'test-user'};
const staff = [
  {userId: 'u-z', username: 'z.staff'},
  ME,
  {userId: 'u-a', username: 'a.staff'},
];

describe('staffPickerOptions', () => {
  it('offers you first, then everyone else by username', () => {
    expect(staffPickerOptions(staff, ME.userId, 'My calendar')).toEqual([
      {value: 'u-me', label: 'My calendar'},
      {value: 'u-a', label: 'a.staff'},
      {value: 'u-z', label: 'z.staff'},
    ]);
  });

  it('copes with no list yet, not knowing who you are, and people without their optional fields', () => {
    expect(staffPickerOptions(undefined, undefined, 'Mine')).toEqual([]);
    expect(staffPickerOptions([{}], 'u-me', 'Mine')).toEqual([
      {value: 'u-me', label: 'Mine'},
      {value: '', label: ''},
    ]);
  });
});

describe('StaffPicker', () => {
  it('shows whose is picked, and says who was picked: undefined for yourself', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(
      <StaffPicker label="Whose calendar" staff={staff} myId={ME.userId} ownLabel="My calendar" value="u-a" onChange={onChange} width={175} />,
      {user: staffUser},
    );

    const picker = await screen.findByRole('combobox', {name: 'Whose calendar'});
    expect(picker).toHaveTextContent('a.staff');
    await user.click(picker);
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['My calendar', 'a.staff', 'z.staff']);
    await user.click(screen.getByRole('option', {name: 'z.staff'}));
    expect(onChange).toHaveBeenLastCalledWith('u-z');

    await user.click(picker);
    await user.click(screen.getByRole('option', {name: 'My calendar'}));
    expect(onChange).toHaveBeenLastCalledWith(undefined);
  });

  it('can be disabled', async () => {
    renderWithApp(<StaffPicker label="Whose" staff={staff} myId={ME.userId} ownLabel="Mine" value="u-me" onChange={vi.fn()} isDisabled />, {
      user: staffUser,
    });
    expect(isDisabled(await screen.findByRole('combobox', {name: 'Whose'}))).toBe(true);
  });
});
