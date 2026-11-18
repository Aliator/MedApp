import {screen, waitFor} from '@testing-library/react';
import {createRef} from 'react';
import {renderWithApp} from '../../test/render';
import {AnchoredMultiSelector, AnchoredSelector, scrollToSelectedOption} from './AnchoredSelector';

const ROW = 20;
const LIST_HEIGHT = 100;

/** Lays out options as rows of 20 px in a list 100 px high (jsdom lays nothing out), and records each scroll. */
function fakeListLayout() {
  const scrolls: {element: Element; top: number}[] = [];
  const isListbox = (e: Element) => e.getAttribute('role') === 'listbox';
  const rowIndex = (e: Element) => [...(e.parentElement?.children ?? [])].indexOf(e);
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return isListbox(this) ? LIST_HEIGHT : 0;
  });
  vi.spyOn(HTMLElement.prototype, 'scrollHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return isListbox(this) ? this.querySelectorAll('[role="option"]').length * ROW : 0;
  });
  vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockReturnValue(ROW);
  vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: Element) {
    // The list is unscrolled in jsdom, so an option's top is its row's.
    const top = this.getAttribute('role') === 'option' ? rowIndex(this) * ROW : 0;
    return {top, bottom: top + ROW, left: 0, right: 100, width: 100, height: ROW, x: 0, y: top, toJSON: () => ({})} as DOMRect;
  });
  vi.spyOn(Element.prototype, 'scrollTop', 'set').mockImplementation(function (this: Element, top: number) {
    scrolls.push({element: this, top});
  });
  return scrolls;
}

function listbox(options: {label: string; selected?: boolean}[]) {
  const list = document.createElement('div');
  list.setAttribute('role', 'listbox');
  for (const {label, selected} of options) {
    const option = document.createElement('div');
    option.setAttribute('role', 'option');
    option.setAttribute('aria-selected', String(selected === true));
    option.textContent = label;
    list.append(option);
  }
  document.body.append(list);
  return list;
}

const times = Array.from({length: 40}, (_, i) => `${String(8 + Math.floor(i / 4)).padStart(2, '0')}:${String((i % 4) * 15).padStart(2, '0')}`);

describe('scrollToSelectedOption', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    document.body.innerHTML = '';
  });

  it('puts the selected option in the middle of the list', () => {
    const scrolls = fakeListLayout();
    const list = listbox(times.map((label, i) => ({label, selected: i === 20})));

    scrollToSelectedOption(list);

    // Row 20's top is 400; centring a 20 px row in 100 px leaves 40 px above it.
    expect(scrolls).toEqual([{element: list, top: 360}]);
  });

  it('goes no higher than the top for an option near it', () => {
    const scrolls = fakeListLayout();
    const list = listbox(times.map((label, i) => ({label, selected: i === 1})));

    scrollToSelectedOption(list);

    expect(scrolls).toEqual([{element: list, top: 0}]);
  });

  it('shows the first option when nothing is selected, and the top of an empty list', () => {
    const scrolls = fakeListLayout();
    const list = listbox(times.map((label) => ({label})));
    const empty = listbox([]);

    scrollToSelectedOption(list);
    scrollToSelectedOption(empty);

    expect(scrolls).toEqual([
      {element: list, top: 0},
      {element: empty, top: 0},
    ]);
  });

  it('scrolls the list, not the page, when the list itself has nothing to scroll yet', () => {
    const scrolls = fakeListLayout();
    const popover = document.createElement('div');
    popover.setAttribute('popover', '');
    const list = listbox([{label: '09:00', selected: true}]);
    popover.append(list);
    document.body.append(popover);

    scrollToSelectedOption(list);

    expect(scrolls.map((s) => s.element)).toEqual([list]);
  });
});

describe('AnchoredSelector', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('is a selector: shows its value, lists its options and reports a choice', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<AnchoredSelector label="Start" value="10:00" onChange={onChange} options={times} />);

    const picker = await screen.findByRole('combobox', {name: 'Start'});
    expect(picker).toHaveTextContent('10:00');
    await user.click(picker);
    await user.click(screen.getByRole('option', {name: '10:15'}));
    expect(onChange).toHaveBeenCalledWith('10:15');
  });

  it('opens its list at the selected option every time, wherever it was left', async () => {
    const scrolls = fakeListLayout();
    const {user} = renderWithApp(<AnchoredSelector label="Start" value="13:00" onChange={() => {}} options={times} />);
    const picker = await screen.findByRole('combobox', {name: 'Start'});

    for (let open = 1; open <= 2; open++) {
      scrolls.length = 0;
      await user.click(picker);
      const list = screen.getByRole('listbox');
      // 13:00 is row 20, so the list scrolls to 360 to centre it.
      await waitFor(() => expect(scrolls).toContainEqual({element: list, top: 360}));
      expect(scrolls.at(-1)).toEqual({element: list, top: 360});
      await user.keyboard('{Escape}');
      await waitFor(() => expect(picker).toHaveAttribute('aria-expanded', 'false'));
    }
  });

  it('passes its trigger to the caller’s ref, and lets it go when unmounted', async () => {
    const ref = createRef<HTMLButtonElement>();
    const {unmount} = renderWithApp(<AnchoredSelector ref={ref} label="Start" value="10:00" onChange={() => {}} options={times} />);

    const trigger = await screen.findByRole('combobox', {name: 'Start'});
    expect(ref.current).toBe(trigger);
    unmount();
    expect(ref.current).toBeNull();
  });

  it('opens a multi-select at the top every time, where it lists what is chosen, wherever it was left', async () => {
    const scrolls = fakeListLayout();
    const onChange = vi.fn();
    const {user} = renderWithApp(<AnchoredMultiSelector label="Who" value={['13:00', '14:00']} onChange={onChange} options={times} />);
    const picker = await screen.findByRole('combobox', {name: 'Who'});

    for (let open = 1; open <= 2; open++) {
      scrolls.length = 0;
      await user.click(picker);
      const list = screen.getByRole('listbox');
      // Astryx lists the chosen options first, so the first chosen option is at the top.
      expect(screen.getAllByRole('option')[0]).toHaveTextContent('13:00');
      await waitFor(() => expect(scrolls).toContainEqual({element: list, top: 0}));
      await user.keyboard('{Escape}');
      await waitFor(() => expect(picker).toHaveAttribute('aria-expanded', 'false'));
    }
    await user.click(picker);
    await user.click(screen.getByRole('option', {name: '08:00'}));
    expect(onChange).toHaveBeenCalled();
  });

  it('passes its trigger to a callback ref too', async () => {
    const ref = vi.fn();
    renderWithApp(<AnchoredSelector ref={ref} label="Start" value="10:00" onChange={() => {}} options={times} />);

    expect(ref).toHaveBeenCalledWith(await screen.findByRole('combobox', {name: 'Start'}));
  });
});
