import {render, screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {BarChart, ChartLegend, ChartTooltip, type ChartSeries} from './BarChart';
import {SERIES_COLOURS} from './chartColours';
import {givenWidth} from '../../test/layout';

const ONE: ChartSeries[] = [{key: 'count', label: 'Overdue', colour: SERIES_COLOURS[0]}];
const TWO: ChartSeries[] = [
  {key: 'created', label: 'Created', colour: SERIES_COLOURS[0]},
  {key: 'completed', label: 'Completed', colour: SERIES_COLOURS[1]},
];
const people = [
  {name: 'k.ashford', count: 3},
  {name: 'A rather long name for a person or a stage', count: 1},
];
const weeks = [
  {week: '2026-11-02', created: 4, completed: 2},
  {week: '2026-11-09', created: 3, completed: 5},
];

const bars = (container: HTMLElement) => container.querySelectorAll('.recharts-bar-rectangle');

describe('BarChart', () => {
  it('draws a bar per item, with its value at its end, as one image named by its description', () => {
    givenWidth();
    const {container} = render(<BarChart data={people} categoryKey="name" series={ONE} description="Overdue by assignee." />);

    expect(screen.getByRole('img', {name: 'Overdue by assignee.'})).toBeInTheDocument();
    expect(bars(container)).toHaveLength(2);
    expect(container.querySelector('.recharts-label-list')).toHaveTextContent('3');
    // Long names are shortened beside their bars (the table has them in full).
    expect(container).toHaveTextContent('A rather long nam…');
    // One series needs no legend: the card's title names it.
    expect(screen.queryByText('Overdue')).not.toBeInTheDocument();
  });

  it('opens what a bar counts when it is clicked', async () => {
    givenWidth();
    const onSelect = vi.fn();
    const {container} = render(<BarChart data={people} categoryKey="name" series={ONE} onSelect={onSelect} description="Chart" />);

    await userEvent.click(bars(container)[1].querySelector('path') ?? bars(container)[1]);

    expect(onSelect).toHaveBeenCalledWith(people[1]);
  });

  it('draws columns for each series, with a legend, its categories and values in words', () => {
    givenWidth();
    const {container} = render(
      <BarChart
        data={weeks}
        categoryKey="week"
        series={TWO}
        layout="columns"
        formatCategory={(w) => `w/c ${w.slice(5)}`}
        formatValue={(v) => `${v}!`}
        description="Throughput"
      />,
    );

    expect(bars(container)).toHaveLength(4);
    expect(container).toHaveTextContent('w/c 11-02');
    expect(container).toHaveTextContent('4!');
    expect(screen.getByText('Created')).toBeInTheDocument();
    expect(screen.getByText('Completed')).toBeInTheDocument();
  });

  it('stacks the series into one bar per item', () => {
    givenWidth();
    const {container} = render(<BarChart data={weeks} categoryKey="week" series={TWO} isStacked description="Stacked" />);

    expect(bars(container)).toHaveLength(4);
    expect(container.querySelectorAll('.recharts-bar')).toHaveLength(2);
  });

  it('draws nothing until it knows its width (as on the server), keeping its place', () => {
    vi.stubGlobal('ResizeObserver', undefined);
    const {container} = render(<BarChart data={people} categoryKey="name" series={ONE} description="Chart" />);

    expect(screen.getByRole('img', {name: 'Chart'})).toBeInTheDocument();
    expect(container.querySelector('svg.recharts-surface')).toBeNull();
  });
});

describe('ChartTooltip', () => {
  it('shows the category and each series value beside its colour', () => {
    render(
      <ChartTooltip
        active
        label="2026-11-02"
        payload={[{name: 'Created', value: 4, color: SERIES_COLOURS[0]}, {name: 'Completed', value: 2}]}
        formatCategory={(c) => `Week of ${c}`}
        formatValue={(v) => `${v} procedures`}
      />,
    );

    expect(screen.getByText('Week of 2026-11-02')).toBeInTheDocument();
    expect(screen.getByText('Created: 4 procedures')).toBeInTheDocument();
    expect(screen.getByText('Completed: 2 procedures')).toBeInTheDocument();
  });

  it('shows plain values and categories by default', () => {
    render(<ChartTooltip active label={7} payload={[{name: 'Overdue', value: 3}]} />);

    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('Overdue: 3')).toBeInTheDocument();
  });

  it('shows nothing away from a bar', () => {
    const {container} = render(<ChartTooltip active={false} payload={[{name: 'Overdue', value: 3}]} />);
    render(<ChartTooltip active payload={[]} />);

    expect(container).toBeEmptyDOMElement();
  });
});

describe('ChartLegend', () => {
  it('names each series', () => {
    render(<ChartLegend series={TWO} />);

    expect(screen.getByText('Created')).toBeInTheDocument();
    expect(screen.getByText('Completed')).toBeInTheDocument();
  });
});
