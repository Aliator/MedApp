import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, json, mockApi, renderWithApp} from '../../test/render';
import {insights, STAFF_ID, TEMPLATE_ID, templates} from '../../test/insights';
import type {InsightsSearch} from '../listSearch';
import {InsightsPage} from './InsightsPage';
import {givenWidth} from '../../test/layout';

const INSIGHTS = '/api/procedures/insights';
const TEMPLATES = '/api/procedures/patient-procedure-stage/templates';

function renderInsights(search: InsightsSearch = {}, body: unknown = insights, status = 200) {
  const fetch = mockApi({[`GET ${INSIGHTS}`]: () => json(body, status), [`GET ${TEMPLATES}`]: () => json(templates)});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<InsightsPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const card = (title: string) => screen.getByRole('heading', {name: title}).closest<HTMLElement>('.astryx-card')!;

describe('InsightsPage', () => {
  it('shows each headline count against this time last week, linking to the procedures it counts', async () => {
    const {fetch} = renderInsights();

    expect(await screen.findByRole('link', {name: 'Open: 14. 2 more than this time last week.'})).toHaveAttribute('href', '/procedures');
    expect(screen.getByRole('link', {name: 'In progress: 6. Same as this time last week.'})).toHaveAttribute(
      'href',
      '/procedures?status=InProgress',
    );
    expect(screen.getByRole('link', {name: 'Overdue: 5. 2 fewer than this time last week.'})).toHaveAttribute('href', '/procedures?overdue=true');
    expect(screen.getByRole('link', {name: 'Completed this week: 3. 2 more than this time last week.'})).toHaveAttribute(
      'href',
      '/procedures?status=Completed',
    );
    expect(screen.getByText('31 Aug 2026 to 16 Nov 2026, all templates')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=12`);
  });

  it('sums up each chart, and draws them as images named by their summaries', async () => {
    givenWidth();
    renderInsights();

    expect(await screen.findByText('5 procedures are overdue. k.ashford has the most (3). 2 have nobody assigned.')).toBeInTheDocument();
    expect(screen.getByRole('img', {name: 'Bar chart. Of 5 overdue, 1 is urgent and 2 high priority.'})).toBeInTheDocument();
    expect(screen.getByText(/^Over 3 weeks, 8 procedures created and 10 completed/)).toBeInTheDocument();
    expect(screen.getByText('The longest is Procedure: 2 d 6 h on average, 2 d 2 h of it waiting to be started.')).toBeInTheDocument();
    expect(screen.getByRole('img', {name: /^Stacked bar chart of the average time at each stage of Knee review/})).toBeInTheDocument();
  });

  it('opens the overdue procedures an assignee bar counts', async () => {
    givenWidth();
    const {user, router} = renderInsights();

    const chart = await screen.findByRole('img', {name: /^Bar chart\. 5 procedures are overdue/});
    await waitFor(() => expect(chart.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(2));
    await user.click(chart.querySelectorAll('.recharts-bar-rectangle path')[1]);

    await waitFor(() => expect(router.state.location.href).toBe('/procedures?overdue=true&assignee=none'));
  });

  it('opens the overdue procedures of a priority from its bar', async () => {
    givenWidth();
    const {user, router} = renderInsights();

    const chart = await screen.findByRole('img', {name: /^Bar chart\. Of 5 overdue/});
    await waitFor(() => expect(chart.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(3));
    await user.click(chart.querySelectorAll('.recharts-bar-rectangle path')[0]);

    await waitFor(() => expect(router.state.location.href).toBe('/procedures?overdue=true&priority=Urgent'));
  });

  it("shows each chart's numbers as a table, whose rows link where the bars do", async () => {
    const {user} = renderInsights();

    await screen.findByRole('heading', {name: 'Overdue by assignee'});
    for (const title of ['Overdue by assignee', 'Overdue by priority', 'Created and completed', 'Time at each stage']) {
      await user.click(within(card(title)).getByRole('button', {name: 'Show Table'}));
    }

    const assignees = screen.getByRole('table', {name: 'Overdue procedures by assignee'});
    expect(within(assignees).getByRole('link', {name: 'k.ashford'})).toHaveAttribute('href', `/procedures?overdue=true&assignee=${STAFF_ID}`);
    expect(within(assignees).getByRole('link', {name: 'Nobody'})).toHaveAttribute('href', '/procedures?overdue=true&assignee=none');
    const priorities = screen.getByRole('table', {name: 'Overdue procedures by priority'});
    expect(within(priorities).getByRole('link', {name: 'High'})).toHaveAttribute('href', '/procedures?overdue=true&priority=High');
    const weeks = screen.getByRole('table', {name: 'Procedures created and completed each week'});
    expect(within(weeks).getByText('9 Nov 2026')).toBeInTheDocument();
    const stages = screen.getByRole('table', {name: 'Average time at each stage of Knee review'});
    const procedure = within(stages).getByText('2. Procedure').closest('tr')!;
    expect(within(procedure).getByText('2 d 2 h')).toBeInTheDocument();
    expect(within(procedure).getByText('4 h')).toBeInTheDocument();
    expect(within(procedure).getByText('3')).toBeInTheDocument();
  });

  it('changes the period, keeping 12 weeks (the default) out of the URL', async () => {
    const {user, onSearchChange} = renderInsights({weeks: 4, template: TEMPLATE_ID});

    await user.click(await screen.findByRole('combobox', {name: /Period/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['Last 4 weeks', 'Last 12 weeks', 'Last 26 weeks']);
    await user.click(screen.getByRole('option', {name: 'Last 26 weeks'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({weeks: 26, template: TEMPLATE_ID});
    await user.click(screen.getByRole('combobox', {name: /Period/}));
    await user.click(screen.getByRole('option', {name: 'Last 12 weeks'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({weeks: undefined, template: TEMPLATE_ID});
  });

  it('chooses a template (archived ones too), or all of them', async () => {
    const {user, onSearchChange} = renderInsights();

    await screen.findByText('31 Aug 2026 to 16 Nov 2026, all templates');
    await user.click(screen.getByRole('combobox', {name: /Template/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All templates', 'Allergy check (archived)', 'Knee review']);
    await user.click(screen.getByRole('option', {name: 'Knee review'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({template: TEMPLATE_ID});
  });

  it('on a template, asks for its insights, names it, and links to its procedures', async () => {
    const {fetch, user, onSearchChange} = renderInsights({weeks: 26, template: TEMPLATE_ID});

    expect(await screen.findByText('31 Aug 2026 to 16 Nov 2026, Knee review')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=26&TemplateId=${TEMPLATE_ID}`);
    expect(screen.getByRole('link', {name: /^Overdue: 5/})).toHaveAttribute('href', '/procedures?overdue=true&template=Knee+review');

    await user.click(screen.getByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'All templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({weeks: 26, template: undefined});
  });

  it("gives each template's stages a chart of their own when there are several", async () => {
    const several = {
      ...insights,
      stageTimes: [
        {templateId: null, templateName: '', stages: [{stageOrder: 1, stageName: 'Old stage', averageWorkingHours: 1}]},
        {templateId: null, templateName: 'Knee review', stages: []},
        ...insights.stageTimes!,
      ],
    };
    renderInsights({}, several);

    expect(await screen.findByRole('heading', {name: 'Procedures (no template)'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Knee review (no template)'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Knee review'})).toBeInTheDocument();
  });

  it('says when nothing is overdue or finished, with nothing to chart', async () => {
    givenWidth();
    const quiet = {...insights, overdue: {current: 0, lastWeek: 0}, overdueByAssignee: [], stageTimes: []};
    renderInsights({}, quiet);

    expect(await screen.findAllByText('Nothing is overdue.')).toHaveLength(2);
    expect(screen.getByText('No stages were finished in this period.')).toBeInTheDocument();
    expect(screen.queryByRole('img', {name: /^Bar chart\. Nothing is overdue/})).toBeInTheDocument();
    expect(screen.getAllByRole('img', {name: /^Bar chart\./})).toHaveLength(1);
  });

  it('says when the insights could not be loaded', async () => {
    renderInsights({}, {title: 'Boom'}, 500);

    expect(await screen.findByText('The insights could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
