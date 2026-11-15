import {act, render, renderHook, screen} from '@testing-library/react';
import {useEffect} from 'react';
import {TextSizeProvider, useTextSize} from './textSize';

let api: ReturnType<typeof useTextSize>;
function Probe() {
  const value = useTextSize();
  // Handed to the test after rendering, as effects run.
  useEffect(() => {
    api = value;
  });
  return <output>{value.textSize}</output>;
}

const renderSize = () =>
  render(
    <TextSizeProvider>
      <Probe />
    </TextSizeProvider>,
  );

describe('textSize', () => {
  it("leaves the browser's own size alone at Standard, as it starts", () => {
    renderSize();

    expect(screen.getByRole('status')).toHaveTextContent('standard');
    expect(document.documentElement.style.fontSize).toBe('');
  });

  it('scales the page for another size, remembers it, and puts the page back when it goes', () => {
    const {unmount} = renderSize();

    act(() => api.setTextSize('large'));

    expect(document.documentElement.style.fontSize).toBe('112.5%');
    expect(localStorage.getItem('medapp.textSize')).toBe('large');
    unmount();
    expect(document.documentElement.style.fontSize).toBe('');
  });

  it('takes a stored size it does not know as Standard', () => {
    localStorage.setItem('medapp.textSize', 'huge');
    renderSize();

    expect(screen.getByRole('status')).toHaveTextContent('standard');
  });

  it('must be used inside its provider', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});

    expect(() => renderHook(() => useTextSize())).toThrow(/inside TextSizeProvider/);
  });
});
