import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {PathwayPatient} from './PathwayPatient';

const patientId = '0000000c-0000-4000-8000-000000000001';
// A synthetic patient and test NHS number (the 999 range is never issued to real patients).
const patient = {firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', nhsNumber: '9990000018'};

describe('PathwayPatient', () => {
  it('shows the name (family name first, linking to the patient), date of birth and NHS number', async () => {
    renderWithApp(<PathwayPatient patientId={patientId} patient={patient} />);

    const link = await screen.findByRole('link', {name: /Ahmed, Freya/});
    expect(link).toHaveAttribute('href', `/patients/${patientId}`);
    expect(screen.getByText(/^1 December 1976/)).toBeInTheDocument();
    expect(screen.getByText('999 000 0018')).toBeInTheDocument();
    expect(screen.getByText('9 9 9, 0 0 0, 0 0 1 8')).toBeInTheDocument();
  });

  it('says when the date of birth or NHS number is missing', async () => {
    renderWithApp(<PathwayPatient patientId={patientId} patient={{firstName: 'Freya', lastName: 'Ahmed'}} />);

    expect(await screen.findByText('Not known')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
  });

  it("falls back to the pathway's name for the patient", async () => {
    renderWithApp(<PathwayPatient patientId={patientId} patient={undefined} patientName="Freya Ahmed" />);

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toBeInTheDocument();
  });
});
