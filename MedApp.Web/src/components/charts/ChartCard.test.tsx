import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {CHART_CARD_HEIGHT, ChartCard, type ChartView} from './ChartCard';

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
    await user.click(screen.getByRole('button', {name: 'Show Table of Overdue by priority'}));

    expect(onViewChange).toHaveBeenCalledWith('table');
  });

  it('offers the chart again while showing the table', async () => {
    const onViewChange = vi.fn();
    const {user} = renderWithApp(
      <ChartCard title="Throughput" summary="Busy." view="table" onViewChange={onViewChange}>
        <p>The table</p>
      </ChartCard>,
    );

    await user.click(await screen.findByRole('button', {name: 'Show Chart of Throughput'}));

    expect(onViewChange).toHaveBeenCalledWith('chart');
  });

  it('keeps one height in both views', async () => {
    function Switching() {
      const [view, setView] = useState<ChartView>('chart');
      return (
        <ChartCard title="Throughput" summary="Busy." view={view} onViewChange={setView}>
          {view === 'chart' ? <p>The chart</p> : Array.from({length: 40}, (_, i) => <p key={i}>Row {i}</p>)}
        </ChartCard>
      );
    }
    const {user} = renderWithApp(<Switching />);
    const frame = async () => (await screen.findByRole('heading', {name: 'Throughput'})).closest(`[style*="height: ${CHART_CARD_HEIGHT}"]`);

    const asChart = await frame();
    expect(asChart).not.toBeNull();
    await user.click(screen.getByRole('button', {name: 'Show Table of Throughput'}));
    expect(await screen.findByText('Row 39')).toBeInTheDocument();

    // The same box, the same height: the table scrolls inside the card's body (PanelCard) instead of growing it.
    expect(await frame()).toBe(asChart);
  });
});
