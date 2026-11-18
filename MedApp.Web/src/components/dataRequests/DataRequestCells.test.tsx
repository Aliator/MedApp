import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {DataRequestStatusToken, DoneCell, DueCell} from './DataRequestCells';

describe('DataRequestCells', () => {
  it("shows a request's status in words", async () => {
    renderWithApp(<DataRequestStatusToken status="Refused" />, {user: staffUser});
    expect(await screen.findByText('Refused')).toBeInTheDocument();
  });

  it('shows the due date, flagged in words once overdue', async () => {
    const {unmount} = renderWithApp(<DueCell dueOn="2026-12-02" isOverdue />, {user: staffUser});
    expect(await screen.findByText('2 Dec 2026')).toBeInTheDocument();
    expect(screen.getByText('Overdue')).toBeInTheDocument();
    unmount();

    renderWithApp(<DueCell dueOn="2026-12-02" />, {user: staffUser});
    expect(await screen.findByText('2 Dec 2026')).toBeInTheDocument();
    expect(screen.queryByText('Overdue')).not.toBeInTheDocument();
  });

  it('says whether a step is done', async () => {
    const {unmount} = renderWithApp(<DoneCell isDone />, {user: staffUser});
    expect(await screen.findByText('Yes')).toBeInTheDocument();
    unmount();

    renderWithApp(<DoneCell isDone={false} />, {user: staffUser});
    expect(await screen.findByText('Not yet')).toBeInTheDocument();
  });
});
