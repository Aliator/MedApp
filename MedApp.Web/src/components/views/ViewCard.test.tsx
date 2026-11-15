import {Button} from '@astryxdesign/core/Button';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {ViewCard} from './ViewCard';
import {ViewSection} from './ViewSection';

describe('ViewCard and ViewSection', () => {
  it('shows titled sections of labelled values, and footer actions', async () => {
    renderWithApp(
      <ViewCard footerActions={<Button label="Edit" />}>
        <ViewSection title="Personal Information">
          <MetadataListItem label="First Name">Freya</MetadataListItem>
          <MetadataListItem label="Last Name">Ahmed</MetadataListItem>
        </ViewSection>
      </ViewCard>,
    );

    expect(await screen.findByRole('heading', {name: 'Personal Information'})).toBeInTheDocument();
    expect(screen.getByText('First Name')).toBeInTheDocument();
    expect(screen.getByText('Freya')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Edit'})).toBeInTheDocument();
  });

  it('has no footer without actions, or when hidden', async () => {
    const {container} = renderWithApp(
      <ViewCard footerActions={<Button label="Edit" />} showFooter={false}>
        <ViewSection useGrid={false}>
          <MetadataListItem label="Email">freya.ahmed18@example.com</MetadataListItem>
        </ViewSection>
      </ViewCard>,
    );

    expect(await screen.findByText('freya.ahmed18@example.com')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Edit'})).not.toBeInTheDocument();
    expect(screen.queryByRole('heading')).not.toBeInTheDocument();
    // Not filling: its own height.
    expect(container.querySelector('[style*="height: 100%"]')).toBeNull();
  });

  it('fills the height it is given, its footer still there', async () => {
    const {container} = renderWithApp(
      <ViewCard fill footerActions={<Button label="Edit" />}>
        <ViewSection title="Details">
          <MetadataListItem label="Email">test.patient@example.org</MetadataListItem>
        </ViewSection>
      </ViewCard>,
    );

    expect(await screen.findByRole('button', {name: 'Edit'})).toBeInTheDocument();
    expect(screen.getByText('test.patient@example.org')).toBeInTheDocument();
    expect(container.querySelectorAll('[style*="height: 100%"]').length).toBeGreaterThan(0);
  });
});
