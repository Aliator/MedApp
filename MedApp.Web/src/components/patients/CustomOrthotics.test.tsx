import {screen} from '@testing-library/react';
import {OrthoticPage} from '../../pages/patients/OrthoticPage';
import {renderWithApp} from '../../test/render';
import {CustomOrthotics} from './CustomOrthotics';

describe('CustomOrthotics', () => {
  it('shows nothing outside the light demo, where no orthotics are kept yet', async () => {
    renderWithApp(<CustomOrthotics patientId="p1" />);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(screen.queryByText('Custom Orthotics')).not.toBeInTheDocument();
  });

  it('says the details page is being built', async () => {
    renderWithApp(<OrthoticPage patientId="p1" />);
    expect(await screen.findByText('This page is currently being built.')).toBeInTheDocument();
  });
});
