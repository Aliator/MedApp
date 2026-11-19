import {screen} from '@testing-library/react';
import {json, mockApi, renderWithApp, userWithRoles} from '../../test/render';
import {REQUEST_ID, testRequest} from '../../test/hospitals';
import {expectPatientFirstInDetails} from '../../test/patientDetails';
import {HospitalRequestPage, NHS_NUMBERS_DIFFER} from './HospitalRequestPage';

const labUser = userWithRoles('test-lab', 'Lab');

function renderPage(answer: () => Response) {
  mockApi({[`GET /api/hospital-requests/${REQUEST_ID}`]: answer});
  return renderWithApp(<HospitalRequestPage id={REQUEST_ID} />, {user: labUser});
}

describe('HospitalRequestPage', () => {
  it('shows the patient, first in Details, over the request’s own numbers, what was asked, and the lab case', async () => {
    renderPage(() => json(testRequest()));

    expect(await screen.findByRole('heading', {name: 'Hospital Request MA26-000201', level: 1})).toBeInTheDocument();
    expectPatientFirstInDetails(/Ashdown, Wilfred/);
    expect(screen.getByText('From Riverside General, hospital number RG-204817')).toBeInTheDocument();
    expect(screen.getByText('Riverside General (ZZR01)')).toBeInTheDocument();
    expect(screen.getByText('Ms A. Example')).toBeInTheDocument();
    expect(screen.getByText('Yes, the request says so')).toBeInTheDocument();
    expect(screen.getByText('Skin biopsy (punch) (SNOMED CT 309066003)')).toBeInTheDocument();
    expect(screen.getByText('Skin histology')).toBeInTheDocument();
    expect(screen.getByText('From the hospital')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Open their record'})).toHaveAttribute('href', `/patients/${testRequest().patientId}`);
    expect(screen.queryByText(NHS_NUMBERS_DIFFER)).not.toBeInTheDocument();
  });

  it('says when the NHS number on the request isn’t the record’s', async () => {
    renderPage(() => json(testRequest({nhsNumber: '9991032746', nhsNumbersDiffer: true, reportNeededBy: null})));

    expect(await screen.findByText(NHS_NUMBERS_DIFFER)).toBeInTheDocument();
    expect(screen.getByText("No date given: the lab's limit applies")).toBeInTheDocument();
  });

  it('says when it isn’t there, or is another team’s', async () => {
    renderPage(() => json({title: 'Not found', status: 404}, 404));
    expect(await screen.findByText('Hospital request not found')).toBeInTheDocument();
  });
});
