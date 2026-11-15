import {act, screen} from '@testing-library/react';
import {useEffect, useState} from 'react';
import {renderWithApp} from '../test/render';
import {useAnnounce, usePageTitle} from './announcements';

const announcer = () => screen.getByTestId('announcer');

function Announces({messages}: {messages: string[]}) {
  const announce = useAnnounce();
  useEffect(() => messages.forEach(announce), [announce, messages]);
  return null;
}

function Titled() {
  const [title, setTitle] = useState('Patients');
  usePageTitle(title, true);
  return <button type="button" onClick={() => setTitle('Procedures')}>Next page</button>;
}

describe('announcements', () => {
  beforeEach(() => {
    vi.useFakeTimers({shouldAdvanceTime: true});
    localStorage.setItem('medapp.accessibility', JSON.stringify({announcements: true}));
  });

  it('says messages that come together as one', async () => {
    renderWithApp(<Announces messages={['Patients page', '12 results']} />);

    await act(() => vi.advanceTimersByTimeAsync(200));
    expect(announcer()).toHaveTextContent('Patients page. 12 results');
  });

  it('says nothing when announcements are off, as they start', async () => {
    localStorage.removeItem('medapp.accessibility');
    renderWithApp(<Announces messages={['12 results']} />);

    await act(() => vi.advanceTimersByTimeAsync(200));
    expect(announcer()).toHaveTextContent('');
  });

  it('titles the browser tab, and announces each page after the first', async () => {
    const {user} = renderWithApp(<Titled />);
    await screen.findByRole('button', {name: 'Next page'});
    await act(() => vi.advanceTimersByTimeAsync(200));
    expect(document.title).toBe('Patients - MedApp');
    // The first page is the one the browser loaded: it reads that itself.
    expect(announcer()).toHaveTextContent('');

    await user.click(screen.getByRole('button', {name: 'Next page'}));
    await act(() => vi.advanceTimersByTimeAsync(200));
    expect(document.title).toBe('Procedures - MedApp');
    expect(announcer()).toHaveTextContent('Procedures page');
  });
});
