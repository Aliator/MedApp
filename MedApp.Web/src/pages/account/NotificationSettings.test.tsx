import {screen, waitFor} from '@testing-library/react';
import {expectAnnounced, findAlert, json, mockApi, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {NotificationSettings} from './NotificationSettings';

const PREFERENCES = '/api/notifications/preferences';

function renderSettings(handlers: Parameters<typeof mockApi>[0]) {
  const fetch = mockApi(handlers);
  return {fetch, ...renderWithApp(<NotificationSettings />, {user: staffUser})};
}

const assignedSwitch = () => screen.getByRole('switch', {name: /assigned to a pathway/});

describe('NotificationSettings', () => {
  it("shows the user's choice, and saves a change on the server", async () => {
    withAnnouncements();
    const {user, fetch} = renderSettings({
      [`GET ${PREFERENCES}`]: () => json({pathwayAssigned: true}),
      [`PUT ${PREFERENCES}`]: () => json({pathwayAssigned: false}),
    });

    await waitFor(() => expect(assignedSwitch()).toBeChecked());
    await user.click(assignedSwitch());

    await waitFor(() => expect(assignedSwitch()).not.toBeChecked());
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await put.json()).toEqual({pathwayAssigned: false});
    await expectAnnounced('Assignment notifications off');
  });

  it('points to My Worklist with its filter on all open work', async () => {
    renderSettings({[`GET ${PREFERENCES}`]: () => json({pathwayAssigned: true})});

    expect(await screen.findByText(/work from My Worklist, with its filter on all open work/)).toBeInTheDocument();
  });

  it('shows a choice made before as off', async () => {
    renderSettings({[`GET ${PREFERENCES}`]: () => json({pathwayAssigned: false})});

    await waitFor(() => expect(assignedSwitch()).not.toBeChecked());
    expect(assignedSwitch()).toBeEnabled();
  });

  it('says when the choice could not be saved, and keeps what was there', async () => {
    const {user} = renderSettings({
      [`GET ${PREFERENCES}`]: () => json({pathwayAssigned: true}),
      [`PUT ${PREFERENCES}`]: () => json({title: 'Server error'}, 500),
    });

    await waitFor(() => expect(assignedSwitch()).toBeChecked());
    await user.click(assignedSwitch());

    await findAlert('Your choice could not be saved');
    expect(assignedSwitch()).toBeChecked();
  });

  it('says when the choices could not be loaded', async () => {
    renderSettings({[`GET ${PREFERENCES}`]: () => json({title: 'Server error'}, 500)});

    await findAlert('Your notification choices could not be loaded');
  });
});
