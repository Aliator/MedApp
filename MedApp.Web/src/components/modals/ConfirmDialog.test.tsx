import {act, fireEvent, screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp, isDisabled, openDialog} from '../../test/render';
import {SaveShortcutProvider} from '../forms/SaveShortcut';
import {ConfirmDialog} from './ConfirmDialog';

const base = {title: 'Delete user', message: 'This cannot be undone.', onConfirm: () => {}, onCancel: () => {}};

/** A dialog whose confirm button becomes "Done" after the first step, held for a second after it changes. */
function TwoSteps({onDone}: {onDone: () => void}) {
  const [step, setStep] = useState(1);
  return (
    <ConfirmDialog
      {...base}
      isOpen
      confirmText={step === 1 ? 'Create' : 'Done'}
      confirmHoldMs={1000}
      onConfirm={() => (step === 1 ? setStep(2) : onDone())}
    />
  );
}

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

  it('confirms on Ctrl+S in a dialog that is a form, only when it could be confirmed', async () => {
    const onConfirm = vi.fn();
    const dialog = (props: {hasSaveShortcut?: boolean; confirmDisabled?: boolean}) => (
      <SaveShortcutProvider>
        <ConfirmDialog {...base} isOpen onConfirm={onConfirm} {...props}>
          <input aria-label="Name" />
        </ConfirmDialog>
      </SaveShortcutProvider>
    );
    const pressIn = async () => {
      const field = await screen.findByLabelText('Name');
      field.focus();
      return fireEvent.keyDown(field, {key: 's', ctrlKey: true});
    };

    // An "are you sure?" dialog wants a deliberate answer.
    let view = renderWithApp(dialog({}));
    expect(await pressIn()).toBe(true);
    view.unmount();

    // A form, but one that can't be confirmed yet: nothing, and no browser Save page dialog either.
    view = renderWithApp(dialog({hasSaveShortcut: true, confirmDisabled: true}));
    expect(await pressIn()).toBe(false);
    expect(onConfirm).not.toHaveBeenCalled();
    view.unmount();

    renderWithApp(dialog({hasSaveShortcut: true}));
    await pressIn();
    expect(onConfirm).toHaveBeenCalledOnce();
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

  it('ignores the confirm button for a moment after its text changes, but not when it first shows', async () => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    const onDone = vi.fn();
    const {user} = renderWithApp(<TwoSteps onDone={onDone} />);

    // The first step isn't held: its text hasn't changed.
    await user.click(await screen.findByRole('button', {name: 'Create'}));
    // The second click of a double-click lands on Done, and does nothing.
    await user.click(screen.getByRole('button', {name: 'Done'}));
    expect(onDone).not.toHaveBeenCalled();

    await act(() => vi.advanceTimersByTimeAsync(1000));
    await user.click(screen.getByRole('button', {name: 'Done'}));
    expect(onDone).toHaveBeenCalledOnce();
  });
});
