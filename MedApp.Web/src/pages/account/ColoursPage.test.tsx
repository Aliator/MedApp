import {fireEvent, screen, within} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {ColoursPage} from './ColoursPage';

const list = (mode: 'Light' | 'Dark') => within(screen.getByRole('list', {name: `${mode} mode colour themes`}));
/** The readability warnings showing on the fields (not their copy in the screen reader's live region). */
const warnings = () => screen.queryAllByText(/hard to read/).filter((el) => !el.closest('[data-astryx-live-region]'));
const stored = () => JSON.parse(localStorage.getItem('medapp.colours') ?? 'null');

describe('ColoursPage', () => {
  it('lists the same themes for each mode, Standard chosen', async () => {
    renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    const names = (mode: 'Light' | 'Dark') =>
      list(mode)
        .getAllByRole('listitem')
        .map((item) => item.textContent);
    expect(names('Light')).toEqual(names('Dark'));
    expect(list('Light').getByText('Standard').closest('[aria-current]')).not.toBeNull();
    expect(screen.queryByLabelText(/Page background/)).not.toBeInTheDocument();
  });

  it("switches the app's mode from a card's header", async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    // Dark is the default, so it's the active one.
    expect(screen.getAllByText('Active')).toHaveLength(1);
    await user.click(screen.getByRole('heading', {name: 'Light Mode'}));
    expect(localStorage.getItem('medapp.colorMode')).toBe('light');
    await user.click(screen.getByRole('heading', {name: 'Dark Mode'}));
    expect(localStorage.getItem('medapp.colorMode')).toBe('dark');
    expect(screen.queryByRole('button', {name: /^Use /})).not.toBeInTheDocument();
  });

  it('chooses a theme for one mode, recolouring it and leaving the other', async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    await user.click(list('Dark').getByText('Chocolate'));

    // The app switches to that mode, so the change shows.
    expect(localStorage.getItem('medapp.colorMode')).toBe('dark');
    expect(stored().dark).toEqual({page: '#1c1610', card: '#2a2018', nav: '#141010'});
    expect(stored().light.page).toBe('#d8e0ef');
  });

  it('can choose any theme in either mode, Stone included (no two are the same)', async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    await user.click(list('Dark').getByText('Stone'));

    expect(list('Dark').getByText('Stone').closest('[aria-current]')).not.toBeNull();
    expect(list('Dark').getByText('Standard').closest('[aria-current]')).toBeNull();
  });

  it('opens Custom with its colour pickers, folding the themes away, and closes it again', async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    await user.click(list('Light').getByText('Custom'));
    expect(localStorage.getItem('medapp.colorMode')).toBe('light');
    expect(list('Light').queryByText('Stone')).not.toBeInTheDocument();
    expect(screen.getByLabelText(/Page background/)).toHaveValue('#d8e0ef');

    await user.click(list('Light').getByText('Custom'));
    expect(list('Light').getByText('Stone')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Page background/)).not.toBeInTheDocument();
  });

  it('picks a custom colour for one part, in that mode, and goes back to the themes from the arrow', async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    await user.click(list('Light').getByText('Custom'));
    await user.click(screen.getByRole('heading', {name: 'Dark Mode'}));
    fireEvent.input(screen.getByLabelText(/Page background/), {target: {value: '#2a3550'}});

    expect(localStorage.getItem('medapp.colorMode')).toBe('light');
    expect(stored().light.page).toBe('#2a3550');

    await user.click(screen.getByRole('button', {name: 'Back to the themes'}));
    expect(list('Light').getByText('Stone')).toBeInTheDocument();
    expect(screen.queryByLabelText(/Page background/)).not.toBeInTheDocument();
  });

  it('warns on a custom colour that leaves text hard to read, and stops when it is readable again', async () => {
    const {user} = renderWithApp(<ColoursPage />, {user: staffUser});

    await screen.findByRole('heading', {name: 'Colours', level: 1});
    await user.click(list('Light').getByText('Custom'));
    expect(warnings()).toHaveLength(0);

    // Light mode's text is dark, so a dark page is hard to read on.
    fireEvent.input(screen.getByLabelText(/Page background/), {target: {value: '#2a3550'}});
    expect(warnings().map((el) => el.textContent)).toEqual([
      'Text on this colour is hard to read (under 4.5:1 contrast). Try a lighter colour.',
    ]);

    fireEvent.input(screen.getByLabelText(/Page background/), {target: {value: '#ffffff'}});
    expect(warnings()).toHaveLength(0);
  });
});
