import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {HomePage} from './HomePage';

describe('HomePage', () => {
  it('welcomes the signed-in user', async () => {
    renderWithApp(<HomePage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: `Welcome, ${staffUser.username}`, level: 1})).toBeInTheDocument();
  });
});
