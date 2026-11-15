import {Markdown} from '@astryxdesign/core/Markdown';
import {markdownSoftBreaksPlugin} from '@astryxdesign/core/Markdown/plugins';

/** Each line ending is a line break, so notes typed as plain text (before rich text) keep their lines. */
const PLUGINS = [markdownSoftBreaksPlugin];

/**
 * Formatted text written in a `RichTextField` (stage notes and instructions, procedure notes), read-only. It's
 * Markdown: bold, italics, lists, quotes and links show formatted; anything that isn't Markdown (notes written
 * before) shows as it was typed, line by line. Raw HTML never runs, and unsafe links show as plain text.
 */
export function RichText({children}: {children: string}) {
  return (
    <Markdown plugins={PLUGINS} density="compact" headingLevelStart={4} contentWidth="100%">
      {children}
    </Markdown>
  );
}
