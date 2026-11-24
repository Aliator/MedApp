import {screen, within} from '@testing-library/react';
import {givenWidth} from '../../test/layout';
import {renderWithApp} from '../../test/render';
import {AppIcon} from '../shared/icons';
import {ActionsList, ActionsListItem} from './ActionsList';
import {TableActions} from './TableActions';

const list = (
  <ActionsList aria-label="Consent forms" hasDividers>
    {['Clinical photography', 'Consent to a procedure'].map((name) => (
      <ActionsListItem
        key={name}
        label={<strong>{name}</strong>}
        actions={
          <TableActions
            actions={['Edit', 'Archive', 'Delete'].map((action) => ({label: `${action} ${name}`, icon: AppIcon.Edit, onClick: () => {}}))}
          />
        }
      />
    ))}
  </ActionsList>
);

describe('ActionsList', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows each row with its actions as buttons at the end', async () => {
    givenWidth(700);
    renderWithApp(list);

    const rows = await screen.findAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText('Clinical photography')).toBeInTheDocument();
    expect(within(rows[0]).getByRole('button', {name: 'Delete Clinical photography'})).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Actions'})).not.toBeInTheDocument();
  });

  it('puts the actions under the name in a narrow list (a phone), more than two in one Actions menu', async () => {
    givenWidth(358);
    const {user} = renderWithApp(list);

    const row = within((await screen.findAllByRole('listitem'))[1]);
    const menu = row.getByRole('button', {name: 'Actions'});
    expect(row.getByText('Consent to a procedure').compareDocumentPosition(menu) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    await user.click(menu);
    expect(await screen.findByRole('menuitem', {name: 'Archive Consent to a procedure'})).toBeInTheDocument();
  });
});
