import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {HalfViewContext} from './halfView';
import {TwoColumns} from './TwoColumns';

describe('TwoColumns', () => {
  it('shows both columns, in order', async () => {
    renderWithApp(<TwoColumns start={<p>Details</p>} end={<p>Stages</p>} />);

    const details = await screen.findByText('Details');
    const stages = screen.getByText('Stages');
    expect(details.compareDocumentPosition(stages) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('gives the columns equal shares of the width, whatever their content', async () => {
    renderWithApp(<TwoColumns start={<p>Short</p>} end={<p>A much longer column of content</p>} />);

    const columns = [(await screen.findByText('Short')).parentElement!, screen.getByText('A much longer column of content').parentElement!];
    for (const column of columns) expect(column.style.flexBasis).toMatch(/^0(px|%)?$/);
  });

  it('stacks them, each as tall as its content, in a narrow layout (a phone, a half view)', async () => {
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <TwoColumns start={<p>Details</p>} end={<p>Stages</p>} />
      </HalfViewContext.Provider>,
    );

    const details = await screen.findByText('Details');
    expect(details.parentElement).toBe(screen.getByText('Stages').parentElement);
    expect(details.parentElement).toHaveAttribute('data-direction', 'vertical');
  });
});
