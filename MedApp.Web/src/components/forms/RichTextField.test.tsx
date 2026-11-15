import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {richText, setRichText} from '../../test/richText';
import {RichTextField} from './RichTextField';

describe('RichTextField', () => {
  it('opens on its text, formatted, with a formatting toolbar', async () => {
    renderWithApp(<RichTextField label="Notes" value={'Seen **today**.\n\n- Bloods taken'} onChange={vi.fn()} />);

    const box = await screen.findByRole('textbox', {name: 'Notes'});
    expect(richText(box)).toContain('Seen today.');
    expect(box.querySelector('strong, b, [class*="bold"]')).not.toBeNull();
    expect(box.querySelector('li')).toHaveTextContent('Bloods taken');
    expect(screen.getByRole('button', {name: /bold/i})).toBeInTheDocument();
  });

  it('reports each change as Markdown', async () => {
    const onChange = vi.fn();
    renderWithApp(<RichTextField label="Notes" value="" onChange={onChange} />);

    await setRichText(await screen.findByRole('textbox', {name: 'Notes'}), 'Plan **agreed**.');

    await waitFor(() => expect(onChange).toHaveBeenLastCalledWith('Plan **agreed**.'));
  });

  it('opens plain text written before as it was', async () => {
    const onChange = vi.fn();
    renderWithApp(<RichTextField label="Notes" value="Prep done." onChange={onChange} />);

    expect(richText(await screen.findByRole('textbox', {name: 'Notes'}))).toBe('Prep done.');
    // Nothing changed, so nothing to report as changed.
    for (const [markdown] of onChange.mock.calls) expect(markdown).toBe('Prep done.');
  });
});
