import {screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {adminUser, calls, json, mockApi, renderWithApp} from '../../test/render';
import {insights, OTHER_TEAM_ID, teams, TEAM_ID, STAFF_ID, TEMPLATE_ID, templates} from '../../test/insights';
import type {InsightsSearch} from '../listSearch';
import {InsightsPage} from './InsightsPage';
import {givenWidth} from '../../test/layout';

const INSIGHTS = '/api/pathways/insights';
const TEMPLATES = '/api/pathways/patient-pathway-stage/templates';
const TEAMS = '/api/teams';

/** Answers with `body`, for the template and team asked for (as the API says which its numbers are for). */
function renderInsights(search: InsightsSearch = {}, body: unknown = insights, status = 200) {
  const fetch = mockApi({
    [`GET ${INSIGHTS}`]: (request) => {
      const query = new URL(request.url).searchParams;
      return json(
        status === 200 ? {...(body as object), templateId: query.get('TemplateId'), teamId: query.get('TeamId')} : body,
        status,
      );
    },
    [`GET ${TEMPLATES}`]: () => json(templates),
    [`GET ${TEAMS}`]: () => json(teams),
  });
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<InsightsPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const card = (title: string) => screen.getByRole('heading', {name: title}).closest<HTMLElement>('.astryx-card')!;

describe('InsightsPage', () => {
  it('shows each headline count against this time last week, linking to the pathways it counts', async () => {
    const {fetch} = renderInsights();

    expect(await screen.findByRole('link', {name: 'Open: 14. 2 more than this time last week.'})).toHaveAttribute('href', '/pathways?mine=false');
    expect(screen.getByRole('link', {name: 'In progress: 6. Same as this time last week.'})).toHaveAttribute(
      'href',
      '/pathways?status=InProgress&mine=false',
    );
    expect(screen.getByRole('link', {name: 'Overdue: 5. 2 fewer than this time last week.'})).toHaveAttribute('href', '/pathways?overdue=true&mine=false');
    // No list shows what was completed when, so that tile isn't a link.
    expect(screen.getByText('Discharged this week')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: /^Discharged this week/})).not.toBeInTheDocument();
    expect(screen.getByText('31 Aug 2026 to 16 Nov 2026, all templates')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=12`);
  });

  it('keeps its links on everyone when Account Settings starts lists on Assigned to me', async () => {
    localStorage.setItem('medapp.proceduresStartOnMine', 'true');
    renderInsights();

    expect(await screen.findByRole('link', {name: /^Overdue: 5/})).toHaveAttribute('href', '/pathways?overdue=true&mine=false');
  });

  it('says it is updating, and dims the last numbers, while another template loads', async () => {
    let answer: (response: Response) => void = () => {};
    const fetch = mockApi({
      [`GET ${INSIGHTS}`]: (request) =>
        new URL(request.url).searchParams.get('TemplateId')
          ? new Promise<Response>((resolve) => (answer = resolve))
          : json({...insights, templateId: null}),
      [`GET ${TEMPLATES}`]: () => json(templates),
    });
    function Switcher() {
      const [search, setSearch] = useState<InsightsSearch>({});
      return (
        <>
          <button onClick={() => setSearch({template: TEMPLATE_ID})}>Choose the template</button>
          <InsightsPage search={search} onSearchChange={setSearch} />
        </>
      );
    }
    const {user} = renderWithApp(<Switcher />, {user: adminUser});
    await screen.findByText('31 Aug 2026 to 16 Nov 2026, all templates');

    await user.click(screen.getByRole('button', {name: 'Choose the template'}));

    expect(await screen.findByText('Updating...')).toBeInTheDocument();
    expect(document.querySelector('[aria-label="Insights"]')).toHaveAttribute('aria-busy', 'true');
    // The numbers on screen are still everyone's, so the links are too.
    expect(screen.getByRole('link', {name: /^Overdue: 5/})).toHaveAttribute('href', '/pathways?overdue=true&mine=false');
    answer(json({...insights, templateId: TEMPLATE_ID}));
    expect(await screen.findByText('31 Aug 2026 to 16 Nov 2026, Knee review')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=12&TemplateId=${TEMPLATE_ID}`);
  });

  it('sums up each chart, and draws them as images named by their summaries', async () => {
    givenWidth();
    renderInsights();

    expect(await screen.findByText('5 pathways are overdue. k.ashford has the most (3). 2 have nobody assigned.')).toBeInTheDocument();
    expect(screen.getByRole('img', {name: 'Bar chart. Of 5 overdue, 1 is urgent and 2 high priority.'})).toBeInTheDocument();
    expect(screen.getByText(/^Over 3 weeks, 8 pathways created and 10 discharged/)).toBeInTheDocument();
    expect(screen.getByText('The longest is Procedure: 2 d 6 h on average, 2 d 2 h of it waiting to be started.')).toBeInTheDocument();
    expect(screen.getByRole('img', {name: /^Stacked bar chart of the average time at each stage of Knee review/})).toBeInTheDocument();
  });

  it('opens the overdue pathways an assignee bar counts', async () => {
    givenWidth();
    const {user, router} = renderInsights();

    const chart = await screen.findByRole('img', {name: /^Bar chart\. 5 pathways are overdue/});
    await waitFor(() => expect(chart.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(2));
    await user.click(chart.querySelectorAll('.recharts-bar-rectangle path')[1]);

    await waitFor(() => expect(router.state.location.href).toBe('/pathways?overdue=true&assignee=none&mine=false'));
  });

  it('opens the overdue pathways of a priority from its bar', async () => {
    givenWidth();
    const {user, router} = renderInsights();

    const chart = await screen.findByRole('img', {name: /^Bar chart\. Of 5 overdue/});
    await waitFor(() => expect(chart.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(3));
    await user.click(chart.querySelectorAll('.recharts-bar-rectangle path')[0]);

    await waitFor(() => expect(router.state.location.href).toBe('/pathways?overdue=true&priority=Urgent&mine=false'));
  });

  it("shows each chart's numbers as a table, whose rows link where the bars do", async () => {
    const {user} = renderInsights();

    await screen.findByRole('heading', {name: 'Overdue by assignee'});
    for (const title of ['Overdue by assignee', 'Overdue by priority', 'Created and discharged', 'Time at each stage']) {
      await user.click(within(card(title)).getByRole('button', {name: `Show Table of ${title}`}));
    }

    const assignees = screen.getByRole('table', {name: 'Overdue pathways by assignee'});
    expect(within(assignees).getByRole('link', {name: 'k.ashford'})).toHaveAttribute('href', `/pathways?overdue=true&assignee=${STAFF_ID}&mine=false`);
    expect(within(assignees).getByRole('link', {name: 'Nobody'})).toHaveAttribute('href', '/pathways?overdue=true&assignee=none&mine=false');
    const priorities = screen.getByRole('table', {name: 'Overdue pathways by priority'});
    expect(within(priorities).getByRole('link', {name: 'High'})).toHaveAttribute('href', '/pathways?overdue=true&priority=High&mine=false');
    const weeks = screen.getByRole('table', {name: 'Pathways created and discharged each week'});
    expect(within(weeks).getByText('9 Nov 2026')).toBeInTheDocument();
    const stages = screen.getByRole('table', {name: 'Average time at each stage of Knee review'});
    const pathway = within(stages).getByText('2. Procedure').closest('tr')!;
    expect(within(pathway).getByText('2 d 2 h')).toBeInTheDocument();
    expect(within(pathway).getByText('4 h')).toBeInTheDocument();
    expect(within(pathway).getByText('3')).toBeInTheDocument();
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

  it('on a template, asks for its insights, names it, and links to its pathways', async () => {
    const {fetch, user, onSearchChange} = renderInsights({weeks: 26, template: TEMPLATE_ID});

    expect(await screen.findByText('31 Aug 2026 to 16 Nov 2026, Knee review')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=26&TemplateId=${TEMPLATE_ID}`);
    expect(screen.getByRole('link', {name: /^Overdue: 5/})).toHaveAttribute('href', `/pathways?overdue=true&mine=false&templateId=${TEMPLATE_ID}`);

    await user.click(screen.getByRole('combobox', {name: /Template/}));
    await user.click(screen.getByRole('option', {name: 'All templates'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({weeks: 26, template: undefined});
  });

  it('on a team, asks for its insights, names it, links to its pathways and offers only its templates', async () => {
    const {fetch, user} = renderInsights({team: TEAM_ID});

    expect(await screen.findByText('31 Aug 2026 to 16 Nov 2026, Biomechanics, all templates')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${INSIGHTS}?Weeks=12&TeamId=${TEAM_ID}`);
    expect(screen.getByRole('link', {name: /^Overdue: 5/})).toHaveAttribute('href', `/pathways?overdue=true&mine=false&team=${TEAM_ID}`);

    await user.click(screen.getByRole('combobox', {name: /Template/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All templates', 'Knee review']);
  });

  it("chooses a team (archived ones too), keeping the template only if it's the team's", async () => {
    const {user, onSearchChange} = renderInsights({template: TEMPLATE_ID});

    await screen.findByText('31 Aug 2026 to 16 Nov 2026, Knee review');
    await user.click(screen.getByRole('combobox', {name: /Team/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All teams', 'Allergy (archived)', 'Biomechanics']);
    await user.click(screen.getByRole('option', {name: 'Biomechanics'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({template: TEMPLATE_ID, team: TEAM_ID});

    await user.click(screen.getByRole('combobox', {name: /Team/}));
    await user.click(screen.getByRole('option', {name: 'Allergy (archived)'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({template: undefined, team: OTHER_TEAM_ID});

    await user.click(screen.getByRole('combobox', {name: /Team/}));
    await user.click(screen.getByRole('option', {name: 'All teams'}));
    expect(onSearchChange).toHaveBeenLastCalledWith({template: TEMPLATE_ID, team: undefined});
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

    expect(await screen.findByRole('heading', {name: 'Pathways (no template)'})).toBeInTheDocument();
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
