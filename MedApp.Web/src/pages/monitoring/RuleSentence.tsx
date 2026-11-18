import {Text} from '@astryxdesign/core/Text';
import {describeRule, type RuleDescription} from './ruleText';

/**
 * A monitoring rule as a plain sentence, with the choices in bold: "Notify me when **Sam** finishes **Bloods** on a
 * **High or Urgent** priority pathway". The rules list and the editor's preview both use it.
 */
export function RuleSentence({rule}: {rule: RuleDescription}) {
  return (
    <Text type="body">
      {describeRule(rule).map((part, i) =>
        part.strong ? (
          <Text key={i} type="body" weight="semibold">
            {part.text}
          </Text>
        ) : (
          part.text
        ),
      )}
    </Text>
  );
}
