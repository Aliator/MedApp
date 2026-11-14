import {screen} from '@testing-library/react';
import {descriptionOf, renderWithApp} from '../../test/render';
import {ageOn} from '../../utils/dates';
import {DateOfBirthInput} from './DateOfBirthInput';
import {DateOfBirthRangeInput} from './DateOfBirthRangeInput';
import {BLANK_DESCRIPTION} from './fieldSpace';

describe('DateOfBirthInput', () => {
  it('is labelled and shows the age for the chosen date', async () => {
    renderWithApp(<DateOfBirthInput value="1980-03-14" onChange={() => {}} />);

    expect(await screen.findByText(`Age: ${ageOn('1980-03-14')} years`)).toBeInTheDocument();
    expect(screen.getAllByText(/date of birth/i).length).toBeGreaterThan(0);
  });

  it("shows no age without a date, keeping the age's line so the input doesn't jump, or when asked not to", async () => {
    const {unmount} = renderWithApp(<DateOfBirthInput value={undefined} onChange={() => {}} />);
    await screen.findAllByText(/date of birth/i);
    expect(screen.queryByText(/^Age:/)).not.toBeInTheDocument();
    expect(descriptionOf(screen.getByRole('combobox', {name: /date of birth/i}))).toBe(BLANK_DESCRIPTION);
    unmount();

    renderWithApp(<DateOfBirthInput value="1980-03-14" onChange={() => {}} showAge={false} />);
    await screen.findAllByText(/date of birth/i);
    expect(screen.queryByText(/^Age:/)).not.toBeInTheDocument();
  });

  it('shows an error status', async () => {
    renderWithApp(
      <DateOfBirthInput value={undefined} onChange={() => {}} status={{type: 'error', message: 'Date of birth must be provided.'}} />,
    );

    expect(await screen.findByText('Date of birth must be provided.')).toBeInTheDocument();
  });
});

describe('DateOfBirthRangeInput', () => {
  it('is labelled for the patient search', async () => {
    renderWithApp(<DateOfBirthRangeInput value={null} onChange={() => {}} />);

    expect((await screen.findAllByText('Date of birth')).length).toBeGreaterThan(0);
  });
});
