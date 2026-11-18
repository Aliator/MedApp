import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {NoPermissionPage} from './NoPermissionPage';

describe('NoPermissionPage', () => {
  it('says what the page needs, in the words the roles page uses, and offers a way home', async () => {
    renderWithApp(<NoPermissionPage permission="ViewInsights" />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'No permission', level: 1})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: "You don't have permission to use this page"})).toBeInTheDocument();
    expect(screen.getAllByText('It needs the permission to: See Insights. Ask an administrator if you need it.').length).toBeGreaterThan(0);
    expect(screen.getByRole('link', {name: 'Go home'})).toHaveAttribute('href', '/');
  });

  it('still says so when the address names no permission it knows', async () => {
    renderWithApp(<NoPermissionPage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: "You don't have permission to use this page"})).toBeInTheDocument();
    expect(screen.getAllByText('Ask an administrator if you need it.').length).toBeGreaterThan(0);
  });
});
