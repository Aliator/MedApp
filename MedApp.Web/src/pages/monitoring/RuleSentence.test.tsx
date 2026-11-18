import {render, screen} from '@testing-library/react';
import {RuleSentence} from './RuleSentence';

describe('RuleSentence', () => {
  it('shows the sentence, with each choice in its own bold text', () => {
    const {container} = render(
      <RuleSentence rule={{event: 'PathwayCreated', subjectUserId: 'u1', subjectUsername: 'sam', minimumPriority: 'Urgent'}} />,
    );

    expect(container).toHaveTextContent('Notify me when sam creates an Urgent priority pathway');
    // Each choice is an element of its own (the bold text), inside the sentence.
    expect(screen.getByText('sam')).toHaveTextContent(/^sam$/);
    expect(screen.getByText('Urgent')).toHaveTextContent(/^Urgent$/);
  });
});
