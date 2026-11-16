const KB = 1024;
const MB = 1024 * KB;

/** A number to one decimal place, without a trailing ".0": 1.25 → "1.3", 20 → "20". */
const oneDecimal = (value: number) => {
  const text = value.toFixed(1);
  return text.endsWith('.0') ? text.slice(0, -2) : text;
};

/**
 * A file's size as people read it: "512 bytes", "12 KB", "1.2 MB". Counted in 1024s, as the API's limit is (its
 * 20 MB is 20 × 1024 × 1024 bytes), so a file at the limit says "20 MB".
 */
export function formatFileSize(bytes: number): string {
  if (bytes < KB) return `${bytes} ${bytes === 1 ? 'byte' : 'bytes'}`;
  // Rounded up to whole KB, so a file just over 1 KB never reads as "1 KB" beside a limit of 1 KB.
  if (bytes < MB) return `${Math.min(Math.ceil(bytes / KB), 1023)} KB`;
  return `${oneDecimal(bytes / MB)} MB`;
}
