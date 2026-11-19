import {Button} from '@astryxdesign/core/Button';
import {act, screen, waitFor, within} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {DialogFrame} from '../modals/DialogFrame';
import {Page, useIsNarrow, useIsNarrowWindow} from './Page';
import {AlertMessage} from '../shared/AlertMessage';
import {HalfViewContext} from './halfView';
import {PageHeader} from './PageHeader';

describe('PageHeader', () => {
  it('shows the title as the page heading, with the subtitle and actions', async () => {
    renderWithApp(<PageHeader title="Patients" subtitle="60 patients registered" actions={<Button label="Search" />} />);

    expect(await screen.findByRole('heading', {level: 1, name: 'Patients'})).toBeInTheDocument();
    expect(screen.getByText('60 patients registered')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Search'})).toBeInTheDocument();
  });

  it('shows a page message in the header, between the title and the actions', async () => {
    renderWithApp(
      <PageHeader
        title="Patients"
        subtitle="Everyone registered with the centre"
        message={<AlertMessage message="The patients could not be loaded." />}
        actions={<Button label="Search" />}
      />,
    );

    const heading = await screen.findByRole('heading', {level: 1, name: 'Patients'});
    const alert = screen.getByRole('alert');
    // In the title's own line, not beside the subtitle under it (the owner, 19 November 2026).
    expect(heading.parentElement).toContainElement(alert);
    expect(heading.parentElement).not.toContainElement(screen.getByText('Everyone registered with the centre'));
    expect(alert).toHaveTextContent('The patients could not be loaded.');
    expect(heading.compareDocumentPosition(alert) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(alert.compareDocumentPosition(screen.getByRole('button', {name: 'Search'})) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    // It starts from no width and takes what's left (with a minimum), so a long message wraps its own words beside the
    // title rather than taking a line of its own and pushing the actions under it.
    const space = alert.closest<HTMLElement>('[style*="flex-basis"]')!;
    expect(space.style.flexBasis).toBe('0px');
    expect(space.style.minInlineSize).toBe('12rem');
  });

  it('ends with a button that closes its half, in a half view', async () => {
    const onClose = vi.fn();
    const {user} = renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true, onClose}}>
        <PageHeader title="Patients" actions={<Button label="Search" />} />
        <PageHeader title="Started" level={2} />
      </HalfViewContext.Provider>,
    );

    await user.click(await screen.findByRole('button', {name: 'Close half view'}));
    expect(onClose).toHaveBeenCalledOnce();
    // Only the page's own header has it, not a section's.
    expect(screen.getAllByRole('button', {name: 'Close half view'})).toHaveLength(1);
    // Outside the part that wraps, so it stays top right when the actions go under the title.
    const close = screen.getByRole('button', {name: 'Close half view'});
    const heading = screen.getByRole('heading', {level: 1, name: 'Patients'});
    let row = screen.getByRole('button', {name: 'Search'}).parentElement!;
    while (!row.contains(heading)) row = row.parentElement!;
    expect(row.contains(close)).toBe(false);
  });

  it('has no back button unless asked for one', async () => {
    renderWithApp(<PageHeader title="Patients" />);

    await screen.findByRole('heading', {name: 'Patients'});
    expect(screen.queryByRole('link', {name: 'Back'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Back'})).not.toBeInTheDocument();
  });

  it('navigates back to backHref', async () => {
    const {user, router} = renderWithApp(<PageHeader title="Add patient" backHref="/patients" />, {path: '/patients/add'});

    await user.click(await screen.findByRole('link', {name: 'Back'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('calls onBack', async () => {
    const onBack = vi.fn();
    const {user} = renderWithApp(<PageHeader title="Edit patient" onBack={onBack} backLabel="Back to patient" />);

    await user.click(await screen.findByRole('button', {name: 'Back'}));

    expect(onBack).toHaveBeenCalledOnce();
  });
});

describe('Page', () => {
  it('shows the header and content', async () => {
    renderWithApp(
      <Page title="Patients">
        <p>Table goes here</p>
      </Page>,
    );

    expect(await screen.findByRole('heading', {name: 'Patients'})).toBeInTheDocument();
    expect(screen.getByText('Table goes here')).toBeInTheDocument();
  });

  it('shows a spinner with the loading message instead of the content while loading', async () => {
    renderWithApp(
      <Page title="Patients" isLoading loadingMessage="Loading patients...">
        <p>Table goes here</p>
      </Page>,
    );

    expect(await screen.findByText('Loading patients...')).toBeInTheDocument();
    expect(screen.queryByText('Table goes here')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Patients'})).toBeInTheDocument();
  });

  it('shows its banner between the header and the content', async () => {
    renderWithApp(
      <Page title="View Patient" banner={<p>Who it is about</p>} fill>
        <p>Details go here</p>
      </Page>,
    );

    const heading = await screen.findByRole('heading', {name: 'View Patient'});
    const banner = screen.getByText('Who it is about');
    expect(heading.compareDocumentPosition(banner) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(banner.compareDocumentPosition(screen.getByText('Details go here')) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('keeps a long page’s header at the top while the page scrolls under it, with a line once it’s stuck', async () => {
    let report: IntersectionObserverCallback = () => {};
    vi.stubGlobal(
      'IntersectionObserver',
      class {
        constructor(callback: IntersectionObserverCallback) {
          report = callback;
        }
        observe() {}
        disconnect() {}
      },
    );
    renderWithApp(
      <Page title="Account Settings" actions={<Button label="Save" />}>
        <p>A long page</p>
      </Page>,
    );

    const header = (await screen.findByRole('heading', {name: 'Account Settings'})).closest<HTMLElement>('[data-page-header]')!;
    expect(header.style.position).toBe('sticky');
    expect(within(header).getByRole('button', {name: 'Save'})).toBeInTheDocument();
    expect(header.style.boxShadow).toBe('');
    // Scrolled: a pixel of it out of view.
    act(() => report([{intersectionRatio: 0.99} as IntersectionObserverEntry], {} as IntersectionObserver));
    expect(header.style.boxShadow).toContain('var(--color-border)');
    vi.unstubAllGlobals();
  });

  it('leaves a fill page’s header as it is: its cards scroll, not the page', async () => {
    renderWithApp(
      <Page title="Patients" fill>
        <p>Table goes here</p>
      </Page>,
    );

    expect((await screen.findByRole('heading', {name: 'Patients'})).closest('[data-page-header]')).toBeNull();
  });

  it('renders its dialogs even while loading', async () => {
    renderWithApp(<Page title="Patients" isLoading dialogs={<p>A dialog</p>} />);

    expect(await screen.findByText('A dialog')).toBeInTheDocument();
  });
});

describe('useIsNarrow', () => {
  function Shows() {
    return <output>{useIsNarrow() ? 'narrow' : 'wide'}</output>;
  }

  it('is narrow in a half view, whatever the window', async () => {
    renderWithApp(
      <>
        <Shows />
        <HalfViewContext.Provider value={{isHalf: true}}>
          <Shows />
        </HalfViewContext.Provider>
      </>,
    );

    expect(await screen.findByText('wide')).toBeInTheDocument();
    expect(screen.getByText('narrow')).toBeInTheDocument();
  });

  it("goes by the window alone in a dialog, which sits over the whole window, even one opened from a half view", async () => {
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <Shows />
        <DialogFrame isOpen title="Synthetic dialog" onClose={() => {}}>
          <Shows />
        </DialogFrame>
      </HalfViewContext.Provider>,
    );

    const dialog = await screen.findByRole('dialog', {name: 'Synthetic dialog'});
    expect(within(dialog).getByText('wide')).toBeInTheDocument();
    // The page behind it is still narrow, in its half.
    expect(screen.getAllByText('narrow')).toHaveLength(1);
  });
});

describe('useIsNarrowWindow', () => {
  it('ignores a half view, for a dialog choosing its own size', async () => {
    function Shows() {
      return <output>{useIsNarrowWindow() ? 'narrow' : 'wide'}</output>;
    }
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <Shows />
      </HalfViewContext.Provider>,
    );

    expect(await screen.findByText('wide')).toBeInTheDocument();
  });
});
