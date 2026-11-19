import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ClampedRichText} from './ClampedRichText';

/** Makes every element measure as this tall in full and this tall as shown, and reports each size as it's watched. */
function measuring(scroll: number, client: number) {
  vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockReturnValue(scroll);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockReturnValue(client);
  vi.stubGlobal(
    'ResizeObserver',
    class {
      constructor(private readonly report: ResizeObserverCallback) {}
      observe() {
        this.report([], this as unknown as ResizeObserver);
      }
      unobserve() {}
      disconnect() {}
    },
  );
}

describe('ClampedRichText', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('cuts a long note to three lines, with Show More to read it all and Show Less to fold it again', async () => {
    measuring(200, 60);
    const {user} = renderWithApp(<ClampedRichText>{'A long synthetic note. '.repeat(40)}</ClampedRichText>);

    const more = await screen.findByRole('button', {name: 'Show More'});
    expect(more).toHaveAttribute('aria-expanded', 'false');
    // The whole text is in the page, folded from view only.
    expect(screen.getByText(/A long synthetic note/)).toBeInTheDocument();
    await user.click(more);

    const less = screen.getByRole('button', {name: 'Show Less'});
    expect(less).toHaveAttribute('aria-expanded', 'true');
    await user.click(less);
    expect(screen.getByRole('button', {name: 'Show More'})).toBeInTheDocument();
  });

  it('shows a note that fits in three lines as it is, with no button', async () => {
    measuring(40, 40);
    renderWithApp(<ClampedRichText>A short note.</ClampedRichText>);

    expect(await screen.findByText('A short note.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Show More'})).not.toBeInTheDocument();
  });
});
