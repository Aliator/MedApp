import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ActivityToken, activityColour, AppointmentStatusToken, VisitRequirementToken} from './CalendarTokens';

const tokenColour = (text: string) => screen.getByText(text).closest('.astryx-token')?.getAttribute('data-color');

describe('CalendarTokens', () => {
  it('shows each appointment status in words, in its colour', async () => {
    renderWithApp(
      <>
        <AppointmentStatusToken status="Requested" />
        <AppointmentStatusToken status="Booked" />
        <AppointmentStatusToken status="Attended" />
        <AppointmentStatusToken status="DidNotAttend" />
        <AppointmentStatusToken status="Cancelled" />
        <AppointmentStatusToken status="Mystery" />
      </>,
    );

    expect(await screen.findByText('Requires appointment')).toBeInTheDocument();
    expect(tokenColour('Requires appointment')).toBe('yellow');
    expect(tokenColour('Booked')).toBe('blue');
    expect(tokenColour('Attended')).toBe('green');
    expect(tokenColour('Did not attend')).toBe('red');
    expect(tokenColour('Cancelled')).toBe('gray');
    expect(tokenColour('Mystery')).toBe('default');
    expect(document.querySelector('.astryx-token svg')).toBeNull();
  });

  it('adds icons when "Icons on statuses" is on', async () => {
    localStorage.setItem('medapp.accessibility', JSON.stringify({statusIcons: true}));
    renderWithApp(
      <>
        <AppointmentStatusToken status="Booked" />
        <AppointmentStatusToken status="Mystery" />
        <ActivityToken name="Lunch" colour="green" />
        <VisitRequirementToken requirement="Required" />
        <VisitRequirementToken requirement="Suggested" />
      </>,
    );

    await screen.findByText('Booked');
    expect(document.querySelectorAll('.astryx-token svg')).toHaveLength(4);
  });

  it('shows an activity in its colour, and gray for a colour outside the palette', async () => {
    renderWithApp(
      <>
        <ActivityToken name="Lunch" colour="green" />
        <ActivityToken name="Odd" colour="#123456" />
      </>,
    );

    expect(await screen.findByText('Lunch')).toBeInTheDocument();
    expect(tokenColour('Lunch')).toBe('green');
    expect(tokenColour('Odd')).toBe('gray');
    expect(activityColour(null)).toBe('gray');
  });

  it('says whether a stage involves a visit, and nothing when it does not', async () => {
    const {container} = renderWithApp(
      <>
        <VisitRequirementToken requirement="Suggested" />
        <VisitRequirementToken requirement="Required" />
        <VisitRequirementToken requirement="None" />
        <VisitRequirementToken requirement={null} />
      </>,
    );

    expect(await screen.findByText('Visit suggested')).toBeInTheDocument();
    expect(screen.getByText('Visit required')).toBeInTheDocument();
    expect(container.querySelectorAll('.astryx-token')).toHaveLength(2);
  });
});
