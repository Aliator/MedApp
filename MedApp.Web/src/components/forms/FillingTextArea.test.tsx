import {render, screen} from '@testing-library/react';
import {afterEach, vi} from 'vitest';
import {FillingTextArea} from './FillingTextArea';

const LINE_HEIGHT = 20;
// The content around the box: label, padding and the buttons under it.
const OTHER_CONTENT = 60;

/**
 * jsdom lays nothing out, so give it a scrolling area of a set height whose content is the text area's rows plus
 * a fixed amount, measured the way the component measures them.
 */
function stubLayout(areaHeight: number) {
  const getComputedStyle = window.getComputedStyle.bind(window);
  vi.spyOn(window, 'getComputedStyle').mockImplementation((element) => {
    const style = getComputedStyle(element);
    if (element instanceof HTMLTextAreaElement) Object.defineProperty(style, 'lineHeight', {value: `${LINE_HEIGHT}px`});
    return style;
  });
  vi.spyOn(HTMLElement.prototype, 'clientHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return this.dataset.testid === 'area' ? areaHeight : 0;
  });
  vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(function (this: HTMLElement) {
    const textArea = this.querySelector('textarea');
    return this.dataset.testid === 'content' && textArea ? OTHER_CONTENT + textArea.rows * LINE_HEIGHT : 0;
  });
}

function renderInArea() {
  return render(
    <div data-testid="area" style={{overflowY: 'auto'}}>
      <div data-testid="content">
        <FillingTextArea label="Notes" value="" onChange={() => {}} />
      </div>
    </div>,
  );
}

afterEach(() => vi.restoreAllMocks());

describe('FillingTextArea', () => {
  it('starts at two rows when nothing is laid out', () => {
    renderInArea();

    expect(screen.getByRole('textbox', {name: 'Notes'})).toHaveAttribute('rows', '2');
  });

  it('grows to fill the free space of the area around it, without overflowing it', () => {
    stubLayout(300);
    renderInArea();

    // 300 high, less 60 of other content, leaves 240: twelve 20px rows.
    expect(screen.getByRole('textbox', {name: 'Notes'})).toHaveAttribute('rows', '12');
  });

  it('keeps at least two rows when there is no room, leaving the area to scroll', () => {
    stubLayout(50);
    renderInArea();

    expect(screen.getByRole('textbox', {name: 'Notes'})).toHaveAttribute('rows', '2');
  });
});
