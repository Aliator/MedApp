import {markdownToEditorStateJSON, RichTextEditor, RichTextEditorToolbar, type RichTextEditorRef} from '@astryxdesign/richtext';
import {useRef, useState} from 'react';
import {NO_FIELD_STATUS_LINE} from './fieldSpace';

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
}

/**
 * A box for formatted text (stage notes and instructions, procedure notes): bold, italics, lists, quotes and links,
 * from its toolbar or by typing Markdown (`**bold**`, `- ` for a list). It's kept as Markdown, so text written
 * before, as plain text, opens as it was. Show it read-only with `RichText`.
 *
 * It isn't controlled: `value` is where it starts, and `onChange` reports each change.
 */
export function RichTextField({value, onChange, minHeight = '7.5rem', ...field}: RichTextFieldProps) {
  const editor = useRef<RichTextEditorRef>(null);
  // Read once, as it opens.
  const [start] = useState(() => markdownToEditorStateJSON(value));

  return (
    <RichTextEditor
      {...field}
      ref={editor}
      defaultValue={start}
      onChange={() => {
        const markdown = editor.current?.getMarkdown();
        if (markdown !== undefined) onChange(markdown);
      }}
      // Its block format is a field, which never shows an error: no reserved line under it.
      toolbar={<RichTextEditorToolbar style={NO_FIELD_STATUS_LINE} />}
      minHeight={minHeight}
      width="100%"
    />
  );
}
