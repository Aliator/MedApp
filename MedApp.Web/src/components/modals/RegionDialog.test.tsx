import {Center} from '@astryxdesign/core/Center';
import {StackItem} from '@astryxdesign/core/Stack';
import {screen, waitFor, within} from '@testing-library/react';
import {useState, type ReactNode} from 'react';
import {openDialog, renderWithApp} from '../../test/render';
import {useIsNarrow} from '../pages/Page';
import {DialogRegionContext, HalfViewContext} from '../pages/halfView';
import {DialogFrame} from './DialogFrame';
import {isDialogOpen} from './openDialogs';

/** Says which layout what's in the dialog takes, as a page's parts ask. */
function LayoutProbe() {
  return <p>{useIsNarrow() ? 'Narrow layout' : 'Wide layout'}</p>;
}

/** A button that opens a dialog, which can hold another. */
function Opener({label, title, children}: {label: string; title: string; children?: ReactNode}) {
  const [isOpen, setIsOpen] = useState(false);
  return (
    <>
      <button onClick={() => setIsOpen(true)}>{label}</button>
      <DialogFrame isOpen={isOpen} title={title} onClose={() => setIsOpen(false)}>
        <LayoutProbe />
        {children}
      </DialogFrame>
    </>
  );
}

/**
 * Two halves as `AppLayout` lays them out: the page in the left one (`isBeside`: something beside it), text to copy in
 * the right one, and the host dialogs over a region are rendered in.
 */
function Halves({isBeside = true, children}: {isBeside?: boolean; children: ReactNode}) {
  const [host, setHost] = useState<HTMLElement | null>(null);
  const [left, setLeft] = useState<HTMLElement | null>(null);
  return (
    <>
      <StackItem ref={setLeft} aria-label="Left half">
        <HalfViewContext.Provider value={{isHalf: isBeside, side: 'left'}}>
          <DialogRegionContext.Provider value={{element: left, host, isBeside}}>{children}</DialogRegionContext.Provider>
        </HalfViewContext.Provider>
      </StackItem>
      <StackItem aria-label="Right half">
        <p>Text to copy</p>
        <button>Beside</button>
      </StackItem>
      <Center ref={setHost} data-dialog-host="">
        {null}
      </Center>
    </>
  );
}

const leftHalf = () => screen.getByLabelText('Left half');
const rightHalf = () => screen.getByLabelText('Right half');

describe('DialogFrame over its own region', () => {
  it('opens over its half only: the page under it is inert, the other half is not', async () => {
    const {user} = renderWithApp(
      <Halves>
        <Opener label="Finish" title="Finish Stage?" />
      </Halves>,
    );

    await user.click(await screen.findByRole('button', {name: 'Finish'}));

    const dialog = await screen.findByRole('dialog', {name: 'Finish Stage?'});
    expect(dialog.closest('[data-dialog-host]')).not.toBeNull();
    expect(openDialog()).toBeNull();
    expect(isDialogOpen()).toBe(true);
    expect(leftHalf()).toHaveAttribute('inert');
    expect(rightHalf()).not.toHaveAttribute('inert');
    // Laid out by its half, as the page under it is.
    expect(within(dialog).getByText('Narrow layout')).toBeInTheDocument();
    // The focus starts in it, at its title.
    expect(dialog).toContainElement(document.activeElement as HTMLElement);
  });

  it('closes with Escape, and gives the focus back to what opened it', async () => {
    const {user} = renderWithApp(
      <Halves>
        <Opener label="Finish" title="Finish Stage?" />
      </Halves>,
    );
    await user.click(await screen.findByRole('button', {name: 'Finish'}));
    await screen.findByRole('dialog', {name: 'Finish Stage?'});

    await user.keyboard('{Escape}');

    await waitFor(() => expect(isDialogOpen()).toBe(false));
    expect(leftHalf()).not.toHaveAttribute('inert');
    expect(screen.getByRole('button', {name: 'Finish'})).toHaveFocus();
  });

  it('stays open on an Escape pressed while working in the other half, so nothing typed in it is lost', async () => {
    const {user} = renderWithApp(
      <Halves>
        <Opener label="Finish" title="Finish Stage?" />
      </Halves>,
    );
    await user.click(await screen.findByRole('button', {name: 'Finish'}));
    const dialog = await screen.findByRole('dialog', {name: 'Finish Stage?'});

    await user.click(within(rightHalf()).getByRole('button', {name: 'Beside'}));
    await user.keyboard('{Escape}');
    expect(isDialogOpen()).toBe(true);

    // Back in the dialog, Escape closes it.
    await user.click(within(dialog).getByText('Narrow layout'));
    await user.keyboard('{Escape}');
    await waitFor(() => expect(isDialogOpen()).toBe(false));
  });

  it('stacks a dialog opened from another within the same half, above it', async () => {
    const {user} = renderWithApp(
      <Halves>
        <Opener label="Outer" title="Outer dialog">
          <Opener label="Inner" title="Inner dialog" />
        </Opener>
      </Halves>,
    );
    await user.click(await screen.findByRole('button', {name: 'Outer'}));
    const outer = await screen.findByRole('dialog', {name: 'Outer dialog'});
    await user.click(within(outer).getByRole('button', {name: 'Inner'}));
    const inner = await screen.findByRole('dialog', {name: 'Inner dialog'});

    const overlays = [...document.querySelectorAll<HTMLElement>('[data-open-dialog]')];
    expect(overlays).toHaveLength(2);
    const zOf = (dialog: HTMLElement) => Number(dialog.closest<HTMLElement>('[data-region-dialog]')!.style.zIndex);
    expect(zOf(inner)).toBeGreaterThan(zOf(outer));

    // The half stays inert until the last of them closes.
    await user.keyboard('{Escape}');
    await waitFor(() => expect(document.querySelectorAll('[data-open-dialog]')).toHaveLength(1));
    expect(leftHalf()).toHaveAttribute('inert');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(isDialogOpen()).toBe(false));
    expect(leftHalf()).not.toHaveAttribute('inert');
  });

  it('sits over the whole window, as before, from a page with nothing beside it', async () => {
    const {user} = renderWithApp(
      <Halves isBeside={false}>
        <Opener label="Finish" title="Finish Stage?" />
      </Halves>,
    );

    await user.click(await screen.findByRole('button', {name: 'Finish'}));

    await waitFor(() => expect(openDialog()).not.toBeNull());
    expect(document.querySelector('[data-region-dialog]')).toBeNull();
    expect(within(openDialog()!).getByText('Wide layout')).toBeInTheDocument();
    expect(leftHalf()).not.toHaveAttribute('inert');
  });
});
