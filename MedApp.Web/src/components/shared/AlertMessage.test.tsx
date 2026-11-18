import {screen} from '@testing-library/react';
import {parseValidationErrors} from '../../api/errors';
import {renderWithApp} from '../../test/render';
import {AlertMessage, ValidationAlert} from './AlertMessage';

describe('AlertMessage', () => {
  it('announces an error with its details', async () => {
    renderWithApp(<AlertMessage message="Could not save the patient." details={['Email is already in use.']} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Could not save the patient.');
    expect(screen.getByText('Email is already in use.')).toBeVisible();
  });

  it('does not announce a success message as an alert', async () => {
    renderWithApp(<AlertMessage status="success" message="Patient saved." />);

    expect(await screen.findByText('Patient saved.')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('offers Dismiss only when asked to, and says when it was pressed', async () => {
    const onDismiss = vi.fn();
    const {user, unmount} = renderWithApp(<AlertMessage status="success" message="Added 8 bank holidays." onDismiss={onDismiss} />);

    await user.click(await screen.findByRole('button', {name: /^Dismiss/}));
    expect(onDismiss).toHaveBeenCalled();
    unmount();

    renderWithApp(<AlertMessage status="success" message="Patient saved." />);
    expect(await screen.findByText('Patient saved.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Dismiss/})).not.toBeInTheDocument();
  });
});

describe('ValidationAlert', () => {
  it('lists every message from a 400', async () => {
    const errors = parseValidationErrors({
      title: 'One or more validation errors occurred.',
      errors: {FirstName: ['First name must not be empty.'], Email: ['Email must not be empty.']},
    });
    renderWithApp(<ValidationAlert errors={errors} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('One or more validation errors occurred.');
    expect(screen.getByText('First name must not be empty.')).toBeVisible();
    expect(screen.getByText('Email must not be empty.')).toBeVisible();
  });

  it('leaves out the messages a form shows on its own fields, keeping the others', async () => {
    const errors = parseValidationErrors({
      title: 'One or more validation errors occurred.',
      errors: {FirstName: ['First name must not be empty.'], PatientId: ['Patient is required.'], '': ['Something general.']},
    });
    renderWithApp(<ValidationAlert errors={errors} fields={['firstName', 'email']} />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('One or more validation errors occurred.');
    expect(alert).not.toHaveTextContent('First name must not be empty.');
    expect(screen.getByText('Patient is required.')).toBeVisible();
    expect(screen.getByText('Something general.')).toBeVisible();
  });
});
