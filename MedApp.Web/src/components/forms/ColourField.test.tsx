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

  it('shows a warning under the colour when it has one, detached, so it never covers the colour', async () => {
    renderWithApp(<ColourField label="Cards" value="#25252a" onChange={() => {}} warning="Hard to read." />);

    const warning = await screen.findByText('Hard to read.');
    // The attached style pulls itself up to join a text box, over the colour and its value.
    expect(warning.closest('[data-variant]')).toHaveAttribute('data-variant', 'detached');
  });

  it('tells someone on the colour what it is for, and its warning', async () => {
    renderWithApp(<ColourField label="Cards" description="The card colour." value="#25252a" onChange={() => {}} warning="Hard to read." />);

    const input = await screen.findByLabelText(/Cards/);
    expect(input).toHaveAccessibleDescription('The card colour. Hard to read.');
  });

  it('opens the picker from a click anywhere on the row, once', async () => {
    const {user} = renderWithApp(
      <ColourField label="Cards" description="The card colour." value="#25252a" onChange={() => {}} warning="Hard to read." />,
    );
    const input = await screen.findByLabelText(/Cards/);
    const clicks = vi.fn();
    input.addEventListener('click', clicks);
    const row = input.closest<HTMLElement>('[style*="border-radius"]')!;

    // The description (which Astryx hands on to the colour itself), the hex value, the warning and the padding.
    await user.click(screen.getByText('The card colour.'));
    await user.click(screen.getByText('#25252a'));
    // The warning on screen, not its copy in the screen reader announcement.
    await user.click(screen.getAllByText('Hard to read.').find((el) => !el.closest('[data-astryx-live-region]'))!);
    await user.click(row);
    expect(clicks).toHaveBeenCalledTimes(4);

    // The colour itself, and its label, open it as they always did: once each, not twice.
    await user.click(input);
    await user.click(screen.getByText('Cards'));
    expect(clicks).toHaveBeenCalledTimes(6);
  });

  it('lets the hex value be selected to copy it, without opening the picker', async () => {
    const {user} = renderWithApp(<ColourField label="Cards" value="#25252a" onChange={() => {}} />);
    const input = await screen.findByLabelText(/Cards/);
    const clicks = vi.fn();
    input.addEventListener('click', clicks);
    const hex = screen.getByText('#25252a');

    // A drag across the value selects it, and ends with a click on the row.
    await user.pointer([
      {keys: '[MouseLeft>]', target: hex, offset: 0},
      {target: hex, offset: 7},
      {keys: '[/MouseLeft]', target: hex},
    ]);
    expect(window.getSelection()?.toString()).toBe('#25252a');
    expect(clicks).not.toHaveBeenCalled();
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
