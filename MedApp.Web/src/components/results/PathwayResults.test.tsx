import {screen} from '@testing-library/react';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {RESULT_ID, RESULT_PATHWAY_ID, testResultSummary} from '../../test/results';
import {PathwayResults} from './PathwayResults';

const pageOf = <T,>(items: T[]) => ({items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1});

describe('PathwayResults', () => {
  it("lists the pathway's results, each linking to its page, with its status and responsible clinician", async () => {
    const fetch = mockApi({
      'GET /api/results/search': () =>
        json(
          pageOf([
            testResultSummary(),
            testResultSummary({id: 'second', title: 'CT chest', specimenNumber: null, source: 'ExternalReport', status: 'SignedOff', needsClinician: false}),
            testResultSummary({id: 'third', title: 'Wound swab report', needsClinician: true, responsibleUsername: 'Deleted user'}),
          ]),
        ),
    });
    renderWithApp(<PathwayResults pathwayId={RESULT_PATHWAY_ID} />, {user: staffUser});

    expect(await screen.findByRole('link', {name: 'Skin biopsy (punch) report'})).toHaveAttribute('href', `/results/${RESULT_ID}`);
    expect(screen.getAllByText('Specimen MA26-000123 · reported 4 Nov 2026, 10:00').length).toBeGreaterThan(0);
    expect(screen.getAllByText('External report · reported 4 Nov 2026, 10:00').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Responsible clinician: Deleted user').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Signed off').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Needs a clinician').length).toBeGreaterThan(0);
    expect(calls(fetch)).toEqual([`GET /api/results/search?View=All&PathwayId=${RESULT_PATHWAY_ID}&PageSize=100`]);
  });

  it('says when there are none, or they could not be loaded', async () => {
    mockApi({'GET /api/results/search': () => json(pageOf([]))});
    renderWithApp(<PathwayResults pathwayId={RESULT_PATHWAY_ID} />, {user: staffUser});
    expect(await screen.findByText('No results yet.')).toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    mockApi({'GET /api/results/search': () => json({}, 500)});
    renderWithApp(<PathwayResults pathwayId={RESULT_PATHWAY_ID} />, {user: staffUser});
    expect(await screen.findByText('The results could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
