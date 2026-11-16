import {formatFileSize} from './fileSize';

describe('formatFileSize', () => {
  it.each([
    [0, '0 bytes'],
    [1, '1 byte'],
    [1023, '1023 bytes'],
    [1024, '1 KB'],
    [1025, '2 KB'],
    [12 * 1024, '12 KB'],
    [1024 * 1024 - 1, '1023 KB'],
    [1024 * 1024, '1 MB'],
    [1258291, '1.2 MB'],
    [20 * 1024 * 1024, '20 MB'],
    [31.2 * 1024 * 1024, '31.2 MB'],
  ])('%d bytes is %s', (bytes, text) => {
    expect(formatFileSize(bytes)).toBe(text);
  });
});
