import {screen} from '@testing-library/react';
import {BLANK_DESCRIPTION} from '../../components/forms/fieldSpace';
import {descriptionOf, renderWithApp} from '../../test/render';
import {EMPTY_PROCEDURE_DETAILS, ProcedureDetailsFields} from './ProcedureDetailsFields';
import {typeRichText} from '../../test/richText';

describe('ProcedureDetailsFields', () => {
  it("keeps the priority level with the title: a blank description line when the title has one", async () => {
    renderWithApp(<ProcedureDetailsFields value={EMPTY_PROCEDURE_DETAILS} onChange={() => {}} titleDescription="Starts as the template's name." />);

    expect(descriptionOf(await screen.findByRole('textbox', {name: /Title/}))).toBe("Starts as the template's name.");
    expect(descriptionOf(screen.getByRole('combobox', {name: /Priority/}))).toBe(BLANK_DESCRIPTION);
  });

  it('gives neither a description line when the title has none', async () => {
    renderWithApp(<ProcedureDetailsFields value={EMPTY_PROCEDURE_DETAILS} onChange={() => {}} />);

    expect(descriptionOf(await screen.findByRole('textbox', {name: /Title/}))).toBeUndefined();
    expect(descriptionOf(screen.getByRole('combobox', {name: /Priority/}))).toBeUndefined();
  });

  it('passes on each change with the other details kept', async () => {
    const onChange = vi.fn();
    renderWithApp(<ProcedureDetailsFields value={EMPTY_PROCEDURE_DETAILS} onChange={onChange} />);

    await typeRichText(await screen.findByRole('textbox', {name: /First note/}), 'x');

    expect(onChange).toHaveBeenLastCalledWith({...EMPTY_PROCEDURE_DETAILS, notes: 'x'});
  });

  it("shows a new procedure's first note box with its hint, which says where the text goes", async () => {
    renderWithApp(<ProcedureDetailsFields value={EMPTY_PROCEDURE_DETAILS} onChange={() => {}} />);

    const box = await screen.findByRole('textbox', {name: /First note/});
    // A shown label (aria-labelledby), not a hidden one (aria-label), which would hide the hint with it.
    expect(box).toHaveAttribute('aria-labelledby');
    expect(box).not.toHaveAttribute('aria-label');
    expect(screen.getByText("Becomes the procedure's first note, by you. More can be added on its page.")).toBeInTheDocument();
  });

  it('has no Notes box for details without notes (editing a procedure)', async () => {
    const details = {title: '', dueDate: '', priority: 'Medium'};
    renderWithApp(<ProcedureDetailsFields value={details} onChange={() => {}} />);

    await screen.findByRole('textbox', {name: /Title/});
    expect(screen.queryByRole('textbox', {name: /First note/})).not.toBeInTheDocument();
  });

  it('passes on a cleared due date as blank', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<ProcedureDetailsFields value={{...EMPTY_PROCEDURE_DETAILS, dueDate: '2099-12-01'}} onChange={onChange} />);

    await user.clear(await screen.findByRole('combobox', {name: /Due Date/}));
    await user.tab();

    expect(onChange).toHaveBeenLastCalledWith({...EMPTY_PROCEDURE_DETAILS, dueDate: ''});
  });
});
