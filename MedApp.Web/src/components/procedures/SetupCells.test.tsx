import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {SetupNameCell, SetupStatusToken} from './SetupCells';

describe('SetupCells', () => {
  it('says whether something is active or archived', async () => {
    renderWithApp(
      <>
        <SetupStatusToken isArchived={false} />
        <SetupStatusToken isArchived />
      </>,
    );

    expect(await screen.findByText('Active')).toBeInTheDocument();
    expect(screen.getByText('Archived')).toBeInTheDocument();
  });

  it('shows a name with its second line, or alone', async () => {
    renderWithApp(
      <>
        <SetupNameCell name="Consent" secondary="Informed consent has been obtained." />
        <SetupNameCell name="Review" />
        <SetupNameCell name="Scan" token={<SetupStatusToken isArchived />} />
      </>,
    );

    expect(await screen.findByText('Consent')).toBeInTheDocument();
    expect(screen.getByText('Informed consent has been obtained.')).toBeInTheDocument();
    expect(screen.getByText('Review')).toBeInTheDocument();
    // A token beside the name.
    expect(screen.getByText('Scan').parentElement).toHaveTextContent('ScanArchived');
  });
});
