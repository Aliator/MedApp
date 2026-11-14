import {allMessages, fieldStatus, parseValidationErrors, problemTitle} from './errors';

describe('parseValidationErrors', () => {
  it('maps ValidationProblemDetails errors onto camelCase fields', () => {
    const errors = parseValidationErrors({
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {FirstName: ['First name must not be empty.'], DateOfBirth: ['Date of birth must be provided.']},
    });

    expect(errors.fields).toEqual({
      firstName: ['First name must not be empty.'],
      dateOfBirth: ['Date of birth must be provided.'],
    });
    expect(errors.general).toEqual([]);
  });

  it('uses the last segment of a nested property name', () => {
    const errors = parseValidationErrors({errors: {'Request.Email': ['Email must be a valid email address.']}});

    expect(errors.fields.email).toEqual(['Email must be a valid email address.']);
  });

  it('merges messages for keys that map to the same field', () => {
    const errors = parseValidationErrors({
      errors: {Email: ['Email must not be empty.'], 'Request.Email': ['Email must be a valid email address.']},
    });

    expect(errors.fields.email).toEqual(['Email must not be empty.', 'Email must be a valid email address.']);
  });

  it('treats an empty key as a general error', () => {
    const errors = parseValidationErrors({errors: {'': ['Something is wrong with the request.']}});

    expect(errors.fields).toEqual({});
    expect(errors.general).toEqual(['Something is wrong with the request.']);
  });

  it('keeps the title from the response', () => {
    expect(parseValidationErrors({title: 'Custom title', errors: {}}).title).toBe('Custom title');
  });

  it('reads a list of Identity errors as general errors', () => {
    const errors = parseValidationErrors([
      {code: 'PasswordTooShort', description: 'Passwords must be at least 8 characters.'},
      {code: 'PasswordRequiresDigit', description: 'Passwords must have at least one digit.'},
    ]);

    expect(errors.fields).toEqual({});
    expect(errors.general).toEqual(['Passwords must be at least 8 characters.', 'Passwords must have at least one digit.']);
  });

  it.each([null, undefined, 'Bad Request', 42])('returns an empty result with the default title for %s', (body) => {
    expect(parseValidationErrors(body)).toEqual({title: 'One or more validation errors occurred.', fields: {}, general: []});
  });

  it('ignores values that are not messages', () => {
    const errors = parseValidationErrors({errors: {FirstName: [1, 'First name must not be empty.'], LastName: null}});

    expect(errors.fields).toEqual({firstName: ['First name must not be empty.']});
  });
});

describe('fieldStatus', () => {
  const errors = parseValidationErrors({errors: {Username: ['Username must not be empty.', 'Username is too short.']}});

  it('returns an error status with every message for the field', () => {
    expect(fieldStatus(errors, 'username')).toEqual({
      type: 'error',
      message: 'Username must not be empty. Username is too short.',
    });
  });

  it('returns undefined for a field without errors, or when there are no errors', () => {
    expect(fieldStatus(errors, 'password')).toBeUndefined();
    expect(fieldStatus(null, 'username')).toBeUndefined();
  });
});

describe('allMessages', () => {
  it('lists field messages before general ones', () => {
    const errors = parseValidationErrors({errors: {'': ['General problem.'], Email: ['Email must not be empty.']}});

    expect(allMessages(errors)).toEqual(['Email must not be empty.', 'General problem.']);
  });
});

describe('problemTitle', () => {
  it('reads the title of a ProblemDetails body', () => {
    expect(problemTitle({title: 'Conflict reason.', status: 409})).toBe('Conflict reason.');
  });

  it('is undefined for anything else', () => {
    expect(problemTitle(undefined)).toBeUndefined();
    expect(problemTitle('text')).toBeUndefined();
    expect(problemTitle({title: ''})).toBeUndefined();
    expect(problemTitle([{title: 'x'}])).toBeUndefined();
  });
});
