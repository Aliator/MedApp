import {Button} from '@astryxdesign/core/Button';
import {act, screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {HalfViewContext} from '../pages/halfView';
import {CollapseButton, PanelCard, PanelHeader} from './PanelCard';

describe('PanelCard', () => {
  it('shows a header bar, the body and footer actions', async () => {
    renderWithApp(
      <PanelCard title="Details" headerActions={<Button label="Collapse" />} footerActions={<Button label="View Patient" />}>
        <p>Body</p>
      </PanelCard>,
    );

    expect(await screen.findByRole('heading', {name: 'Details', level: 2})).toBeInTheDocument();
    expect(screen.getByText('Body')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Collapse'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'View Patient'})).toBeInTheDocument();
  });

  it('has no footer without footer actions', async () => {
    renderWithApp(
      <PanelCard title="Stages" isBodyPadded={false}>
        <p>Body</p>
      </PanelCard>,
    );

    expect(await screen.findByText('Body')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('makes a scrolling body a Tab stop named by the title, while it overflows, when asked', async () => {
    const layout = stubScrollLayout({content: 200, box: 400});
    renderWithApp(
      <PanelCard title="Created and completed" isBodyKeyboardScrollable>
        <p>Rows</p>
      </PanelCard>,
    );
    await screen.findByText('Rows');

    // It fits: no extra stop.
    expect(screen.queryByLabelText('Created and completed')).not.toBeInTheDocument();

    layout.setContent(900);
    const body = await screen.findByLabelText('Created and completed');
    expect(body).toHaveAttribute('tabindex', '0');
    expect(body).toContainElement(screen.getByText('Rows'));

    // It fits again (the table went back to a chart): the stop goes.
    layout.setContent(200);
    await waitFor(() => expect(screen.queryByLabelText('Created and completed')).not.toBeInTheDocument());
  });

  it('leaves a scrolling body out of the Tab order unless asked, as its own fields and links scroll it', async () => {
    stubScrollLayout({content: 900, box: 400});
    renderWithApp(
      <PanelCard title="Details">
        <p>Fields</p>
      </PanelCard>,
    );

    expect(await screen.findByText('Fields')).toBeInTheDocument();
    expect(screen.queryByLabelText('Details')).not.toBeInTheDocument();
    expect(document.querySelector('[tabindex="0"]')).toBeNull();
  });
});

/**
 * jsdom has no layout: every element's content is `content` px tall in a `box` px box, until the test ends, and
 * `setContent` changes the content's height and tells every ResizeObserver, as a browser would.
 */
function stubScrollLayout(size: {content: number; box: number}) {
  const callbacks: ResizeObserverCallback[] = [];
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(callback: ResizeObserverCallback) {
        callbacks.push(callback);
      }
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
  vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockImplementation(() => size.content);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(() => size.box);
  return {
    setContent: (content: number) =>
      act(() => {
        size.content = content;
        callbacks.forEach((callback) => callback([], {} as ResizeObserver));
      }),
  };
}

describe('PanelHeader and CollapseButton', () => {
  it('shows a section title with a button that folds it', async () => {
    const onToggle = vi.fn();
    const {user} = renderWithApp(
      <PanelHeader title="Stages" actions={<CollapseButton name="stages" isOpen={false} onToggle={onToggle} />} />,
    );

    expect(await screen.findByRole('heading', {name: 'Stages', level: 2})).toBeInTheDocument();
    const toggle = screen.getByRole('button', {name: 'Show stages'});
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);

    expect(onToggle).toHaveBeenCalled();
  });

  it('lets its actions wrap under the title in a narrow layout (a phone, a half view), and only there', async () => {
    const header = <PanelHeader title="Today, Tuesday 24 November" actions={<Button label="Open Calendar" />} />;
    const {unmount} = renderWithApp(header);
    const bar = async () => (await screen.findByRole('heading', {name: /^Today/})).parentElement!;
    expect(await bar()).not.toHaveAttribute('data-wrap', 'wrap');
    unmount();

    renderWithApp(<HalfViewContext.Provider value={{isHalf: true}}>{header}</HalfViewContext.Provider>);
    expect(await bar()).toHaveAttribute('data-wrap', 'wrap');
  });

  it('says Hide when the section is open', async () => {
    renderWithApp(<CollapseButton name="details" isOpen onToggle={() => {}} />);

    expect(await screen.findByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
  });

  it('shades a clickable header while the pointer is over it, and runs its click', async () => {
    const onHeaderClick = vi.fn();
    const {user} = renderWithApp(
      <PanelCard title="Light Mode" onHeaderClick={onHeaderClick}>
        Body
      </PanelCard>,
    );

    const header = (await screen.findByRole('heading', {name: 'Light Mode'})).closest('[style*="cursor"]') as HTMLElement;
    expect(header.style.backgroundColor).toBe('');
    await user.hover(header);
    expect(header.style.backgroundColor).toContain('color-mix');
    await user.unhover(header);
    expect(header.style.backgroundColor).toBe('');

    await user.click(header);
    expect(onHeaderClick).toHaveBeenCalledOnce();
  });
});
