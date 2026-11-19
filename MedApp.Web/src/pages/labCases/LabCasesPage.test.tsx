import {screen} from '@testing-library/react';
import {json, mockApi, renderWithApp, userWithRoles} from '../../test/render';
import {CASE_ID, testCaseSummary} from '../../test/labCases';
import {LabCasesPage} from './LabCasesPage';
import {labCaseViewOf, parseLabCasesSearch} from './labCasesSearch';

const labUser = userWithRoles('test-lab', 'Lab');

describe('LabCasesPage', () => {
  it('lists the tab’s cases, each opening its page, with where it came from and what it needs next', async () => {
    const fetch = mockApi({
      'POST /api/lab-cases/search': () => json({items: [testCaseSummary()], page: 1, pageSize: 10, totalCount: 1, totalPages: 1}),
    });
    renderWithApp(<LabCasesPage search={{view: 'ToReport'}} onSearchChange={vi.fn()} />, {user: labUser});

    expect(await screen.findByRole('link', {name: 'MA26-000300'})).toHaveAttribute('href', `/lab/cases/${CASE_ID}`);
    expect(screen.getByText('To report: 1 case')).toBeInTheDocument();
    expect(screen.getByText('Dermatology')).toBeInTheDocument();
    expect(screen.getByText('Needs more work?')).toBeInTheDocument();
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toMatchObject({view: 'ToReport', page: 1});
  });

  it('reads its address, dropping what isn’t valid', () => {
    expect(parseLabCasesSearch({view: 'Reported', source: 'Hospital', page: '2', sort: 'number', asc: 'false'})).toEqual({
      view: 'Reported',
      source: 'Hospital',
      page: 2,
      sort: 'number',
      asc: false,
    });
    expect(parseLabCasesSearch({view: 'Nope', source: 'Elsewhere', page: '1', sort: 'x'})).toEqual({});
    expect(labCaseViewOf({})).toBe('Open');
  });
});
