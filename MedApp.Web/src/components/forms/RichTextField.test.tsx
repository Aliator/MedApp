import {act, screen, waitFor} from '@testing-library/react';
import {createRef} from 'react';
import {renderWithApp} from '../../test/render';
import {richText, selectRichText, setRichText, typeRichText} from '../../test/richText';
import {RichTextField, WRAPPING_TOOLBAR_CSS, type RichTextFieldHandle} from './RichTextField';

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

  it("wraps its toolbar's buttons onto another row where it's narrow, rather than hiding them", async () => {
    renderWithApp(<RichTextField label="Notes" value="" onChange={vi.fn()} />);

    const toolbar = await screen.findByRole('toolbar');
    expect(toolbar.closest('.medapp-rich-text-toolbar')).not.toBeNull();
    expect(document.querySelector('style[data-medapp-rich-text]')?.textContent).toBe(WRAPPING_TOOLBAR_CSS);
    expect(WRAPPING_TOOLBAR_CSS).toContain('flex-wrap: wrap');
  });

  describe('its selection', () => {
    async function renderField(value: string) {
      const field = createRef<RichTextFieldHandle>();
      const onChange = vi.fn();
      renderWithApp(<RichTextField ref={field} label="Notes" value={value} onChange={onChange} />);
      const box = await screen.findByRole('textbox', {name: 'Notes'});
      return {field, box, onChange};
    }

    it('is nothing while nothing is selected', async () => {
      const {field} = await renderField('Synthetic text.');

      expect(field.current!.selection()).toBeNull();
    });

    it('keeps its formatting, across bold and lines', async () => {
      const {field, box} = await renderField('Seen **today** by the team.\n\nBloods sent.');

      await selectRichText(box, 'today by the team.Bloods');

      expect(field.current!.selection()!.markdown).toBe('**today** by the team.\n\nBloods');
    });

    it('keeps whole list items as a list', async () => {
      const {field, box} = await renderField('- Bloods taken\n- Obs stable');

      await selectRichText(box, 'Bloods takenObs stable');

      expect(field.current!.selection()!.markdown).toBe('- Bloods taken\n- Obs stable');
    });

    it('takes out exactly the copy selected, not the first copy of the same words', async () => {
      const {field, box, onChange} = await renderField('Bed 3: obs stable\n\nBed 4: obs stable');

      await selectRichText(box, 'obs stable', 1);
      let after: string | null = null;
      act(() => {
        after = field.current!.selection()!.remove();
      });

      expect(after).toMatch(/^Bed 3: obs stable\n\nBed 4:\s*$/);
      expect(onChange).toHaveBeenLastCalledWith(after);
    });

    it('takes nothing out once the box has changed since it was selected', async () => {
      const {field, box} = await renderField('Synthetic: call back.');
      await selectRichText(box, 'call back');
      const selected = field.current!.selection()!;

      await typeRichText(box, ' Done.');

      expect(selected.remove()).toBeNull();
      expect(richText(box)).toBe('Synthetic: call back. Done.');
    });
  });
});
