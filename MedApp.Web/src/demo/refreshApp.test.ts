import {refreshApp} from './refreshApp';

describe('refreshApp', () => {
  it("clears the light demo's storage, keeps the full demo's, and reloads", async () => {
    localStorage.setItem('medapp.colorMode', 'dark');
    localStorage.setItem('medappLightDemo.coach', '{}');
    localStorage.setItem('medappFullDemo.coach', '{}');
    sessionStorage.setItem('anything', '1');
    const reload = vi.fn();
    vi.stubGlobal('location', {...window.location, href: 'http://localhost/MedApp/login', reload});
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response('')));
    vi.stubGlobal('indexedDB', {
      deleteDatabase: () => {
        const request: {onsuccess?: () => void} = {};
        setTimeout(() => request.onsuccess?.(), 0);
        return request;
      },
    });

    await refreshApp();

    expect(localStorage.getItem('medapp.colorMode')).toBeNull();
    expect(localStorage.getItem('medappLightDemo.coach')).toBeNull();
    expect(localStorage.getItem('medappFullDemo.coach')).toBe('{}');
    expect(sessionStorage.length).toBe(0);
    expect(reload).toHaveBeenCalled();
    vi.unstubAllGlobals();
    localStorage.clear();
  });
});
