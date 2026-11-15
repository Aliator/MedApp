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
