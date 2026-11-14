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
});
