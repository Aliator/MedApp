import {screen} from '@testing-library/react';
import {adminUser, renderWithApp, staffUser} from '../../test/render';
import {AccountPage} from './AccountPage';

describe('AccountPage', () => {
  it('shows who you are, and links to Change Password and Change Colours', async () => {
    renderWithApp(<AccountPage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Account Settings', level: 1})).toBeInTheDocument();
    expect(screen.getByText(staffUser.username)).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Change Password'})).toHaveAttribute('href', '/account/password');
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

  it('changes the text size in the Accessibility card, and remembers it', async () => {
    const {user} = renderWithApp(<AccountPage />, {user: staffUser});

    await user.click(await screen.findByRole('radio', {name: 'Large'}));

    expect(document.documentElement.style.fontSize).toBe('112.5%');
    expect(localStorage.getItem('medapp.textSize')).toBe('large');
  });
});
