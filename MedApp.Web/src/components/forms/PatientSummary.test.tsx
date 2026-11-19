import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {PatientSummary} from './PatientSummary';

const patient = {firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'};

describe('PatientSummary', () => {
  it('shows the name, a UK-formatted date of birth and the email', async () => {
    renderWithApp(<PatientSummary {...patient} />);

    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.getByText('1 December 1976')).toBeInTheDocument();
    expect(screen.getByText('freya.ahmed18@example.com')).toBeInTheDocument();
  });

  it('can leave out the date of birth and email, and add items', async () => {
    renderWithApp(
      <PatientSummary
        {...patient}
        showDateOfBirth={false}
        showEmail={false}
        additionalItems={<MetadataListItem label="Pathways">3</MetadataListItem>}
      />,
    );

    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.queryByText('1 December 1976')).not.toBeInTheDocument();
    expect(screen.queryByText('freya.ahmed18@example.com')).not.toBeInTheDocument();
    expect(screen.getByText('Pathways')).toBeInTheDocument();
  });

  it('shows the NHS number when given, grouped, or says it was not given', async () => {
    const {unmount} = renderWithApp(<PatientSummary {...patient} nhsNumber="9990000026" />);
    expect(await screen.findByText('999 000 0026')).toBeInTheDocument();
    unmount();

    renderWithApp(<PatientSummary {...patient} nhsNumber="" />);
    expect(await screen.findByText('Not given')).toBeInTheDocument();
  });

  it('leaves the NHS number out when not given at all', async () => {
    renderWithApp(<PatientSummary {...patient} />);

    expect(await screen.findByText('Freya Ahmed')).toBeInTheDocument();
    expect(screen.queryByText('NHS number')).not.toBeInTheDocument();
  });
});
