import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {OneTimePassword} from './OneTimePassword';

describe('OneTimePassword', () => {
  it('shows the password under its label, with a note that it is shown only once', async () => {
    renderWithApp(<OneTimePassword label="First password" password="Synth3ticPass" username="new.user" />);

    expect(await screen.findByText('First password')).toBeInTheDocument();
    expect(screen.getByText('Synth3ticPass')).toBeInTheDocument();
    expect(screen.getByText('This password is shown only once. Save it now and pass it on to new.user securely.')).toBeInTheDocument();
  });
});
