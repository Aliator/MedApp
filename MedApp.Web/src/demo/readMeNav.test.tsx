import {SideNav} from '@astryxdesign/core/SideNav';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../test/render';
import {readMeNavEntry} from './readMeNav';

describe('readMeNavEntry', () => {
  it('is a side nav item that opens the Read Me, marked when it is the current page', async () => {
    const entry = readMeNavEntry({isSelected: true, isHalfView: false, halfViewRef: () => {}, menu: null});
    renderWithApp(<SideNav>{entry.row}</SideNav>, {path: '/read-me'});

    expect(entry).toMatchObject({key: 'read-me', label: 'Read Me', href: '/read-me', isSelected: true});
    expect(await screen.findByRole('link', {name: 'Read Me'})).toHaveAttribute('href', '/read-me');
  });
});
