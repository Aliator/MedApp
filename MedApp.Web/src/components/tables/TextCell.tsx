import {Text} from '@astryxdesign/core/Text';

export interface TextCellProps {
  text: string | null | undefined;
  /** How many lines it may take before truncating (with the whole text in a tooltip). */
  maxLines?: number;
}

/**
 * Plain text in a table cell, kept to its lines: truncated with an ellipsis, and the whole text in a tooltip.
 * DataTable rows are a fixed height, so a cell's text never wraps onto lines the row doesn't have.
 */
export function TextCell({text, maxLines = 1}: TextCellProps) {
  return (
    <Text type="body" maxLines={maxLines}>
      {text ?? ''}
    </Text>
  );
}
