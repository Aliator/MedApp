import {SideNav} from '@astryxdesign/core/SideNav';
import {screen} from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {renderWithApp} from '../test/render';
import {coachPosition, resetCoachForTests} from './coach/coachState';
import {walkthroughNavEntry} from './walkthroughNav';

describe('walkthroughNavEntry', () => {
  beforeEach(() => resetCoachForTests());

  it('is a side nav item that starts the walkthrough from the beginning', async () => {
    const user = userEvent.setup();
    const entry = walkthroughNavEntry();
    renderWithApp(<SideNav>{entry.row}</SideNav>);
    expect(entry).toMatchObject({key: 'walkthrough', label: 'Walkthrough', isSelected: false});
    await user.click(await screen.findByRole('button', {name: 'Walkthrough'}));
    expect(coachPosition()).toEqual({step: 0, mark: 0});
  });
});
