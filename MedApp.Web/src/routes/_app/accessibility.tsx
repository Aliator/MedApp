import {createFileRoute} from '@tanstack/react-router';
import {AccessibilityStatementPage} from '../../pages/accessibility/AccessibilityStatementPage';

export const Route = createFileRoute('/_app/accessibility')({
  component: AccessibilityStatementPage,
});
