import {fireEvent, render, screen} from '@testing-library/react';
import {useRef} from 'react';
import {pickSaver, SaveShortcutProvider, useSaveShortcut, type Saver} from './SaveShortcut';

/** A form that Ctrl+S can save, with a field in it. */
function Form({name, onSave}: {name: string; onSave?: () => void}) {
  const ref = useRef<HTMLFormElement>(null);
  useSaveShortcut(ref, onSave);
  return (
    <form ref={ref} aria-label={name}>
      <input aria-label={`${name} field`} />
    </form>
  );
}

/** Presses Ctrl+S on an element; returns whether the browser would still have its way (not prevented). */
const pressSave = (target: Element = document.body, init: KeyboardEventInit = {ctrlKey: true}) =>
  fireEvent.keyDown(target, {key: 's', ...init});

describe('SaveShortcutProvider', () => {
  it('saves the form being typed in, and stops the browser saving the page', () => {
    const first = vi.fn();
    const second = vi.fn();
    render(
      <SaveShortcutProvider>
        <Form name="First" onSave={first} />
        <Form name="Second" onSave={second} />
      </SaveShortcutProvider>,
    );
    const field = screen.getByLabelText('Second field');
    field.focus();

    expect(pressSave(field)).toBe(false);
    expect(second).toHaveBeenCalledTimes(1);
    expect(first).not.toHaveBeenCalled();
  });

  it('saves the only form when the focus is outside it, and ignores S without Ctrl or with Alt', () => {
    const save = vi.fn();
    render(
      <SaveShortcutProvider>
        <button type="button">Elsewhere</button>
        <Form name="Only" onSave={save} />
      </SaveShortcutProvider>,
    );
    screen.getByRole('button', {name: 'Elsewhere'}).focus();

    pressSave(document.activeElement!, {});
    pressSave(document.activeElement!, {ctrlKey: true, altKey: true});
    expect(save).not.toHaveBeenCalled();

    pressSave(document.activeElement!);
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('leaves Ctrl+S to the browser when no form is open', () => {
    render(
      <SaveShortcutProvider>
        <Form name="Read only" />
      </SaveShortcutProvider>,
    );

    expect(pressSave()).toBe(true);
  });

  it('stops listening once the form has gone', () => {
    const save = vi.fn();
    const {rerender} = render(
      <SaveShortcutProvider>
        <Form name="Going" onSave={save} />
      </SaveShortcutProvider>,
    );
    rerender(<SaveShortcutProvider>{null}</SaveShortcutProvider>);

    expect(pressSave()).toBe(true);
    expect(save).not.toHaveBeenCalled();
  });

  it('calls the latest save, after the form has changed', () => {
    const before = vi.fn();
    const after = vi.fn();
    const {rerender} = render(
      <SaveShortcutProvider>
        <Form name="Changing" onSave={before} />
      </SaveShortcutProvider>,
    );
    rerender(
      <SaveShortcutProvider>
        <Form name="Changing" onSave={after} />
      </SaveShortcutProvider>,
    );

    pressSave();

    expect(before).not.toHaveBeenCalled();
    expect(after).toHaveBeenCalledTimes(1);
  });

  it('does nothing outside its provider', () => {
    const save = vi.fn();
    render(<Form name="Alone" onSave={save} />);

    expect(pressSave()).toBe(true);
    expect(save).not.toHaveBeenCalled();
  });
});

describe('pickSaver', () => {
  const page = () => {
    document.body.innerHTML = `
      <form id="outer"><input id="outer-field" /><section id="inner"><input id="inner-field" /></section></form>
      <form id="other"><input id="other-field" /></form>
      <dialog id="dialog"><form id="in-dialog"><input id="dialog-field" /></form><button id="dialog-button"></button></dialog>
      <button id="loose"></button>`;
    const el = (id: string) => document.getElementById(id)!;
    const saver = (id: string): Saver => ({element: () => el(id), save: () => {}});
    return {el, saver};
  };

  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('picks the innermost form holding the focus', () => {
    const {el, saver} = page();
    const outer = saver('outer');
    const inner = saver('inner');

    expect(pickSaver([outer, inner], el('inner-field'), document)).toBe(inner);
    expect(pickSaver([inner, outer], el('inner-field'), document)).toBe(inner);
    expect(pickSaver([outer, inner], el('outer-field'), document)).toBe(outer);
  });

  it('saves a form in a dialog from inside it, but never the page behind a dialog', () => {
    const {el, saver} = page();
    const inDialog = saver('in-dialog');
    const other = saver('other');

    expect(pickSaver([other, inDialog], el('dialog-field'), document)).toBe(inDialog);
    expect(pickSaver([other], el('dialog-button'), document)).toBeNull();
    (el('dialog') as HTMLDialogElement).setAttribute('open', '');
    expect(pickSaver([other], document.body, document)).toBeNull();
  });

  it("with the focus in no form, picks the page's only form, or none of two", () => {
    const {el, saver} = page();
    const other = saver('other');

    expect(pickSaver([other, saver('in-dialog')], el('loose'), document)).toBe(other);
    expect(pickSaver([other, saver('outer')], el('loose'), document)).toBeNull();
    expect(pickSaver([other], null, document)).toBe(other);
    expect(pickSaver([{element: () => null, save: () => {}}], el('loose'), document)).toBeNull();
  });
});
