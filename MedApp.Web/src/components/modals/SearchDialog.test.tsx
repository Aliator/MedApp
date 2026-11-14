import {screen} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {SearchDialog, type SearchField, type SearchValues} from './SearchDialog';

const fields: SearchField[] = [
  {type: 'text', key: 'firstName', label: 'First name', placeholder: 'Search by first name'},
  {type: 'select', key: 'role', label: 'Role', options: [{value: 'Admin', label: 'Admin'}, {value: 'User', label: 'User'}]},
  {type: 'dateRange', key: 'dob', label: 'Date of birth'},
  {type: 'checkbox', key: 'overdue', label: 'Overdue only'},
];

function Search({onSearch, onClear}: {onSearch: (values: SearchValues) => void; onClear?: () => void}) {
  const [values, setValues] = useState<SearchValues>({});
  return (
    <SearchDialog
      isOpen
      title="Search Patients"
      fields={fields}
      values={values}
      onValuesChange={setValues}
      onSearch={() => onSearch(values)}
      onClear={
        onClear &&
        (() => {
          setValues({});
          onClear();
        })
      }
      onClose={() => {}}
    />
  );
}

describe('SearchDialog', () => {
  it('shows a field for each definition', async () => {
    renderWithApp(<Search onSearch={() => {}} />);

    expect(await screen.findByRole('heading', {name: 'Search Patients'})).toBeInTheDocument();
    expect(screen.getByLabelText(/first name/i)).toBeInTheDocument();
    expect(screen.getAllByText('Role').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Date of birth').length).toBeGreaterThan(0);
  });

  it('searches with the entered values, from the button or Enter', async () => {
    const onSearch = vi.fn();
    const {user} = renderWithApp(<Search onSearch={onSearch} />);

    await user.type(await screen.findByLabelText(/first name/i), 'Freya');
    await user.click(screen.getByRole('button', {name: 'Search'}));
    await user.type(screen.getByLabelText(/first name/i), '{Enter}');

    expect(onSearch).toHaveBeenCalledTimes(2);
    expect(onSearch).toHaveBeenLastCalledWith({firstName: 'Freya'});
  });

  it('clears every field', async () => {
    const onClear = vi.fn();
    const {user} = renderWithApp(<Search onSearch={() => {}} onClear={onClear} />);

    const input = await screen.findByLabelText(/first name/i);
    await user.type(input, 'Freya');
    await user.click(screen.getByRole('button', {name: 'Clear'}));

    expect(onClear).toHaveBeenCalledOnce();
    expect(input).toHaveValue('');
  });
});

describe('SearchDialog checkboxes', () => {
  it("records a ticked box as 'true' and an unticked one as ''", async () => {
    const onSearch = vi.fn();
    const {user} = renderWithApp(<Search onSearch={onSearch} />);

    const box = await screen.findByRole('checkbox', {name: 'Overdue only'});
    await user.click(box);
    await user.click(screen.getByRole('button', {name: 'Search'}));
    expect(onSearch).toHaveBeenLastCalledWith(expect.objectContaining({overdue: 'true'}));

    await user.click(box);
    await user.click(screen.getByRole('button', {name: 'Search'}));
    expect(onSearch).toHaveBeenLastCalledWith(expect.objectContaining({overdue: ''}));
  });
});
