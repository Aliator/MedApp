import {Button} from '@astryxdesign/core/Button';
import {TextInput} from '@astryxdesign/core/TextInput';
import {screen} from '@testing-library/react';
import {renderWithApp, isDisabled} from '../../test/render';
import {FormCard} from './FormCard';
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
