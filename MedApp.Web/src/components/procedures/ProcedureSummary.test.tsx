import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ProcedureSummary} from './ProcedureSummary';

describe('ProcedureSummary', () => {
  it('lists what it is given, with the due date written out', async () => {
    renderWithApp(<ProcedureSummary title="Knee review" patientName="Test Patient" dueDate="2026-12-01" priority="High" />);

    expect(await screen.findByText('Knee review')).toBeInTheDocument();
    expect(screen.getByText('Test Patient')).toBeInTheDocument();
    expect(screen.getByText('High')).toBeInTheDocument();
    expect(screen.getByText(/1 December 2026/)).toBeInTheDocument();
    expect(screen.queryByText('Template')).not.toBeInTheDocument();
  });
});
