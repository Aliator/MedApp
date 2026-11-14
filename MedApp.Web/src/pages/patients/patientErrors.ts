import {ApiError, ApiValidationError, type ValidationErrors} from '../../api/errors';

/** What the add and edit pages show for a failed save: field errors for a 400, otherwise one message. */
export interface SaveProblem {
  validation: ValidationErrors | null;
  message: string | null;
}

export const NO_PROBLEM: SaveProblem = {validation: null, message: null};

/** The Blazor pages' messages for each kind of failure. */
export function saveProblem(error: unknown, action: 'save' | 'add' | 'delete'): SaveProblem {
  if (error instanceof ApiValidationError) return {validation: error.errors, message: null};
  if (error instanceof ApiError) {
    switch (error.status) {
      case 401:
        return {validation: null, message: 'Your session has ended. Please sign in again.'};
      case 403:
        return {validation: null, message: 'You do not have permission to change patients.'};
      case 404:
        return {validation: null, message: 'Patient not found. It may have been deleted.'};
      default:
        if (error.status >= 500) return {validation: null, message: 'A server error occurred. Please try again later.'};
    }
  }
  if (error instanceof TypeError) {
    return {validation: null, message: 'Unable to connect to the server. Please check your connection and try again.'};
  }
  const what = action === 'add' ? 'adding the patient' : action === 'delete' ? 'deleting the patient' : 'saving changes';
  return {validation: null, message: `An unexpected error occurred while ${what}.`};
}
