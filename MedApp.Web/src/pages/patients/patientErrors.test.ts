import {ApiError, ApiValidationError, parseValidationErrors} from '../../api/errors';
import {saveProblem} from './patientErrors';

describe('saveProblem', () => {
  it('returns field errors for a 400', () => {
    const errors = parseValidationErrors({errors: {Email: ['Email must not be empty.']}});

    expect(saveProblem(new ApiValidationError(errors), 'add')).toEqual({validation: errors, message: null});
  });

  it.each([
    [401, 'Your session has ended. Please sign in again.'],
    [403, 'You do not have permission to change patients.'],
    [404, 'Patient not found. It may have been deleted.'],
    [500, 'A server error occurred. Please try again later.'],
    [503, 'A server error occurred. Please try again later.'],
  ])('explains a %i', (status, message) => {
    expect(saveProblem(new ApiError(status), 'save')).toEqual({validation: null, message});
  });

  it('explains a network failure', () => {
    expect(saveProblem(new TypeError('Failed to fetch'), 'save').message).toBe(
      'Unable to connect to the server. Please check your connection and try again.',
    );
  });

  it.each([
    ['add', 'An unexpected error occurred while adding the patient.'],
    ['save', 'An unexpected error occurred while saving changes.'],
    ['delete', 'An unexpected error occurred while deleting the patient.'],
  ] as const)('falls back to a message about %s', (action, message) => {
    expect(saveProblem(new ApiError(409), action).message).toBe(message);
  });
});
