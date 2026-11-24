import {Button} from '@astryxdesign/core/Button';
import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {DisabledReason} from './DisabledReason';

const REASON = 'Save the letter first';

describe('DisabledReason', () => {
  it('describes the disabled control with its reason, kept shut until asked', async () => {
    renderWithApp(
      <DisabledReason reason={REASON}>
        <Button label="Approve" isDisabled />
      </DisabledReason>,
    );

    expect(await screen.findByRole('button', {name: 'Approve'})).toHaveAccessibleDescription(REASON);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('opens the reason on a click, for a pointer that cannot hover', async () => {
    const {user} = renderWithApp(
      <DisabledReason reason={REASON}>
        <Button label="Approve" isDisabled />
      </DisabledReason>,
    );

    await user.pointer({keys: '[MouseLeft]', target: await screen.findByRole('button', {name: 'Approve'})});
    await waitFor(() => expect(screen.getByRole('tooltip')).toHaveTextContent(REASON));
  });

  it('opens the reason on hover', async () => {
    const {user} = renderWithApp(
      <DisabledReason reason={REASON}>
        <Button label="Approve" isDisabled />
      </DisabledReason>,
    );

    await user.hover(await screen.findByRole('button', {name: 'Approve'}));
    expect(await screen.findByRole('tooltip')).toHaveTextContent(REASON);
  });

  it('leaves a working control as it is', async () => {
    const {user} = renderWithApp(
      <DisabledReason reason={undefined}>
        <Button label="Approve" />
      </DisabledReason>,
    );

    const button = await screen.findByRole('button', {name: 'Approve'});
    expect(button).not.toHaveAccessibleDescription();
    await user.click(button);
    expect(screen.queryByRole('tooltip', {hidden: true})).not.toBeInTheDocument();
  });
});
