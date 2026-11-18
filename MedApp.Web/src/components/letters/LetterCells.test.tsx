import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {DispatchStatusToken, LetterStatusToken} from './LetterCells';

describe('LetterCells', () => {
  afterEach(() => localStorage.clear());

  it('words where a letter is, with an icon when icons on statuses are on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <LetterStatusToken letter={{status: 'Draft'}} />
        <LetterStatusToken letter={{status: 'Draft', isReturned: true}} />
        <LetterStatusToken letter={{status: 'AwaitingApproval'}} />
        <LetterStatusToken letter={{status: 'Approved', unsentCount: 2}} />
        <LetterStatusToken letter={{status: 'Approved', unsentCount: 0}} />
        <LetterStatusToken letter={{status: 'Withdrawn'}} />
        <LetterStatusToken letter={{status: 'Lost'}} />
      </>,
      {user: staffUser},
    );
    for (const words of ['Draft', 'Returned', 'Awaiting approval', 'To send', 'Approved', 'Withdrawn', 'Lost'])
      expect(await screen.findByText(words)).toBeInTheDocument();
    expect(document.querySelectorAll('svg').length).toBeGreaterThanOrEqual(6);
  });

  it('words dispatch, without icons unless they are on', async () => {
    renderWithApp(
      <>
        <DispatchStatusToken status="NotSent" />
        <DispatchStatusToken status="Sent" />
        <DispatchStatusToken status="Failed" />
        <DispatchStatusToken status="Queued" />
      </>,
      {user: staffUser},
    );
    for (const words of ['Not sent', 'Sent', 'Failed', 'Queued']) expect(await screen.findByText(words)).toBeInTheDocument();
    expect(document.querySelectorAll('svg')).toHaveLength(0);
  });
});
