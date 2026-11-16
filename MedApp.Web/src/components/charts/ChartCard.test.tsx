import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {CHART_CARD_HEIGHT, ChartCard} from './ChartCard';

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

  it('keeps one height whichever it shows, so a long table scrolls inside the card rather than lengthening the page', async () => {
    renderWithApp(
      <>
        <ChartCard title="As a chart" summary="Busy." view="chart" onViewChange={() => {}}>
          <p>The chart</p>
        </ChartCard>
        <ChartCard title="As a table" summary="Busy." view="table" onViewChange={() => {}}>
          {Array.from({length: 40}, (_, i) => (
            <p key={i}>Row {i}</p>
          ))}
        </ChartCard>
      </>,
    );

    const heightOf = async (title: string) =>
      ((await screen.findByRole('heading', {name: title})).closest(`[style*="height: ${CHART_CARD_HEIGHT}px"]`) as HTMLElement | null)?.style.height;
    expect(await heightOf('As a chart')).toBe(`${CHART_CARD_HEIGHT}px`);
    expect(await heightOf('As a table')).toBe(`${CHART_CARD_HEIGHT}px`);
  });
});
