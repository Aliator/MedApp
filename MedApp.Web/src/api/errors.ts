import type {InputStatus} from '@astryxdesign/core/Field';

/**
 * What a 400 from the API says, in one shape. The API sends two kinds:
 * - `ValidationProblemDetails` (FluentValidation, via the exception middleware): `errors` keyed by property name.
 * - A list of ASP.NET Identity errors (`{code, description}`), from the account endpoints.
 */
export interface ValidationErrors {
  title: string;
  /** Messages keyed by field, in camelCase to match the request DTOs (`FirstName` → `firstName`). */
  fields: Record<string, string[]>;
  /** Messages that don't belong to a field. */
  general: string[];
}

const DEFAULT_TITLE = 'One or more validation errors occurred.';

/** Thrown by the API client for a 400, so callers can show the errors on their fields. */
export class ApiValidationError extends Error {
  readonly errors: ValidationErrors;

  constructor(errors: ValidationErrors) {
    super(errors.title);
    this.name = 'ApiValidationError';
    this.errors = errors;
  }
}

/** Thrown by the API client for any other unsuccessful response. */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message?: string) {
    super(message ?? `The request failed (${status}).`);
    this.name = 'ApiError';
    this.status = status;
  }
}

/** `Request.FirstName` or `FirstName` → `firstName`; an empty key is a general error. */
function toFieldKey(key: string): string {
  const last = key.split('.').pop() ?? '';
  return last.charAt(0).toLowerCase() + last.slice(1);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asMessages(value: unknown): string[] {
  if (Array.isArray(value)) return value.filter((m): m is string => typeof m === 'string');
  return typeof value === 'string' ? [value] : [];
}

/** Reads a 400 body (either shape above) into {@link ValidationErrors}. Anything unrecognised becomes an empty result with the default title. */
export function parseValidationErrors(body: unknown): ValidationErrors {
  const result: ValidationErrors = {title: DEFAULT_TITLE, fields: {}, general: []};

  if (Array.isArray(body)) {
    for (const item of body) {
      if (isRecord(item) && typeof item.description === 'string') result.general.push(item.description);
    }
    return result;
  }

  if (!isRecord(body)) return result;
  if (typeof body.title === 'string' && body.title) result.title = body.title;

  if (isRecord(body.errors)) {
    for (const [key, value] of Object.entries(body.errors)) {
      const messages = asMessages(value);
      if (messages.length === 0) continue;
      const field = toFieldKey(key);
      if (field) (result.fields[field] ??= []).push(...messages);
      else result.general.push(...messages);
    }
  }
  return result;
}

/** The error `status` for a field's input, or undefined when the field has no errors. */
export function fieldStatus(errors: ValidationErrors | null | undefined, field: string): InputStatus | undefined {
  const messages = errors?.fields[field];
  return messages?.length ? {type: 'error', message: messages.join(' ')} : undefined;
}

/** Every message, for a summary banner: field messages first, then general ones. */
export function allMessages(errors: ValidationErrors): string[] {
  return [...Object.values(errors.fields).flat(), ...errors.general];
}
