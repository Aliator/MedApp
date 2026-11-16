import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {describeChange, StatTile} from './StatTile';

describe('StatTile', () => {
  it('shows the count and how it compares, as a link to what it counts', async () => {
    renderWithApp(<StatTile label="Overdue" value={5} previous={7} previousLabel="this time last week" isRiseGood={false} href="/procedures?overdue=true" />);

    const link = await screen.findByRole('link', {name: 'Overdue: 5. 2 fewer than this time last week.'});
    expect(link).toHaveAttribute('href', '/procedures?overdue=true');
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('2 fewer than this time last week')).toBeInTheDocument();
  });

  it.each([
    [3, 1, true, 'success'],
    [3, 1, false, 'error'],
    [1, 3, true, 'error'],
    [3, 1, undefined, 'secondary'],
    [2, 2, true, 'secondary'],
  ])('colours the arrow for %i against %i (a rise good: %s) as %s', async (value, previous, isRiseGood, colour) => {
    const {container} = renderWithApp(<StatTile label="Completed" value={value} previous={previous} previousLabel="last week" isRiseGood={isRiseGood} />);

    await screen.findByText('Completed');
    expect(container.querySelector(`.astryx-icon[data-color="${colour}"]`)).not.toBeNull();
  });
});

describe('describeChange', () => {
  it('says how much more, fewer, or the same', () => {
    expect(describeChange(5, 2, 'last week')).toBe('3 more than last week');
    expect(describeChange(1, 2, 'last week')).toBe('1 fewer than last week');
    expect(describeChange(2, 2, 'last week')).toBe('Same as last week');
  });
});
