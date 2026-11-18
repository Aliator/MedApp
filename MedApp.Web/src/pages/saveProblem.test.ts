import {ApiError, ApiValidationError, parseValidationErrors} from '../api/errors';
import {saveProblem} from './saveProblem';

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

  it('names a pathway when that is what is being saved', () => {
    expect(saveProblem(new ApiError(403), 'save', 'pathway').message).toBe('You do not have permission to change pathways.');
    // A permission the person's roles don't grant: the API names it.
    const missing = new ApiError(403, undefined, "Your roles don't allow this. It needs the permission to: See Insights.", 'permission_missing');
    expect(saveProblem(missing, 'save', 'pathway').message).toBe(missing.title);
    expect(saveProblem(new ApiError(404), 'save', 'pathway').message).toBe('Pathway not found. It may have been deleted.');
    expect(saveProblem(new ApiError(418), 'add', 'pathway').message).toBe(
      'An unexpected error occurred while adding the pathway.',
    );
  });

  it('names stages and templates, and archiving and restoring', () => {
    expect(saveProblem(new ApiError(404), 'archive', 'stage').message).toBe('Stage not found. It may have been deleted.');
    expect(saveProblem(new ApiError(418), 'archive', 'template').message).toBe('An unexpected error occurred while archiving the template.');
    expect(saveProblem(new ApiError(418), 'restore', 'stage').message).toBe(
      'An unexpected error occurred while restoring the stage.',
    );
  });

  it('shows the reason the API gives for a conflict', () => {
    const conflict = new ApiError(409, undefined, "This patient has 2 pathways, so they can't be deleted.");

    expect(saveProblem(conflict, 'delete').message).toBe("This patient has 2 pathways, so they can't be deleted.");
  });
});
