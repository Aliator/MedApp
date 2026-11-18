import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {RouterLink} from './RouterLink';

const links = (onClick?: () => void) => (
  <>
    <RouterLink href="/patients" onClick={onClick}>
      Patients
    </RouterLink>
    <RouterLink href="https://example.org/help">Help</RouterLink>
    <RouterLink href="/pathways" target="_blank">
      Pathways in a new tab
    </RouterLink>
    <RouterLink>No address</RouterLink>
  </>
);

describe('RouterLink', () => {
  it('goes to an app page on the client, calling its own click handler first', async () => {
    const onClick = vi.fn();
    const {user, router} = renderWithApp(links(onClick));

    await user.click(await screen.findByRole('link', {name: 'Patients'}));

    expect(onClick).toHaveBeenCalled();
    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('leaves modified clicks (a new tab or window) to the browser', async () => {
    const {user, router} = renderWithApp(links());
    const patients = await screen.findByRole('link', {name: 'Patients'});

    for (const key of ['Control', 'Meta', 'Shift', 'Alt']) {
      await user.keyboard(`{${key}>}`);
      await user.click(patients);
      await user.keyboard(`{/${key}}`);
    }
    await user.pointer({keys: '[MouseMiddle]', target: patients});

    expect(router.state.location.pathname).toBe('/');
  });

  it('leaves other sites, links that open elsewhere, and links with no address to the browser', async () => {
    const {user, router} = renderWithApp(links());
    // jsdom doesn't follow links, so a link left to the browser goes nowhere here.
    await user.click(await screen.findByRole('link', {name: 'Help'}));
    await user.click(screen.getByRole('link', {name: 'Pathways in a new tab'}));
    await user.click(screen.getByText('No address'));

    expect(router.state.location.pathname).toBe('/');
  });

  it('leaves a click its own handler has dealt with', async () => {
    const {user, router} = renderWithApp(
      <RouterLink href="/patients" onClick={(event) => event.preventDefault()}>
        Patients
      </RouterLink>,
    );

    await user.click(await screen.findByRole('link', {name: 'Patients'}));

    expect(router.state.location.pathname).toBe('/');
  });
});
