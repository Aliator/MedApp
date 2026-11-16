import {safeRedirectPath} from './redirect';

describe('safeRedirectPath', () => {
  it.each([
    ['/confirm-email', '/confirm-email'],
    ['/patients?page=2', '/patients?page=2'],
    ['/patients#section', '/patients'],
    ['/a/../confirm-email', '/confirm-email'],
  ])('keeps a path in this app: %s', (value, expected) => {
    expect(safeRedirectPath(value)).toBe(expected);
  });

  it.each([
    ['another site', 'https://example.org/'],
    ['a protocol-relative address', '//example.org/'],
    ['a backslash browsers read as a slash', '/\\example.org/'],
    ['a backslash later on', '/patients\\x'],
    ['a tab browsers drop', '/\t/example.org/'],
    ['a newline', '/patients\n'],
    ['a relative path', 'confirm-email'],
    ['a script', 'javascript:alert(1)'],
    ['an empty string', ''],
    ['something not a string', 42],
    ['nothing', undefined],
  ])('refuses %s', (_, value) => {
    expect(safeRedirectPath(value)).toBeUndefined();
  });
});
