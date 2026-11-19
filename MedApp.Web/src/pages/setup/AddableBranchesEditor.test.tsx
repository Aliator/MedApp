import {screen, within} from '@testing-library/react';
import {useState} from 'react';
import {renderWithApp} from '../../test/render';
import {AddableBranchesEditor, branchesToSend, templateBranchRows, type BranchRow} from './AddableBranchesEditor';

const rows = [{key: 'a'}, {key: 'b'}, {key: 'c'}];
const definitions = [
  {id: 'd1', name: 'Excision'},
  {id: 'd2', name: 'Follow-up'},
];

function Harness({onChange}: {onChange: (branches: BranchRow[]) => void}) {
  const [branches, setBranches] = useState<BranchRow[]>([]);
  return (
    <AddableBranchesEditor
      branches={branches}
      onChange={(next) => {
        setBranches(next);
        onChange(next);
      }}
      stages={rows.map((r, i) => ({key: r.key, label: `${i + 1}. Stage ${r.key}`}))}
      definitions={definitions}
      byId={new Map(definitions.map((d) => [d.id, d]))}
      isDisabled={false}
    />
  );
}

describe('AddableBranchesEditor', () => {
  it('adds a branch with a name and stages in order', async () => {
    const onChange = vi.fn();
    const {user} = renderWithApp(<Harness onChange={onChange} />);
    await user.click(await screen.findByRole('button', {name: 'Add a Branch'}));
    const list = within(screen.getByRole('list', {name: 'Branches added on new information'}));
    await user.type(list.getByRole('textbox', {name: 'Name'}), 'Melanoma route');
    for (const name of ['Excision', 'Follow-up']) {
      await user.click(list.getByRole('combobox', {name: /Add a stage to Melanoma route/}));
      await user.click(screen.getByRole('option', {name}));
    }
    expect(list.getByText('Its stages: 1. Excision, 2. Follow-up')).toBeInTheDocument();
    const last = onChange.mock.lastCall![0] as BranchRow[];
    expect(branchesToSend(last, rows)).toEqual([
      {id: null, name: 'Melanoma route', stageDefinitionIds: ['d1', 'd2'], attachAfterStageOrders: [], isRepeatable: false},
    ]);
  });

  it('maps the stages a saved branch may follow onto the rows, and back by order (never the last)', () => {
    const template = {addableBranches: [{id: 'b1', name: 'Repeat', stages: [{stageDefinitionId: 'd1'}], attachAfterStageOrders: [2, 3], isRepeatable: true}]};
    const branches = templateBranchRows(template, rows);
    expect(branches[0].afterKeys).toEqual(['b', 'c']);
    expect(branchesToSend(branches, rows)[0]).toMatchObject({id: 'b1', attachAfterStageOrders: [2], isRepeatable: true});
  });
});
