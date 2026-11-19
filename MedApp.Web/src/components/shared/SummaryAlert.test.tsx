import {act, fireEvent, screen, waitFor, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {SummaryAlert} from './SummaryAlert';

// The hover card itself, counting what the label's click asks of it: jsdom can't show whether a real browser would
// reopen a card that a tap closed, so the tests check that nothing asks it to.
const showRequests = vi.hoisted(() => ({count: 0}));
vi.mock('@astryxdesign/core/HoverCard', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@astryxdesign/core/HoverCard')>();
  return {
    ...actual,
    useHoverCard: (...args: Parameters<typeof actual.useHoverCard>) => {
      const card = actual.useHoverCard(...args);
      return {
        ...card,
        show: () => {
          showRequests.count++;
          card.show();
        },
      };
    },
  };
});

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

  it('shows only the short label as text, with the status icon as a button that says there is more, closed', async () => {
    await renderAlert();

    // The label is plain text; the button is the icon at its end, with no text of its own.
    expect(screen.getByText('Synthetic state')).not.toContainElement(trigger());
    expect(screen.getByText('Synthetic state').closest('button')).toBeNull();
    expect(trigger()).toHaveTextContent(/^$/);
    expect(trigger()).toHaveAttribute('aria-haspopup', 'dialog');
    expect(trigger()).toHaveAttribute('aria-expanded', 'false');
    expect(overlay()).not.toBeInTheDocument();
  });

  it('gives screen readers the whole message: in the alert, and as the button’s description', async () => {
    await renderAlert();

    // The alert is the message alone, once: not the label, and never the overlay.
    expect(screen.getByRole('alert').textContent).toBe(`${MESSAGE} Reason: Synthetic check Since yesterday.`);
    expect(trigger()).toHaveAccessibleDescription(`${MESSAGE} Reason: Synthetic check Since yesterday.`);
  });

  it('keeps the open overlay out of the alert, so opening it never announces the message again', async () => {
    const {user} = await renderAlert();

    await user.click(trigger());
    const card = await expectOpen();
    const alert = screen.getByRole('alert');
    expect(alert).not.toContainElement(card);
    expect(alert).not.toContainElement(trigger());
    expect(alert.textContent?.split(MESSAGE)).toHaveLength(2);
  });

  it('shows a summary after the label, on screen and in the button’s name', async () => {
    const {user} = renderWithApp(<SummaryAlert label="Synthetic state" summary="key fact" message={MESSAGE} />);

    const button = await screen.findByRole('button', {name: 'Synthetic state: key fact, show details'});
    expect(screen.getByText('Synthetic state: key fact')).toBeInTheDocument();
    await user.click(button);
    expect(await screen.findByRole('dialog', {name: 'Synthetic state: details'})).toBeInTheDocument();
  });

  it('ends an info banner with the "i" alone, with no icon before the label', async () => {
    const {container} = renderWithApp(<SummaryAlert status="info" label="Synthetic state" message={MESSAGE} />);
    const button = await screen.findByRole('button', {name: 'Synthetic state, show details'});

    // The only icon in the banner is the button's (Astryx draws its icons as svg).
    expect(container.querySelectorAll('svg')).toHaveLength(button.querySelectorAll('svg').length);
    expect(button.querySelectorAll('svg')).toHaveLength(1);
  });

  it('keeps a warning’s own icon before the label, so its status shows by more than colour', async () => {
    const {container} = renderWithApp(<SummaryAlert label="Synthetic state" message={MESSAGE} />);
    const button = await screen.findByRole('button', {name: 'Synthetic state, show details'});

    expect(button.querySelectorAll('svg')).toHaveLength(1);
    expect(container.querySelectorAll('svg').length).toBeGreaterThan(1);
  });

  it('paints the overlay on the popover colour, as menus and popovers are', async () => {
    const {user} = await renderAlert();

    await user.click(trigger());
    const card = await expectOpen();
    expect(card.closest('[popover]')?.getAttribute('style') ?? card.getAttribute('style')).toContain('var(--color-background-popover)');
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

  it('shows the whole message on a tap, and hides it on a second', async () => {
    const {user} = await renderAlert();

    await user.pointer({keys: '[TouchA]', target: trigger()});
    await expectOpen();
    await user.pointer({keys: '[TouchA]', target: trigger()});
    await waitFor(() => expect(overlay()).not.toBeInTheDocument());
  });

  it('toggles on taps whose click reports a mouse, as iOS Safari’s do', async () => {
    await renderAlert();
    // iOS Safari's tap: the press says "touch", the click that follows says "mouse".
    const tap = () => {
      fireEvent.pointerDown(trigger(), {pointerType: 'touch', isPrimary: true});
      fireEvent.pointerUp(trigger(), {pointerType: 'touch', isPrimary: true});
      fireEvent.click(trigger(), {pointerType: 'mouse'});
    };

    act(tap);
    await expectOpen();
    showRequests.count = 0;
    act(tap);
    await waitFor(() => expect(overlay()).not.toBeInTheDocument());
    // The click that ended the closing tap didn't ask to open it again.
    expect(showRequests.count).toBe(0);
  });

  it('opens on a click whatever its pointer, but never asks again while it is open', async () => {
    const {user} = await renderAlert();

    await user.hover(trigger());
    await expectOpen();
    showRequests.count = 0;
    await user.click(trigger());
    expect(showRequests.count).toBe(0);
    expect(overlay()).toBeInTheDocument();
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
