import {screen} from '@testing-library/react';
import {renderWithApp, isDisabled, openDialog} from '../../test/render';
import {ConfirmDialog} from './ConfirmDialog';

const base = {title: 'Delete user', message: 'This cannot be undone.', onConfirm: () => {}, onCancel: () => {}};

describe('ConfirmDialog', () => {
  it('shows the title, message and extra content when open', async () => {
    renderWithApp(
      <ConfirmDialog {...base} isOpen>
        <p>a.hartwell</p>
      </ConfirmDialog>,
    );

    expect(await screen.findByRole('heading', {name: 'Delete user'})).toBeInTheDocument();
    expect(screen.getByText('This cannot be undone.')).toBeInTheDocument();
    expect(screen.getByText('a.hartwell')).toBeInTheDocument();
  });

  it('renders nothing when closed', async () => {
    renderWithApp(<ConfirmDialog {...base} isOpen={false} />);

    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(openDialog()).toBeNull();
  });

  it('confirms and cancels', async () => {
    const onConfirm = vi.fn();
    const onCancel = vi.fn();
    const {user} = renderWithApp(<ConfirmDialog {...base} isOpen confirmText="Delete" isDanger onConfirm={onConfirm} onCancel={onCancel} />);

    await user.click(await screen.findByRole('button', {name: 'Delete'}));
    await user.click(screen.getByRole('button', {name: 'Cancel'}));

    expect(onConfirm).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalled();
  });

  it("cancels from the header's close button", async () => {
    const onCancel = vi.fn();
    const {user} = renderWithApp(<ConfirmDialog {...base} isOpen onCancel={onCancel} />);

    await user.click(await screen.findByRole('button', {name: /close/i}));

    expect(onCancel).toHaveBeenCalled();
  });

  it('can disable confirming', async () => {
    renderWithApp(<ConfirmDialog {...base} isOpen confirmDisabled />);

    expect(isDisabled(await screen.findByRole('button', {name: 'Confirm'}))).toBe(true);
  });

  it('blocks cancelling while processing', async () => {
    const onCancel = vi.fn();
    renderWithApp(<ConfirmDialog {...base} isOpen isProcessing onCancel={onCancel} />);

    expect(isDisabled(await screen.findByRole('button', {name: 'Cancel'}))).toBe(true);
    expect(screen.queryByRole('button', {name: /close/i})).not.toBeInTheDocument();
  });
});
