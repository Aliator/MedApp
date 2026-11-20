import {SideNav} from '@astryxdesign/core/SideNav';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import userEvent from '@testing-library/user-event';
import {coachPosition, resetCoachForTests} from './coach/coachState';
import {guidedTourNavEntry, readMeNavEntry} from './readMeNav';

describe('readMeNavEntry', () => {
  it('is a side nav item that opens the Read Me, marked when it is the current page', async () => {
    const entry = readMeNavEntry({isSelected: true, isHalfView: false, halfViewRef: () => {}, menu: null});
    renderWithApp(<SideNav>{entry.row}</SideNav>, {path: '/read-me'});

    expect(entry).toMatchObject({key: 'read-me', label: 'Read Me', href: '/read-me', isSelected: true});
    expect(await screen.findByRole('link', {name: 'Read Me'})).toHaveAttribute('href', '/read-me');
  });
});

describe('guidedTourNavEntry', () => {
  beforeEach(() => resetCoachForTests());

  it('is a side nav item that starts the guided tour from the beginning', async () => {
    const user = userEvent.setup();
    const entry = guidedTourNavEntry();
    renderWithApp(<SideNav>{entry.row}</SideNav>);
    expect(entry).toMatchObject({key: 'guided-tour', label: 'Guided Tour', isSelected: false});
    await user.click(await screen.findByRole('button', {name: 'Guided Tour'}));
    expect(coachPosition()).toEqual({step: 0, mark: 0});
  });
});
