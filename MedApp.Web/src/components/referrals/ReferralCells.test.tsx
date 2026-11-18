import {screen} from '@testing-library/react';
import {renderWithApp, staffUser} from '../../test/render';
import {daysAgoWords, LetterCell, ReceivedCell, ReferralActions, ReferralPatientCell, ReferralStatusToken} from './ReferralCells';

describe('ReferralCells', () => {
  it('words statuses, with an icon when icons on statuses are on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <ReferralStatusToken status="AwaitingTriage" />
        <ReferralStatusToken status="InformationRequested" />
        <ReferralStatusToken status="RedirectedElsewhere" />
        <ReferralStatusToken status="EnteredInError" />
        <ReferralStatusToken status="SomethingNew" />
        <ReferralStatusToken status={null} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('Awaiting triage')).toBeInTheDocument();
    expect(screen.getByText('Information requested')).toBeInTheDocument();
    expect(screen.getByText('Redirected elsewhere')).toBeInTheDocument();
    expect(screen.getByText('Entered in error')).toBeInTheDocument();
    expect(screen.getByText('Something new')).toBeInTheDocument();
    expect(document.querySelectorAll('svg').length).toBeGreaterThanOrEqual(3);
    localStorage.clear();
  });

  it('says how long ago, or how long it waited once closed', async () => {
    expect(daysAgoWords(0)).toBe('Today');
    expect(daysAgoWords(1)).toBe('1 day ago');
    expect(daysAgoWords(12)).toBe('12 days ago');
    renderWithApp(
      <>
        <ReceivedCell receivedOn="2026-11-02" daysSinceReceived={12} isOpen />
        <ReceivedCell receivedOn="2026-11-03" daysSinceReceived={1} isOpen={false} />
        <ReceivedCell receivedOn="2026-11-04" daysSinceReceived={5} isOpen={false} />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByText('2 Nov 2026')).toBeInTheDocument();
    expect(screen.getByText('12 days ago')).toBeInTheDocument();
    expect(screen.getByText('Waited 1 day')).toBeInTheDocument();
    expect(screen.getByText('Waited 5 days')).toBeInTheDocument();
  });

  it('links the patient to the referral, with their NHS number, and offers both pages', async () => {
    renderWithApp(
      <>
        <ReferralPatientCell id="r1" patientName="Freya Ahmed" nhsNumber="9990000018" />
        <ReferralPatientCell id="r2" patientName="Sam Test" nhsNumber={null} />
        <LetterCell hasLetter />
        <LetterCell hasLetter={false} />
        <ReferralActions id="r1" patientId="p1" />
      </>,
      {user: staffUser},
    );
    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('href', '/referrals/r1');
    expect(screen.getByText('NHS No. 999 000 0018')).toBeInTheDocument();
    expect(screen.getByText('No NHS number')).toBeInTheDocument();
    expect(screen.getByText('Yes')).toBeInTheDocument();
    expect(screen.getByText('No')).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'View referral'})).toHaveAttribute('href', '/referrals/r1');
    expect(screen.getByRole('link', {name: 'View patient'})).toHaveAttribute('href', '/patients/p1');
  });
});
