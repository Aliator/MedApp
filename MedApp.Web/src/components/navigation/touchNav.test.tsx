import {fireEvent, renderHook, screen, waitFor} from '@testing-library/react';
import {json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {AppSideNav} from './AppSideNav';
import {TOUCH_NAV_QUERY, useIsTouchNav} from './touchNav';
import {appSettings} from '../../app/settingsStore';

function givenTouchScreen(isTouch: boolean) {
  vi.stubGlobal('matchMedia', (media: string) => ({
    matches: isTouch && media === TOUCH_NAV_QUERY,
    media,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }));
}

const patientId = '00000000-0000-4000-8000-00000000a001';

function renderNav(path: string) {
  mockApi({'GET /api/patients/labels': () => json({items: [{id: patientId, label: 'Freya Ahmed'}]})});
  return renderWithApp(<AppSideNav />, {user: staffUser, path});
}

describe('the side nav on a touch screen', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('is a touch screen when the window is phone sized or can’t hover', () => {
    givenTouchScreen(true);
    expect(renderHook(() => useIsTouchNav()).result.current).toBe(true);
    givenTouchScreen(false);
    expect(renderHook(() => useIsTouchNav()).result.current).toBe(false);
  });

  it('has no row menus, so a tap only opens the page', async () => {
    givenTouchScreen(true);
    const {router} = renderNav(`/patients/${patientId}/edit`);

    const patients = await screen.findByRole('link', {name: /^patients$/i});
    expect(document.querySelector('button[aria-label$=" options"]')).toBeNull();

    fireEvent.click(patients);
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('keeps the row menus with a mouse', async () => {
    givenTouchScreen(false);
    renderNav(`/patients/${patientId}/edit`);

    await screen.findByRole('link', {name: /^patients$/i});
    expect(document.querySelector('button[aria-label="Patients options"]')).not.toBeNull();
  });

  it('brings the row menus back, always shown, when Always show the side nav\'s menu buttons is on', async () => {
    givenTouchScreen(true);
    appSettings.set('medapp.navAlwaysShowRowMenus', 'true');
    renderNav(`/patients/${patientId}/edit`);

    await screen.findByRole('link', {name: /^patients$/i});
    const button = document.querySelector<HTMLElement>('button[aria-label="Patients options"]');
    expect(button).not.toBeNull();
    expect(button!.closest('[style*="visibility: hidden"]')).toBeNull();
  });

  it('shows a mouse user the row menus without hovering when the setting is on', async () => {
    givenTouchScreen(false);
    appSettings.set('medapp.navAlwaysShowRowMenus', 'true');
    renderNav(`/patients/${patientId}/edit`);

    await screen.findByRole('link', {name: /^patients$/i});
    const button = document.querySelector<HTMLElement>('button[aria-label="Patients options"]');
    expect(button!.closest('[style*="visibility: hidden"]')).toBeNull();
  });
});
