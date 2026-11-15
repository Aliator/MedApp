import {fireEvent, screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ColourField, ColourSwatches} from './ColourField';

describe('ColourField', () => {
  it('is labelled, shows the hex value, and reports each change', async () => {
    const onChange = vi.fn();
    renderWithApp(<ColourField label="Cards" description="The card colour." value="#25252A" onChange={onChange} />);

    const input = await screen.findByLabelText(/Cards/);
    expect(input).toHaveValue('#25252a');
    expect(screen.getByText('#25252a')).toBeInTheDocument();

    fireEvent.input(input, {target: {value: '#2a3550'}});
    expect(onChange).toHaveBeenCalledWith('#2a3550');
  });

  it('shades under the pointer, as list rows do', async () => {
    const {user} = renderWithApp(<ColourField label="Cards" value="#25252a" onChange={() => {}} />);
    const field = (await screen.findByLabelText(/Cards/)).closest<HTMLElement>('[style*="border-radius"]')!;
    const shade = () => field.style.backgroundColor;

    expect(shade()).toBe('');
    await user.hover(field);
    expect(shade()).toContain('color-mix');
    await user.unhover(field);
    expect(shade()).toBe('');
  });
});

describe('ColourSwatches', () => {
  it('shows one square per colour, hidden from screen readers', async () => {
    const {container} = renderWithApp(<ColourSwatches colours={['#000000', '#ffffff', '#ff0000']} />);

    await waitFor(() => expect(container.querySelectorAll('[aria-hidden] span')).toHaveLength(3));
  });
});
