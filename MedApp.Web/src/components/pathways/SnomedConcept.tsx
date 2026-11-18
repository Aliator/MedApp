import {VStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {Text} from '@astryxdesign/core/Text';

/** A stage's SNOMED CT concept, as the API gives it: both or neither. */
export interface SnomedConceptProps {
  code?: string | null;
  term?: string | null;
}

/** "80146002 | Appendectomy" (SNOMED CT's own way of writing a concept with its term), the code alone, or null. */
export function snomedLabel({code, term}: SnomedConceptProps): string | null {
  const id = code?.trim();
  if (!id) return null;
  const name = term?.trim();
  return name ? `${id} | ${name}` : id;
}

/**
 * A stage's SNOMED CT code and term, on a small line of its own, e.g. "SNOMED CT: 80146002 | Appendectomy", under the
 * stage's name or details: on templates, and on a pathway's stages (which keep the copy made when the pathway
 * was created). Shows nothing when the stage has no code.
 */
export function SnomedConcept(props: SnomedConceptProps) {
  const label = snomedLabel(props);
  if (!label) return null;
  return (
    <Text type="supporting" color="secondary">
      SNOMED CT: {label}
    </Text>
  );
}

/**
 * The same in a table's cell (the stage definitions list): the code, with the term under it, or a dash when there's
 * none. Kept to a line each, with the whole text in a tooltip.
 */
export function SnomedCell({code, term}: SnomedConceptProps) {
  const id = code?.trim();
  if (!id) return <Text type="body" color="secondary">—</Text>;
  return (
    <VStack gap={0}>
      <Text type="body" maxLines={1} hasTabularNumbers>
        {id}
      </Text>
      {term?.trim() && (
        <Text type="supporting" color="secondary" maxLines={1}>
          {term.trim()}
        </Text>
      )}
    </VStack>
  );
}

/**
 * A list item's description for a stage (a `ListItem`'s `description`, e.g. the template editor's lists): its own
 * description, with its SNOMED CT concept on a line under it when it has one. Without a concept it's the description
 * as it is, so the item shows and truncates it as usual.
 */
export function stageDescription(description: string | null | undefined, concept: SnomedConceptProps): ReactNode {
  if (!snomedLabel(concept)) return description ?? undefined;
  return (
    // A span, as the item's description is one.
    <VStack as="span" gap={0}>
      {description && <Text type="inherit">{description}</Text>}
      <SnomedConcept {...concept} />
    </VStack>
  );
}
