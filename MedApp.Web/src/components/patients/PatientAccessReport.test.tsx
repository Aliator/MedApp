import {screen} from '@testing-library/react';
import {adminUser, calls, json, mockApi, pageOf, renderWithApp} from '../../test/render';
import {formatDateTime} from '../../utils/dates';
import {PatientAccessReport} from './PatientAccessReport';

const patientId = 'a1b2c3d4-0000-4000-8000-000000000003';
const SEARCH = '/api/activity/search';

const entries = [
  {id: 'e1', action: 'PatientViewed', username: 'k.ashford', atUtc: '2026-11-15T10:00:00Z', patientId, details: '{}'},
  {id: 'e2', action: 'PatientUpdated', username: '', atUtc: '2026-11-14T10:00:00Z', patientId, details: JSON.stringify({fields: ['email']})},
];

function renderReport(handler: () => Response) {
  const fetch = mockApi({[`GET ${SEARCH}`]: handler});
  return {fetch, ...renderWithApp(<PatientAccessReport patientId={patientId} />, {user: adminUser})};
}

describe('PatientAccessReport', () => {
  it('lists who accessed the record and when, newest first, with a link to all of it', async () => {
    const {fetch} = renderReport(() => json(pageOf(entries, 23)));

    expect(await screen.findByText('k.ashford')).toBeInTheDocument();
    expect(screen.getByText('Patient viewed')).toBeInTheDocument();
    expect(screen.getByText('Patient edited: Changed email')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.getByText(formatDateTime('2026-11-15T10:00:00Z'))).toBeInTheDocument();
    expect(screen.getByText('Showing the latest 2 of 23.')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'View All in the Activity Log'})).toHaveAttribute('href', `/admin/activity?patientId=${patientId}`);
    expect(calls(fetch)).toEqual([`GET ${SEARCH}?PageSize=10&PatientId=${patientId}&Page=1&Ascending=false`]);
  });

  it('says when nobody has accessed it', async () => {
    renderReport(() => json(pageOf([])));

    expect(await screen.findByText(/Nobody has accessed this record/)).toBeInTheDocument();
    expect(screen.queryByText(/Showing the latest/)).not.toBeInTheDocument();
  });

  it('says when it could not be loaded', async () => {
    renderReport(() => json({title: 'Server error'}, 500));

    expect(await screen.findByText('The access report could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
