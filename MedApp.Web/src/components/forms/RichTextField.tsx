import {$generateJSONFromSelectedNodes} from '@lexical/clipboard';
import {
  editorStateJSONToMarkdown,
  markdownToEditorStateJSON,
  RichTextEditor,
  RichTextEditorToolbar,
  type RichTextEditorRef,
} from '@astryxdesign/richtext';
import {$getNodeByKey, $getSelection, $isRangeSelection, $setSelection} from 'lexical';
import {useImperativeHandle, useRef, useState, type Ref} from 'react';
import {NO_FIELD_STATUS_LINE} from './fieldSpace';

/** A part of a rich text box's text that's selected: as Markdown, and a way to take exactly that part out. */
export interface RichTextSelection {
  /** The selected part as Markdown, keeping its formatting (bold, a whole list item's bullet). */
  markdown: string;
  /**
   * Takes exactly the selected part out of the box, as pressing Delete would, and returns what the box then says as
   * Markdown. Null, and nothing taken out, when the box has changed since it was selected.
   */
  remove: () => string | null;
}

/** What a `RichTextField` lets its page do: put the cursor in it, and read or take out what's selected. */
export interface RichTextFieldHandle {
  focus: () => void;
  /** What's selected in the box now, or null when nothing is. */
  selection: () => RichTextSelection | null;
}

export interface RichTextFieldProps {
  label: string;
  /** The text to start from, as Markdown. Read once: give the field a `key` that changes to start it again. */
  value: string;
  /** Called with the text as Markdown on each change. */
  onChange: (markdown: string) => void;
  description?: string;
  placeholder?: string;
  isLabelHidden?: boolean;
  isRequired?: boolean;
  isDisabled?: boolean;
  status?: {type: 'error' | 'warning' | 'success'; message?: string};
  /** The writing area's least height (it grows with its text). */
  minHeight?: number | string;
  ref?: Ref<RichTextFieldHandle>;
}

/**
 * The toolbar's buttons wrap onto another row where the box is narrow (the notepad, a phone), rather than scrolling
 * sideways out of sight with nothing to say there are more. Astryx's toolbar scrolls its row (`overflow-x: auto`),
 * with no prop to wrap it, so this overrides that one layout rule, for MedApp's boxes only.
 */
export const WRAPPING_TOOLBAR_CSS =
  '.medapp-rich-text-toolbar [role="group"] { flex-wrap: wrap !important; overflow: visible !important; row-gap: var(--spacing-1); }';

/**
 * A box for formatted text (stage notes and instructions, procedure notes, the notepad): bold, italics, lists, quotes
 * and links, from its toolbar or by typing Markdown (`**bold**`, `- ` for a list). It's kept as Markdown, so text
 * written before, as plain text, opens as it was. Show it read-only with `RichText`.
 *
 * It isn't controlled: `value` is where it starts, and `onChange` reports each change. Its `ref` puts the cursor in it,
 * and reads what's selected as Markdown and takes exactly that out (the notepad's Add Note to Procedure).
 */
export function RichTextField({value, onChange, minHeight = '7.5rem', ref, ...field}: RichTextFieldProps) {
  const editor = useRef<RichTextEditorRef>(null);
  // Read once, as it opens.
  const [start] = useState(() => markdownToEditorStateJSON(value));

  useImperativeHandle(ref, () => ({
    focus: () => {
      // The box itself, then Lexical's own focus, which puts the caret in it (on its own it only moves the selection).
      editor.current?.getEditor().getRootElement()?.focus({preventScroll: true});
      editor.current?.focus();
    },
    selection: () => (editor.current ? selectionOf(editor.current) : null),
  }));

  return (
    <>
      <style data-medapp-rich-text="">{WRAPPING_TOOLBAR_CSS}</style>
      <RichTextEditor
        {...field}
        ref={editor}
        defaultValue={start}
        onChange={() => {
          const markdown = editor.current?.getMarkdown();
          if (markdown !== undefined) onChange(markdown);
        }}
        // Its block format is a field, which never shows an error: no reserved line under it.
        toolbar={<RichTextEditorToolbar className="medapp-rich-text-toolbar" style={NO_FIELD_STATUS_LINE} />}
        minHeight={minHeight}
        width="100%"
      />
    </>
  );
}

/** Block nodes, which can sit in a document's root; anything else (text, a link, a line break) goes in a paragraph. */
const BLOCKS = new Set(['paragraph', 'heading', 'quote', 'list', 'code', 'table', 'horizontalrule']);

interface SerializedNode {
  type: string;
  children?: SerializedNode[];
  [key: string]: unknown;
}

const paragraphOf = (children: SerializedNode[]): SerializedNode => ({
  children,
  direction: null,
  format: '',
  indent: 0,
  type: 'paragraph',
  version: 1,
  textFormat: 0,
  textStyle: '',
});

/** The selected nodes as a document of their own, as Markdown: a run of text (part of one paragraph) in a paragraph. */
export function selectedMarkdown(nodes: SerializedNode[]): string {
  const children: SerializedNode[] = [];
  let paragraph: SerializedNode | null = null;
  for (const node of nodes) {
    if (BLOCKS.has(node.type)) {
      paragraph = null;
      children.push(node);
    } else {
      if (!paragraph) {
        paragraph = paragraphOf([]);
        children.push(paragraph);
      }
      paragraph.children!.push(node);
    }
  }
  const root = {children, direction: null, format: '', indent: 0, type: 'root', version: 1};
  return editorStateJSONToMarkdown(JSON.stringify({root})).trim();
}

/**
 * What's selected in the editor, from Lexical's own selection (as copying does, `$generateJSONFromSelectedNodes`), so
 * it keeps its formatting; and a remover that deletes that same range by its position, never by searching for its text.
 */
function selectionOf(field: RichTextEditorRef): RichTextSelection | null {
  const lexical = field.getEditor();
  const taken = lexical.read(() => {
    const selection = $getSelection();
    if (!$isRangeSelection(selection) || selection.isCollapsed()) return null;
    return {nodes: $generateJSONFromSelectedNodes<SerializedNode>(lexical, selection).nodes, range: selection.clone()};
  });
  if (!taken) return null;
  const markdown = selectedMarkdown(taken.nodes);
  if (!markdown) return null;

  const before = field.getMarkdown();
  return {
    markdown,
    remove: () => {
      // Only from the very text it was selected in: if anything has changed since, it's not done.
      if (field.getMarkdown() !== before) return null;
      let isRemoved = false;
      lexical.update(
        () => {
          const range = taken.range.clone();
          if (!$getNodeByKey(range.anchor.key) || !$getNodeByKey(range.focus.key)) return;
          $setSelection(range);
          range.removeText();
          isRemoved = true;
        },
        {discrete: true},
      );
      return isRemoved ? field.getMarkdown() : null;
    },
  };
}
