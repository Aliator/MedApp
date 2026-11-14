import {Button} from '@astryxdesign/core/Button';
import {screen, waitFor} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {Page} from './Page';
import {PageHeader} from './PageHeader';

describe('PageHeader', () => {
  it('shows the title as the page heading, with the subtitle and actions', async () => {
    renderWithApp(<PageHeader title="Patients" subtitle="60 patients registered" actions={<Button label="Search" />} />);

    expect(await screen.findByRole('heading', {level: 1, name: 'Patients'})).toBeInTheDocument();
    expect(screen.getByText('60 patients registered')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Search'})).toBeInTheDocument();
  });

  it('has no back button unless asked for one', async () => {
    renderWithApp(<PageHeader title="Patients" />);

    await screen.findByRole('heading', {name: 'Patients'});
    expect(screen.queryByRole('link', {name: 'Back'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Back'})).not.toBeInTheDocument();
  });

  it('navigates back to backHref', async () => {
    const {user, router} = renderWithApp(<PageHeader title="Add patient" backHref="/patients" />, {path: '/patients/add'});

    await user.click(await screen.findByRole('link', {name: 'Back'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('calls onBack', async () => {
    const onBack = vi.fn();
    const {user} = renderWithApp(<PageHeader title="Edit patient" onBack={onBack} backLabel="Back to patient" />);

    await user.click(await screen.findByRole('button', {name: 'Back'}));

    expect(onBack).toHaveBeenCalledOnce();
  });
});

describe('Page', () => {
  it('shows the header and content', async () => {
    renderWithApp(
      <Page title="Patients">
        <p>Table goes here</p>
      </Page>,
    );

    expect(await screen.findByRole('heading', {name: 'Patients'})).toBeInTheDocument();
    expect(screen.getByText('Table goes here')).toBeInTheDocument();
  });

  it('shows a spinner with the loading message instead of the content while loading', async () => {
    renderWithApp(
      <Page title="Patients" isLoading loadingMessage="Loading patients...">
        <p>Table goes here</p>
      </Page>,
    );

    expect(await screen.findByText('Loading patients...')).toBeInTheDocument();
    expect(screen.queryByText('Table goes here')).not.toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Patients'})).toBeInTheDocument();
  });

  it('renders its dialogs even while loading', async () => {
    renderWithApp(<Page title="Patients" isLoading dialogs={<p>A dialog</p>} />);

    expect(await screen.findByText('A dialog')).toBeInTheDocument();
  });
});
