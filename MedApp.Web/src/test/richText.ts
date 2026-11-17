import {$convertFromMarkdownString, TRANSFORMERS} from '@lexical/markdown';
import {act} from '@testing-library/react';
import type {LexicalEditor} from 'lexical';

/**
 * Rich text boxes (`RichTextField`) are Lexical editors, which user-event can't type into under jsdom (it has no
 * layout). These reach the box's editor instead, as typing would. `box` is its text box, e.g.
 * `screen.getByRole('textbox', {name: 'Notes'})`.
 */
const editorOf = (box: HTMLElement) => {
  const editor = (box as HTMLElement & {__lexicalEditor?: LexicalEditor}).__lexicalEditor;
  if (!editor) throw new Error('Not a rich text box.');
  return editor;
};

/** Replaces what the box says with this Markdown, as if typed. */
export async function setRichText(box: HTMLElement, markdown: string) {
  await act(async () => {
    editorOf(box).update(() => $convertFromMarkdownString(markdown, TRANSFORMERS), {discrete: true});
  });
}

/** What the box says, as plain text. */
export const richText = (box: HTMLElement) => box.textContent ?? '';

/** Adds plain text to the end of what the box says, as typing at its end would. */
export const typeRichText = (box: HTMLElement, text: string) => setRichText(box, richText(box) + text);

/**
 * Selects the `occurrence`th copy (from 0) of `text` in the box, as dragging over it would, even across formatting (a
 * bold word) or lines: the browser's selection, which the box's editor follows.
 */
export async function selectRichText(box: HTMLElement, text: string, occurrence = 0) {
  const walker = document.createTreeWalker(box, NodeFilter.SHOW_TEXT);
  const pieces: {node: Text; from: number}[] = [];
  let all = '';
  for (let node = walker.nextNode() as Text | null; node; node = walker.nextNode() as Text | null) {
    pieces.push({node, from: all.length});
    all += node.data;
  }
  let at = -1;
  for (let i = 0; i <= occurrence; i++) {
    at = all.indexOf(text, at + 1);
    if (at < 0) throw new Error(`"${text}" isn't in the box ${occurrence + 1} times.`);
  }
  const point = (offset: number, isEnd: boolean) => {
    const piece = pieces.findLast((p) => (isEnd ? p.from < offset : p.from <= offset))!;
    return [piece.node, offset - piece.from] as const;
  };
  const range = document.createRange();
  range.setStart(...point(at, false));
  range.setEnd(...point(at + text.length, true));
  await act(async () => {
    box.focus();
    document.getSelection()!.removeAllRanges();
    document.getSelection()!.addRange(range);
    document.dispatchEvent(new Event('selectionchange'));
  });
}
