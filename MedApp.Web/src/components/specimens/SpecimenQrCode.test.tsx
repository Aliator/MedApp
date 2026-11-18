import {render, screen} from '@testing-library/react';
import {SpecimenQrCode} from './SpecimenQrCode';

describe('SpecimenQrCode', () => {
  it("draws the server's path, black on white, named by the number it holds", () => {
    render(<SpecimenQrCode path="M4 4h1v1h-1z" size={29} number="MA26-000123" displaySize={120} />);
    const code = screen.getByRole('img', {name: 'QR code of specimen number MA26-000123'});
    expect(code).toHaveAttribute('viewBox', '0 0 29 29');
    expect(code).toHaveAttribute('width', '120');
    expect(code.querySelector('path')).toHaveAttribute('d', 'M4 4h1v1h-1z');
    expect(code.querySelector('path')).toHaveAttribute('fill', '#000000');
    expect(code.querySelector('rect')).toHaveAttribute('fill', '#ffffff');
  });

  it('draws nothing without a path', () => {
    const {container} = render(<SpecimenQrCode path="" size={0} number="MA26-000123" />);
    expect(container).toBeEmptyDOMElement();
  });
});
