import {ApiError, ApiValidationError, type ValidationErrors} from '../api/errors';

/** What the add and edit pages show for a failed save: field errors for a 400, otherwise one message. */
export interface SaveProblem {
  validation: ValidationErrors | null;
  message: string | null;
}

export const NO_PROBLEM: SaveProblem = {validation: null, message: null};

/** What's being saved, as the messages name it. */
export type SaveSubject =
  | 'patient'
  | 'pathway'
  | 'stage'
  | 'template'
  | 'rule'
  | 'activity type'
  | 'day template'
  | 'week template'
  | 'site'
  | 'room'
  | 'specialty';

/** The Blazor pages' messages for each kind of failure, naming what's being saved (a patient by default). */
export function saveProblem(error: unknown, action: 'save' | 'add' | 'delete' | 'archive' | 'restore', subject: SaveSubject = 'patient'): SaveProblem {
  const Subject = subject.charAt(0).toUpperCase() + subject.slice(1);
  const subjects = /[^aeiou]y$/.test(subject) ? `${subject.slice(0, -1)}ies` : `${subject}s`;
  if (error instanceof ApiValidationError) return {validation: error.errors, message: null};
  if (error instanceof ApiError) {
    switch (error.status) {
      case 401:
        return {validation: null, message: 'Your session has ended. Please sign in again.'};
      case 403:
        return {validation: null, message: `You do not have permission to change ${subjects}.`};
      case 404:
        return {validation: null, message: `${Subject} not found. It may have been deleted.`};
      case 409:
        // The API says why, e.g. "This patient has 2 pathways, so they can't be deleted."
        if (error.title) return {validation: null, message: error.title};
        break;
      default:
        if (error.status >= 500) return {validation: null, message: 'A server error occurred. Please try again later.'};
    }
  }
  if (error instanceof TypeError) {
    return {validation: null, message: 'Unable to connect to the server. Please check your connection and try again.'};
  }
  const what =
    action === 'add'
      ? `adding the ${subject}`
      : action === 'save'
        ? 'saving changes'
        : `${action === 'delete' ? 'deleting' : action === 'archive' ? 'archiving' : 'restoring'} the ${subject}`;
  return {validation: null, message: `An unexpected error occurred while ${what}.`};
}
