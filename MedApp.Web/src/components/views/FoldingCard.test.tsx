import {Button} from '@astryxdesign/core/Button';
import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {FoldingCard} from './FoldingCard';

function Example() {
  const [open, setOpen] = useState({details: true, stages: false});
  return (
    <FoldingCard
      footerActions={<Button label="View Patient" />}
      sections={[
        {key: 'details', title: 'Details', isOpen: open.details, onToggle: () => setOpen({...open, details: !open.details}), children: <p>Details body</p>},
        {key: 'stages', title: 'Stages', isOpen: open.stages, onToggle: () => setOpen({...open, stages: !open.stages}), children: <p>Stages body</p>},
      ]}
    />
  );
}

describe('FoldingCard', () => {
  it('shows a header bar per section, with the bodies of the open ones', async () => {
    renderWithApp(<Example />);

    expect(await screen.findByRole('heading', {name: 'Details', level: 2})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Stages', level: 2})).toBeInTheDocument();
    expect(screen.getByText('Details body')).toBeInTheDocument();
    expect(screen.queryByText('Stages body')).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'View Patient'})).toBeInTheDocument();
  });

  it('folds a section when its header bar is clicked anywhere, toggling once from the chevron', async () => {
    const {user} = renderWithApp(<Example />);

    await user.click(await screen.findByRole('heading', {name: 'Details'}));
    expect(screen.queryByText('Details body')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Show details'}));
    expect(screen.getByText('Details body')).toBeInTheDocument();
  });

  it('folds and opens each section from its bar', async () => {
    const {user} = renderWithApp(<Example />);

    await user.click(await screen.findByRole('button', {name: 'Show stages'}));
    expect(screen.getByText('Stages body')).toBeInTheDocument();
    expect(screen.getByText('Details body')).toBeInTheDocument();

    await user.click(screen.getByRole('button', {name: 'Hide details'}));
    expect(screen.queryByText('Details body')).not.toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Show details'})).toHaveAttribute('aria-expanded', 'false');
  });

  it("keeps a section that can't fold open, with no chevron, and shows a summary in a bar", async () => {
    const {user} = renderWithApp(
      <FoldingCard
        sections={[
          {key: 'details', title: 'Details', isOpen: true, canFold: false, onToggle: () => {}, children: <p>Details body</p>},
          {key: 'stages', title: 'Stages', isOpen: false, onToggle: () => {}, summary: <p>3 of 5</p>, children: <p>Stages body</p>},
        ]}
      />,
    );

    await user.click(await screen.findByRole('heading', {name: 'Details'}));
    expect(screen.getByText('Details body')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: /details/})).not.toBeInTheDocument();
    expect(screen.getByText('3 of 5')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Show stages'})).toHaveAttribute('aria-expanded', 'false');
  });
});
