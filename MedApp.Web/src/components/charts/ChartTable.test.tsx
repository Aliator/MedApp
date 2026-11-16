import {screen, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ChartTable} from './ChartTable';

const rows = [
  {name: 'k.ashford', count: 3, hours: 1.5},
  {name: 'Nobody', count: 2, hours: 0},
];

describe('ChartTable', () => {
  it("lists each row's numbers under its series, with its name linking where its bar does", async () => {
    renderWithApp(
      <ChartTable
        data={rows}
        categoryKey="name"
        categoryHeader="Assigned to"
        series={[{key: 'count', label: 'Overdue'}, {key: 'hours', label: 'Time', format: (v) => `${v} h`}]}
        hrefFor={(row) => (row.name === 'Nobody' ? undefined : `/procedures?assignee=${row.name}`)}
        label="Overdue by assignee"
      />,
    );

    const table = await screen.findByRole('table', {name: 'Overdue by assignee'});
    expect(within(table).getByRole('columnheader', {name: 'Assigned to'})).toBeInTheDocument();
    expect(within(table).getByRole('link', {name: 'k.ashford'})).toHaveAttribute('href', '/procedures?assignee=k.ashford');
    // A row with nowhere to go is plain text.
    expect(within(table).getByText('Nobody').closest('a')).toBeNull();
    expect(within(table).getByText('1.5 h')).toBeInTheDocument();
    expect(within(table).getByText('3')).toBeInTheDocument();
  });

  it('formats categories and values, and shows missing values as nought', async () => {
    renderWithApp(
      <ChartTable
        data={[{week: '2026-11-02'}]}
        categoryKey="week"
        categoryHeader="Week of"
        series={[{key: 'created', label: 'Created'}]}
        formatCategory={(w) => `Week ${w}`}
        formatValue={(v) => `${v} made`}
        label="Throughput"
      />,
    );

    expect(await screen.findByText('Week 2026-11-02')).toBeInTheDocument();
    expect(screen.getByText('0 made')).toBeInTheDocument();
  });
});
