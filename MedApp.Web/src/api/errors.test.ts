import {
  ApiError,
  ApiValidationError,
  allMessages,
  fieldStatus,
  otherMessages,
  parseValidationErrors,
  problemCode,
  problemMessage,
  problemTitle,
  refusalMessage,
  soleFieldStatus,
} from './errors';

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

  it('keeps each Identity error with its code, so a page can tell them apart', () => {
    const errors = parseValidationErrors([
      {code: 'InvalidToken', description: 'This link has expired or has already been used.'},
      {description: 'No code.'},
      {code: 'Ignored'},
    ]);

    expect(errors.identity).toEqual([
      {code: 'InvalidToken', description: 'This link has expired or has already been used.'},
      {code: '', description: 'No code.'},
    ]);
    expect(errors.general).toEqual(['This link has expired or has already been used.', 'No code.']);
    expect(parseValidationErrors({errors: {}}).identity).toBeUndefined();
  });

  it.each([null, undefined, 'Bad Request', 42])('returns an empty result with the default title for %s', (body) => {
    expect(parseValidationErrors(body)).toEqual({title: 'One or more validation errors occurred.', fields: {}, general: []});
  });

  it('ignores values that are not messages', () => {
    const errors = parseValidationErrors({errors: {FirstName: [1, 'First name must not be empty.'], LastName: null}});

    expect(errors.fields).toEqual({firstName: ['First name must not be empty.']});
  });

  it('reads a single message that is not in a list', () => {
    expect(parseValidationErrors({errors: {Email: 'Email is not valid.'}}).fields).toEqual({email: ['Email is not valid.']});
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

describe('problemCode', () => {
  it('reads the code of a ProblemDetails body', () => {
    expect(problemCode({title: 'Password change required', status: 403, code: 'password_change_required'})).toBe('password_change_required');
  });

  it('is undefined for anything else', () => {
    expect(problemCode(undefined)).toBeUndefined();
    expect(problemCode({title: 'Forbidden'})).toBeUndefined();
    expect(problemCode({code: ''})).toBeUndefined();
    expect(problemCode({code: 7})).toBeUndefined();
    expect(problemCode([{code: 'x'}])).toBeUndefined();
  });
});

describe('problemMessage', () => {
  it("gives the API's reason for a conflict or a missing record", () => {
    expect(problemMessage(new ApiError(409, undefined, 'Finish the Consent stage first.'), 'Failed.')).toBe('Finish the Consent stage first.');
    expect(problemMessage(new ApiError(404, undefined, 'Pathway not found.'), 'Failed.')).toBe('Pathway not found.');
  });

  it('gives the validation messages for a 400', () => {
    const error = new ApiValidationError(parseValidationErrors({errors: {Reason: ['A reason is required.']}}));

    expect(problemMessage(error, 'Failed.')).toBe('A reason is required.');
  });

  it("gives a 400's title when it has no messages", () => {
    const error = new ApiValidationError(parseValidationErrors({title: 'The request was not valid.'}));

    expect(problemMessage(error, 'Failed.')).toBe('The request was not valid.');
  });

  it('falls back for anything else', () => {
    expect(problemMessage(new ApiError(500, undefined, 'Server error'), 'Failed.')).toBe('Failed.');
    expect(problemMessage(new TypeError('offline'), 'Failed.')).toBe('Failed.');
  });
});

describe('soleFieldStatus', () => {
  it("gives the field its own messages, or else every message, so a one-field form shows all the API said", () => {
    const own = parseValidationErrors({errors: {Code: ['That code is not right.'], Challenge: ['Ignored here.']}});
    const other = parseValidationErrors({errors: {Challenge: ['The sign-in is missing.']}});
    const identity = parseValidationErrors([{code: 'OwnTwoFactor', description: 'Not your own.'}]);

    expect(soleFieldStatus(own, 'code')).toEqual({type: 'error', message: 'That code is not right.'});
    expect(soleFieldStatus(other, 'code')).toEqual({type: 'error', message: 'The sign-in is missing.'});
    expect(soleFieldStatus(identity, 'code')).toEqual({type: 'error', message: 'Not your own.'});
    expect(soleFieldStatus(parseValidationErrors({}), 'code')).toBeUndefined();
    expect(soleFieldStatus(null, 'code')).toBeUndefined();
  });
});

describe('refusalMessage', () => {
  it.each([409, 423, 429])("gives the API's words for a %s", (status) => {
    expect(refusalMessage(new ApiError(status, undefined, 'Try again in 5 minutes.'), 'Failed.')).toBe('Try again in 5 minutes.');
  });

  it('falls back for one without a title, another status, or anything else', () => {
    expect(refusalMessage(new ApiError(429), 'Failed.')).toBe('Failed.');
    expect(refusalMessage(new ApiError(500, undefined, 'Server error'), 'Failed.')).toBe('Failed.');
    expect(refusalMessage(new TypeError('offline'), 'Failed.')).toBe('Failed.');
  });
});

describe('otherMessages', () => {
  it("lists the messages that aren't on the given fields, then the general ones", () => {
    const errors = parseValidationErrors({errors: {Password: ['Wrong.'], Challenge: ['Missing.'], '': ['Something else.']}});

    expect(otherMessages(errors, ['password', 'code'])).toEqual(['Missing.', 'Something else.']);
    expect(otherMessages(null, ['password'])).toEqual([]);
  });
});
