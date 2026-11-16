import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ChartCard} from './ChartCard';

describe('ChartCard', () => {
  it('shows its title, summary and content, and switches between chart and table', async () => {
    const onViewChange = vi.fn();
    const {user} = renderWithApp(
      <ChartCard title="Overdue by priority" summary="Of 5 overdue, 1 is urgent." view="chart" onViewChange={onViewChange}>
        <p>The chart</p>
      </ChartCard>,
    );

    expect(await screen.findByRole('heading', {name: 'Overdue by priority'})).toBeInTheDocument();
    expect(screen.getByText('Of 5 overdue, 1 is urgent.')).toBeInTheDocument();
    expect(screen.getByText('The chart')).toBeInTheDocument();
    const show = screen.getByRole('radiogroup', {name: 'Show Overdue by priority as'});
    expect(screen.getByRole('radio', {name: 'Chart'})).toBeChecked();

    await user.click(screen.getByRole('radio', {name: 'Table'}));

    expect(show).toBeInTheDocument();
    expect(onViewChange).toHaveBeenCalledWith('table');
  });
});
