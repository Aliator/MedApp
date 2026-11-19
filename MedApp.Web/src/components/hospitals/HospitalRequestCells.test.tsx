import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {LabRequestPriorityToken} from './HospitalRequestCells';

describe('LabRequestPriorityToken', () => {
  it('says the priority in words', async () => {
    renderWithApp(<LabRequestPriorityToken priority="Asap" />);
    expect(await screen.findByText('As soon as possible')).toBeInTheDocument();
  });

  it('shows an unknown priority as given', async () => {
    renderWithApp(<LabRequestPriorityToken priority="Later" />);
    expect(await screen.findByText('Later')).toBeInTheDocument();
  });
});
