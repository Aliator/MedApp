import {screen} from '@testing-library/react';
import {ACCESSIBILITY_SETTINGS} from '../../app/accessibility';
import {adminUser, isDisabled, renderWithApp, staffUser} from '../../test/render';
import {AccountPage} from './AccountPage';

describe('AccountPage', () => {
  it('shows who you are, and links to Email Address, Security, Change Password and Change Colours', async () => {
    renderWithApp(<AccountPage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Account Settings', level: 1})).toBeInTheDocument();
    expect(screen.getByText(staffUser.username)).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Change Password'})).toHaveAttribute('href', '/account/password');
    expect(screen.getByRole('link', {name: 'Email Address'})).toHaveAttribute('href', '/account/email');
    expect(screen.getByRole('link', {name: 'Security'})).toHaveAttribute('href', '/account/security');
    expect(screen.getByRole('link', {name: 'Change Colours'})).toHaveAttribute('href', '/account/colours');
  });

  it('switches between dark and light mode and remembers the choice', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: adminUser});

    const dark = await screen.findByRole('switch', {name: 'Dark mode'});
    expect(dark).toBeChecked();
    await user.click(dark);

    expect(dark).not.toBeChecked();
    expect(localStorage.getItem('medapp.colorMode')).toBe('light');
  });

  it('chooses whether nav items close the half view, and remembers it', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const closes = await screen.findByRole('switch', {name: 'Nav items close the half view'});
    expect(closes).toBeChecked();
    await user.click(closes);

    expect(closes).not.toBeChecked();
    expect(localStorage.getItem('medapp.navClosesHalfView')).toBe('false');
  });

  it('changes the side nav hover delay in the Accessibility card, and remembers it', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const delay = await screen.findByRole('slider', {name: 'Side nav hover delay'});
    expect(delay).toHaveAttribute('aria-valuetext', 'Standard (0.5s)');
    delay.focus();
    await user.keyboard('{End}');

    expect(delay).toHaveAttribute('aria-valuetext', 'Longer (2s)');

    expect(localStorage.getItem('medapp.navFlyoutDelay')).toBe('longer');
  });

  it('chooses the start page in the Navigation card, and remembers it', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    expect(await screen.findByRole('radio', {name: 'Home'})).toBeChecked();
    await user.click(screen.getByRole('radio', {name: 'My Worklist'}));

    expect(screen.getByRole('radio', {name: 'My Worklist'})).toBeChecked();
    expect(localStorage.getItem('medapp.startPage')).toBe('tasks');
  });

  it('chooses rows per page and where pathway lists start, in the Lists card', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const rows = await screen.findByRole('spinbutton', {name: /^Rows per page/});
    expect(rows).toHaveValue('10');
    const apply = screen.getByRole('button', {name: 'Apply'});
    expect(isDisabled(apply)).toBe(true);
    await user.clear(rows);
    await user.type(rows, '30');
    await user.tab();
    // Nothing changes until Apply.
    expect(localStorage.getItem('medapp.pageSize')).toBeNull();
    await user.click(apply);
    await user.click(screen.getByRole('switch', {name: 'Pathway lists start on Assigned to me'}));

    expect(localStorage.getItem('medapp.pageSize')).toBe('30');
    expect(isDisabled(screen.getByRole('button', {name: 'Apply'}))).toBe(true);
    expect(localStorage.getItem('medapp.proceduresStartOnMine')).toBe('true');
  });

  it('switches from light to dark mode', async () => {
    localStorage.setItem('medapp.colorMode', 'light');
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const dark = await screen.findByRole('switch', {name: 'Dark mode'});
    expect(dark).not.toBeChecked();
    await user.click(dark);

    expect(localStorage.getItem('medapp.colorMode')).toBe('dark');
  });

  it('keeps rows per page between 1 and 100', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const rows = await screen.findByRole('spinbutton', {name: /^Rows per page/});
    await user.clear(rows);
    await user.type(rows, '250{Enter}');

    expect(localStorage.getItem('medapp.pageSize')).toBe('100');
  });

  it('changes the text size in the Accessibility card, and remembers it', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    await user.click(await screen.findByRole('radio', {name: 'Large'}));

    expect(document.documentElement.style.fontSize).toBe('112.5%');
    expect(localStorage.getItem('medapp.textSize')).toBe('large');
  });

  it('switches the accessibility settings, and links to the statement', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    const shortcuts = await screen.findByRole('switch', {name: /^Keyboard shortcuts/});
    // Every accessibility switch starts off.
    for (const setting of ACCESSIBILITY_SETTINGS) {
      expect(screen.getByRole('switch', {name: new RegExp(`^${setting.label}`)})).not.toBeChecked();
    }
    await user.click(shortcuts);
    await user.click(screen.getByRole('switch', {name: /^Underline links/}));

    expect(JSON.parse(localStorage.getItem('medapp.accessibility')!)).toEqual({shortcuts: true, underlineLinks: true});
    expect(document.querySelector('style[data-medapp-accessibility]')?.textContent).toContain('.astryx-link');
    expect(screen.getByRole('link', {name: 'Accessibility statement'})).toHaveAttribute('href', '/accessibility');
  });
});

