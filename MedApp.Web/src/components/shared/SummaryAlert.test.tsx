import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {SummaryAlert} from './SummaryAlert';

const MESSAGE = 'Synthetic standing warning: nothing here can be changed until an admin says so.';
const DETAILS = ['Reason: Synthetic check', 'Since yesterday.'];

async function renderAlert(status?: 'warning' | 'info', strict = false) {
  const rendered = renderWithApp(<SummaryAlert label="Synthetic state" message={MESSAGE} details={DETAILS} status={status} />, {strict});
  await screen.findByRole('button', {name: 'Synthetic state, show details'});
  return rendered;
}

const trigger = () => screen.getByRole('button', {name: 'Synthetic state, show details'});
const overlay = () => screen.queryByRole('dialog', {name: 'Synthetic state: details'});

async function expectOpen() {
  const card = await screen.findByRole('dialog', {name: 'Synthetic state: details'});
  expect(within(card).getByText(MESSAGE)).toBeInTheDocument();
  expect(within(card).getByText('Reason: Synthetic check')).toBeInTheDocument();
  expect(within(card).getByText('Since yesterday.')).toBeInTheDocument();
  return card;
}

describe('SummaryAlert', () => {
  beforeEach(() => {
    // jsdom has no Popover API, so nothing matches `:popover-open`, and Astryx's layers take Escape only while
    // they're shown: a layer in the document counts as shown, as it is in a browser.
    const matches = Element.prototype.matches;
    vi.spyOn(Element.prototype, 'matches').mockImplementation(function (this: Element, selector: string) {
      return selector === ':popover-open' ? this.isConnected && this.hasAttribute('popover') : matches.call(this, selector);
    });
  });

  it('shows only the short label, as a button that says there is more, with the whole message closed', async () => {
    await renderAlert();

    expect(trigger()).toHaveTextContent('Synthetic state');
    expect(trigger()).toHaveAttribute('aria-haspopup', 'dialog');
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
    expect(overlay()).not.toBeInTheDocument();
  });

  it('gives screen readers the whole message: in the alert, and as the button’s description', async () => {
    await renderAlert();

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(MESSAGE);
    expect(alert).toHaveTextContent('Reason: Synthetic check Since yesterday.');
    expect(trigger()).toHaveAccessibleDescription(`${MESSAGE} Reason: Synthetic check Since yesterday.`);
  });

  it('is not an alert when it only informs', async () => {
    await renderAlert('info');

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(trigger()).toHaveAccessibleDescription(new RegExp(MESSAGE));
  });

  it('shows the whole message on hover, and keeps it while the pointer moves onto it', async () => {
    const {user} = await renderAlert();

    await user.hover(trigger());
    const card = await expectOpen();
    expect(trigger()).toHaveAttribute('aria-expanded', 'true');

    await user.hover(within(card).getByText(MESSAGE));
    await act(() => new Promise((resolve) => setTimeout(resolve, 400)));
    expect(overlay()).toBeInTheDocument();
  });

  it('shows the whole message when the label takes the keyboard focus, and hides it on Escape', async () => {
    const {user} = await renderAlert();

    await user.tab();
    expect(trigger()).toHaveFocus();
    await expectOpen();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(overlay()).not.toBeInTheDocument());
    expect(trigger()).toHaveFocus();

    // Enter opens it again, as focus and hover haven't changed.
    await user.keyboard('{Enter}');
    await expectOpen();
  });

  it('shows the whole message on a click', async () => {
    const {user} = await renderAlert();

    await user.click(trigger());
    await expectOpen();
  });

  it('shows the whole message on a tap', async () => {
    const {user} = await renderAlert();

    await user.pointer({keys: '[TouchA]', target: trigger()});
    await expectOpen();
  });

  it('closes when the focus leaves', async () => {
    await renderAlert();

    fireEvent.focusIn(trigger());
    await expectOpen();
    fireEvent.focusOut(trigger(), {relatedTarget: document.body});
    await waitFor(() => expect(overlay()).not.toBeInTheDocument());
  });

  it('works under StrictMode', async () => {
    const {user} = await renderAlert('warning', true);

    await user.click(trigger());
    await expectOpen();
    await user.keyboard('{Escape}');
    await waitFor(() => expect(overlay()).not.toBeInTheDocument());
  });
});
