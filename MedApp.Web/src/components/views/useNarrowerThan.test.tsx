import {render, screen} from '@testing-library/react';
import {givenWidth} from '../../test/layout';
import {useNarrowerThan} from './useNarrowerThan';

function Probe({rem}: {rem: number}) {
  const [ref, isNarrower] = useNarrowerThan(rem);
  return <output ref={ref}>{isNarrower ? 'narrower' : 'wide enough'}</output>;
}

describe('useNarrowerThan', () => {
  it('says whether the element is narrower than a width in rem (16px each by default)', () => {
    givenWidth(500);
    const {unmount} = render(<Probe rem={38} />);
    // 500px is under 38rem (608px).
    expect(screen.getByRole('status')).toHaveTextContent('narrower');
    unmount();

    render(<Probe rem={30} />);
    expect(screen.getByRole('status')).toHaveTextContent('wide enough');
  });

  it('counts rem at the text size chosen', () => {
    givenWidth(500);
    document.documentElement.style.fontSize = '12px';
    try {
      // 38rem at 12px is 456px.
      render(<Probe rem={38} />);
      expect(screen.getByRole('status')).toHaveTextContent('wide enough');
    } finally {
      document.documentElement.style.fontSize = '';
    }
  });

  it("is wide enough until it's measured, and while it has no width", () => {
    render(<Probe rem={38} />);
    // jsdom has no ResizeObserver: never measured.
    expect(screen.getByRole('status')).toHaveTextContent('wide enough');

    givenWidth(0);
    render(<Probe rem={38} />);
    expect(screen.getAllByRole('status')[1]).toHaveTextContent('wide enough');
  });
});
