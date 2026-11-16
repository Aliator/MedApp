import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {StandalonePage} from './StandalonePage';

describe('StandalonePage', () => {
  it('shows MedApp over a card headed with the title, and names the tab', async () => {
    renderWithApp(
      <StandalonePage title="Reset your password">
        <p>Synthetic content</p>
      </StandalonePage>,
    );

    expect(await screen.findByRole('heading', {name: 'Reset your password', level: 1})).toBeInTheDocument();
    expect(screen.getByText('MedApp')).toBeInTheDocument();
    expect(screen.getByText('Synthetic content')).toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(document.title).toBe('Reset your password - MedApp');
  });
});
