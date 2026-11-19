import {screen, within} from '@testing-library/react';
import {parseValidationErrors} from '../../api/errors';
import {openAlertDetails, renderWithApp} from '../../test/render';
import {AlertMessage, ValidationAlert} from './AlertMessage';

const MESSAGE = 'Synthetic refusal: the API said why, at some length, in words meant for the user.';

describe('AlertMessage', () => {
  it('shows the short headline, with the whole message behind the "i"', async () => {
    const {user} = renderWithApp(<AlertMessage label="Not saved" message={MESSAGE} details={['Email is already in use.']} />);

    expect(await screen.findByText('Not saved')).toBeInTheDocument();
    const card = await openAlertDetails(user, 'Not saved');
    expect(within(card).getByText(MESSAGE)).toBeInTheDocument();
    expect(within(card).getByText('Email is already in use.')).toBeInTheDocument();
  });

  it('opens the whole message on hover', async () => {
    const {user} = renderWithApp(<AlertMessage label="Not saved" message={MESSAGE} />);

    await user.hover(await screen.findByRole('button', {name: 'Not saved, show details'}));
    expect(await screen.findByRole('dialog', {name: 'Not saved: details'})).toHaveTextContent(MESSAGE);
  });

  it('opens the whole message when the "i" takes the keyboard focus', async () => {
    const {user} = renderWithApp(<AlertMessage label="Not booked" message={MESSAGE} />);
    await screen.findByRole('button', {name: 'Not booked, show details'});

    await user.tab();
    expect(await screen.findByRole('dialog', {name: 'Not booked: details'})).toHaveTextContent(MESSAGE);
  });

  it('announces an error’s whole message, with its details', async () => {
    renderWithApp(<AlertMessage label="Not saved" message="Could not save the patient." details={['Email is already in use.']} />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Could not save the patient. Email is already in use.');
    expect(screen.getByRole('button', {name: 'Not saved, show details'})).toHaveAccessibleDescription(
      'Could not save the patient. Email is already in use.',
    );
  });

  it('shows no "i" when the headline is the whole message, and announces the headline', async () => {
    renderWithApp(<AlertMessage label="Pathway not found" message="Pathway not found." />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Pathway not found');
    expect(screen.queryByRole('button', {name: /show details/})).not.toBeInTheDocument();
  });

  it('does not announce a success message as an alert', async () => {
    renderWithApp(<AlertMessage status="success" label="Patient saved" />);

    expect(await screen.findByText('Patient saved')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('keeps the leading icon of an info banner with no "i", so its status shows by more than colour', async () => {
    const {container} = renderWithApp(<AlertMessage status="info" label="Nothing to show" />);

    await screen.findByText('Nothing to show');
    expect(container.querySelectorAll('svg')).toHaveLength(1);
  });

  it('offers Dismiss only when asked to, and says when it was pressed', async () => {
    const onDismiss = vi.fn();
    const {user, unmount} = renderWithApp(
      <AlertMessage status="success" label="Bank holidays added" message="Added 8 bank holidays." onDismiss={onDismiss} />,
    );

    await user.click(await screen.findByRole('button', {name: 'Dismiss Bank holidays added'}));
    expect(onDismiss).toHaveBeenCalled();
    unmount();

    renderWithApp(<AlertMessage status="success" label="Patient saved" />);
    expect(await screen.findByText('Patient saved')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /^Dismiss/})).not.toBeInTheDocument();
  });
});

describe('ValidationAlert', () => {
  it('says "Check the form", with every message from a 400 behind the "i"', async () => {
    const errors = parseValidationErrors({
      title: 'One or more validation errors occurred.',
      errors: {FirstName: ['First name must not be empty.'], Email: ['Email must not be empty.']},
    });
    const {user} = renderWithApp(<ValidationAlert errors={errors} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('One or more validation errors occurred.');
    expect(screen.getByText('Check the form')).toBeInTheDocument();
    const card = await openAlertDetails(user, 'Check the form');
    expect(within(card).getByText('First name must not be empty.')).toBeInTheDocument();
    expect(within(card).getByText('Email must not be empty.')).toBeInTheDocument();
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
    expect(alert).toHaveTextContent('Patient is required.');
    expect(alert).toHaveTextContent('Something general.');
  });
});
