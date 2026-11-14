import {Button} from '@astryxdesign/core/Button';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {CollapseButton, PanelCard, PanelHeader} from './PanelCard';

describe('PanelCard', () => {
  it('shows a header bar, the body and footer actions', async () => {
    renderWithApp(
      <PanelCard title="Details" headerActions={<Button label="Collapse" />} footerActions={<Button label="View Patient" />}>
        <p>Body</p>
      </PanelCard>,
    );

    expect(await screen.findByRole('heading', {name: 'Details', level: 2})).toBeInTheDocument();
    expect(screen.getByText('Body')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Collapse'})).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'View Patient'})).toBeInTheDocument();
  });

  it('has no footer without footer actions', async () => {
    renderWithApp(
      <PanelCard title="Stages" isBodyPadded={false}>
        <p>Body</p>
      </PanelCard>,
    );

    expect(await screen.findByText('Body')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('PanelHeader and CollapseButton', () => {
  it('shows a section title with a button that folds it', async () => {
    const onToggle = vi.fn();
    const {user} = renderWithApp(
      <PanelHeader title="Stages" actions={<CollapseButton name="stages" isOpen={false} onToggle={onToggle} />} />,
    );

    expect(await screen.findByRole('heading', {name: 'Stages', level: 2})).toBeInTheDocument();
    const toggle = screen.getByRole('button', {name: 'Show stages'});
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await user.click(toggle);

    expect(onToggle).toHaveBeenCalled();
  });

  it('says Hide when the section is open', async () => {
    renderWithApp(<CollapseButton name="details" isOpen onToggle={() => {}} />);

    expect(await screen.findByRole('button', {name: 'Hide details'})).toHaveAttribute('aria-expanded', 'true');
  });
});
