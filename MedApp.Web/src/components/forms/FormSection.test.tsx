import {TextInput} from '@astryxdesign/core/TextInput';
import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {HalfViewContext} from '../pages/halfView';
import {FormSection} from './FormSection';

const fields = (
  <>
    <TextInput label="First name" value="" onChange={() => {}} />
    <TextInput label="Last name" value="" onChange={() => {}} />
  </>
);

const grid = () => screen.getByRole('textbox', {name: /First name/}).closest('.astryx-grid');

describe('FormSection', () => {
  it('shows its title and fields', async () => {
    renderWithApp(<FormSection title="Personal Information">{fields}</FormSection>);

    expect(await screen.findByText('Personal Information')).toBeInTheDocument();
    expect(screen.getByRole('textbox', {name: /Last name/})).toBeInTheDocument();
  });

  it('lays the fields out in two fixed columns in a wide layout', async () => {
    renderWithApp(<FormSection title="Details">{fields}</FormSection>);

    await screen.findByRole('textbox', {name: /First name/});
    expect(grid()).toHaveAttribute('data-columns', '2');
  });

  it('lets the columns reflow to one in a narrow layout (a phone, a half view)', async () => {
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <FormSection title="Details">{fields}</FormSection>
      </HalfViewContext.Provider>,
    );

    await screen.findByRole('textbox', {name: /First name/});
    expect(grid()).not.toBeNull();
    expect(grid()).not.toHaveAttribute('data-columns');
  });

  it('puts the fields one under another without the grid', async () => {
    renderWithApp(<FormSection useGrid={false}>{fields}</FormSection>);

    await screen.findByRole('textbox', {name: /First name/});
    expect(grid()).toBeNull();
  });
});
