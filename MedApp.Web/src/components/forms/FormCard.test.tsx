import {Button} from '@astryxdesign/core/Button';
import {TextInput} from '@astryxdesign/core/TextInput';
import {fireEvent, screen} from '@testing-library/react';
import {renderWithApp, isDisabled} from '../../test/render';
import {HalfViewContext} from '../pages/halfView';
import {FormCard} from './FormCard';
import {SaveShortcutProvider} from './SaveShortcut';
import {FormSection} from './FormSection';

describe('FormCard', () => {
  it('saves when Save is clicked', async () => {
    const onSave = vi.fn();
    const {user} = renderWithApp(
      <FormCard onSave={onSave} saveText="Add patient">
        <p>Fields</p>
      </FormCard>,
    );

    await user.click(await screen.findByRole('button', {name: 'Add patient'}));

    expect(onSave).toHaveBeenCalledOnce();
  });

  it('fills its space with the same sections and footer, still saving', async () => {
    const onSave = vi.fn();
    const {user} = renderWithApp(
      <FormCard fill onSave={onSave} onCancel={() => {}}>
        <FormSection title="Details">
          <p>Fields</p>
        </FormSection>
      </FormCard>,
    );

    expect(await screen.findByRole('heading', {name: 'Details'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Cancel'})).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save'}));

    expect(onSave).toHaveBeenCalledOnce();
  });

  it('saves when Enter is pressed in a field', async () => {
    const onSave = vi.fn();
    const {user} = renderWithApp(
      <FormCard onSave={onSave}>
        <TextInput label="First name" value="" onChange={() => {}} />
      </FormCard>,
    );

    await user.type(await screen.findByLabelText(/first name/i), '{Enter}');

    expect(onSave).toHaveBeenCalledOnce();
  });

  it('saves on Ctrl+S in a field, but not while processing', async () => {
    const onSave = vi.fn();
    const form = (isProcessing: boolean) => (
      <SaveShortcutProvider>
        <FormCard onSave={onSave} isProcessing={isProcessing}>
          <TextInput label="First name" value="" onChange={() => {}} />
        </FormCard>
      </SaveShortcutProvider>
    );
    const {unmount} = renderWithApp(form(false));

    const field = await screen.findByLabelText(/first name/i);
    field.focus();
    expect(fireEvent.keyDown(field, {key: 's', ctrlKey: true})).toBe(false);
    expect(onSave).toHaveBeenCalledOnce();
    unmount();

    renderWithApp(form(true));
    const busy = await screen.findByLabelText(/first name/i);
    busy.focus();
    fireEvent.keyDown(busy, {key: 's', ctrlKey: true});
    expect(onSave).toHaveBeenCalledOnce();
  });

  it('does nothing when Enter is pressed in a form with no Save', async () => {
    const onCancel = vi.fn();
    const {user} = renderWithApp(
      <FormCard onCancel={onCancel}>
        <TextInput label="First name" value="" onChange={() => {}} />
      </FormCard>,
    );

    await user.type(await screen.findByLabelText(/first name/i), '{Enter}');

    expect(onCancel).not.toHaveBeenCalled();
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
  });

  it('cancels and deletes through their own buttons, without saving', async () => {
    const onSave = vi.fn();
    const onCancel = vi.fn();
    const onDelete = vi.fn();
    const {user} = renderWithApp(
      <FormCard onSave={onSave} onCancel={onCancel} onDelete={onDelete}>
        <p>Fields</p>
      </FormCard>,
    );

    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await user.click(screen.getByRole('button', {name: 'Delete'}));

    expect(onCancel).toHaveBeenCalledOnce();
    expect(onDelete).toHaveBeenCalledOnce();
    expect(onSave).not.toHaveBeenCalled();
  });

  it('only shows the buttons it has handlers for, plus any footer actions', async () => {
    renderWithApp(
      <FormCard onSave={() => {}} footerActions={<Button label="Reset" />}>
        <p>Fields</p>
      </FormCard>,
    );

    expect(await screen.findByRole('button', {name: 'Save'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Reset'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Cancel'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete'})).not.toBeInTheDocument();
  });

  it('disables the buttons and ignores submits while processing', async () => {
    const onSave = vi.fn();
    const {user} = renderWithApp(
      <FormCard onSave={onSave} onCancel={() => {}} isProcessing>
        <TextInput label="First name" value="" onChange={() => {}} />
      </FormCard>,
    );

    expect(isDisabled(await screen.findByRole('button', {name: 'Cancel'}))).toBe(true);
    await user.type(screen.getByLabelText(/first name/i), '{Enter}');
    expect(onSave).not.toHaveBeenCalled();
  });

  it("wraps the footer's buttons onto more lines in a narrow layout (a phone, a half view), rather than cutting them short", async () => {
    const form = (
      <FormCard onSave={() => {}} onCancel={() => {}} onDelete={() => {}}>
        <p>Fields</p>
      </FormCard>
    );
    const {unmount} = renderWithApp(form);
    const footer = async () => (await screen.findByRole('button', {name: 'Save'})).parentElement!;
    expect(await footer()).not.toHaveAttribute('data-wrap', 'wrap');
    unmount();

    renderWithApp(<HalfViewContext.Provider value={{isHalf: true}}>{form}</HalfViewContext.Provider>);
    expect(await footer()).toHaveAttribute('data-wrap', 'wrap');
  });

  it('can hide the footer', async () => {
    renderWithApp(
      <FormCard onSave={() => {}} showFooter={false}>
        <p>Fields</p>
      </FormCard>,
    );

    expect(await screen.findByText('Fields')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Save'})).not.toBeInTheDocument();
  });
});

describe('FormSection', () => {
  it('shows its title and fields', async () => {
    renderWithApp(
      <FormSection title="Personal Information">
        <TextInput label="First name" value="" onChange={() => {}} />
        <TextInput label="Last name" value="" onChange={() => {}} />
      </FormSection>,
    );

    expect(await screen.findByRole('heading', {name: 'Personal Information'})).toBeInTheDocument();
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/last name/i)).toBeInTheDocument();
  });

  it('can do without a title, in one column', async () => {
    renderWithApp(
      <FormSection useGrid={false}>
        <TextInput label="Email" value="" onChange={() => {}} />
      </FormSection>,
    );

    expect(await screen.findByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
  });
});
