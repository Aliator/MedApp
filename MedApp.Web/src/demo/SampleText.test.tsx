import {screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {renderWithApp} from '../test/render';
import {SampleText} from './SampleText';

describe('SampleText', () => {
  it('shows each line and copies them all, one per line', async () => {
    const user = userEvent.setup();
    const writeText = vi.spyOn(navigator.clipboard, 'writeText').mockResolvedValue();
    renderWithApp(<SampleText lines={['First line.', 'Second line.']} />);
    expect(await screen.findByText('First line.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Copy the Notes'}));
    expect(writeText).toHaveBeenCalledWith('First line.\nSecond line.');
  });
});
