import {statusLabel, statusWords} from './statusWords';

describe('statusWords', () => {
  it.each([
    ['Unassigned', 'unassigned'],
    ['Assigned', 'assigned'],
    ['InProgress', 'in progress'],
    ['Cancelled', 'cancelled'],
    ['Archived', 'archived'],
    ['Urgent', 'urgent'],
    ['', ''],
  ])('puts %s in lower-case words: %s', (value, words) => {
    expect(statusWords(value)).toBe(words);
  });

  it.each([
    ['InReview', 'awaiting review'],
    ['Blocked', 'on hold'],
    ['Completed', 'completed'],
  ])('shows %s by the name people see: %s', (value, words) => {
    expect(statusWords(value)).toBe(words);
  });
});

describe('statusLabel', () => {
  it.each([
    ['InProgress', 'In progress'],
    ['InReview', 'Awaiting review'],
    ['Blocked', 'On hold'],
    ['Completed', 'Completed'],
    ['Cancelled', 'Cancelled'],
    ['', ''],
  ])('labels %s as %s', (status, label) => {
    expect(statusLabel(status)).toBe(label);
  });
});
