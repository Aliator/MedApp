import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {AccessibilityStatementPage} from './AccessibilityStatementPage';

describe('AccessibilityStatementPage', () => {
  it('states the standard, the settings people can change, and how to report a problem', async () => {
    renderWithApp(<AccessibilityStatementPage />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Accessibility Statement', level: 1})).toBeInTheDocument();
    expect(screen.getByText(/WCAG\) 2\.2 at level AA/)).toBeInTheDocument();
    expect(screen.getByText('High contrast')).toBeInTheDocument();
    expect(screen.getByText('Keyboard shortcuts')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Accessibility Settings'})).toHaveAttribute('href', '/account');
    expect(screen.getByRole('heading', {name: 'Reporting a problem'})).toBeInTheDocument();
  });
});
