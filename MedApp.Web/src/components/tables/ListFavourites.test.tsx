import {screen, waitFor, within} from '@testing-library/react';
import {openDialog, pickFavourite, renderWithApp} from '../../test/render';
import {ListFavourites, type ListFavouritesProps} from './ListFavourites';

const KEY = 'medapp.favourites';
const same = (values: Record<string, string | boolean | undefined>) => values;

function renderFavourites(props: Partial<ListFavouritesProps> = {}) {
  const onApply = vi.fn();
  const result = renderWithApp(
    <ListFavourites list="patients" current={{}} normalize={same} onApply={onApply} isUnfiltered={false} {...props} />,
  );
  return {...result, onApply};
}

const savedFor = (list: string) => JSON.parse(localStorage.getItem(KEY) ?? '{}')[list];

describe('ListFavourites', () => {
  it("offers the list's quick filters, and applies one", async () => {
    const {user, onApply} = renderFavourites({quick: [{label: 'Over 65', values: {dobTo: '1961-11-15'}}]});

    await pickFavourite(user, 'Over 65');

    expect(onApply).toHaveBeenCalledWith({dobTo: '1961-11-15'});
  });

  it('saves the search, by name, and can make it the starting search', async () => {
    const {user} = renderFavourites({current: {last: 'bennett'}});

    await pickFavourite(user, 'Save this search…');
    const dialog = within(openDialog()!);
    await user.type(dialog.getByRole('textbox', {name: /^Name/}), 'The Bennetts');
    await user.click(dialog.getByRole('switch', {name: 'Start the list on it'}));
    await user.click(dialog.getByRole('button', {name: 'Save'}));

    const saved = savedFor('patients');
    expect(saved.saved).toEqual([{id: expect.any(String), name: 'The Bennetts', values: {last: 'bennett'}}]);
    expect(saved.startId).toBe(saved.saved[0].id);
  });

  it("names the favourite the list is showing, beside the star, and can't save it twice", async () => {
    localStorage.setItem(KEY, JSON.stringify({patients: {saved: [{id: 'f1', name: 'The Bennetts', values: {last: 'bennett'}}]}}));
    const {user} = renderFavourites({current: {last: 'bennett'}});

    const star = await screen.findByRole('button', {name: 'Favourites (showing The Bennetts)'});
    // Its name, as a token beside the star (and in the menu, under Your favourites).
    expect(screen.getAllByText('The Bennetts').length).toBeGreaterThan(0);

    await user.click(star);
    const menu = await waitFor(() => document.querySelector<HTMLElement>(`[aria-labelledby="${CSS.escape(star.id)}"]`)!);
    expect(within(menu).getByRole('menuitem', {name: /Save this search/, hidden: true})).toHaveAttribute('aria-disabled', 'true');
  });

  it('takes its starting search when the list opens with no filters', async () => {
    localStorage.setItem(KEY, JSON.stringify({patients: {saved: [{id: 'f1', name: 'Mine', values: {first: 'fr'}}], startId: 'f1'}}));
    const {onApply} = renderFavourites({isUnfiltered: true});

    await waitFor(() => expect(onApply).toHaveBeenCalledWith({first: 'fr'}));
  });

  it('leaves a list opened with filters as it is', async () => {
    localStorage.setItem(KEY, JSON.stringify({patients: {saved: [{id: 'f1', name: 'Mine', values: {first: 'fr'}}], startId: 'f1'}}));
    const {onApply} = renderFavourites({isUnfiltered: false, current: {last: 'ali'}});

    await screen.findByRole('button', {name: /^Favourites/});
    expect(onApply).not.toHaveBeenCalled();
  });

  it('renames, changes the starting search of, and deletes favourites', async () => {
    localStorage.setItem(
      KEY,
      JSON.stringify({
        patients: {
          saved: [
            {id: 'f1', name: 'One', values: {first: 'a'}},
            {id: 'f2', name: 'Two', values: {first: 'b'}},
          ],
          startId: 'f1',
        },
      }),
    );
    const {user} = renderFavourites();

    await pickFavourite(user, 'Manage favourites…');
    const dialog = within(openDialog()!);
    const names = dialog.getAllByRole('textbox', {name: 'Name'});
    await user.clear(names[1]);
    await user.type(names[1], 'Second');
    await user.click(dialog.getAllByRole('switch', {name: 'Starting search'})[1]);
    await user.click(dialog.getByRole('button', {name: 'Delete One'}));

    expect(savedFor('patients')).toEqual({saved: [{id: 'f2', name: 'Second', values: {first: 'b'}}], startId: 'f2'});
  });
});

describe('ListFavourites, more', () => {
  const one = {patients: {saved: [{id: 'f1', name: 'One', values: {first: 'a'}}], startId: 'f1'}};

  it('applies a saved favourite', async () => {
    localStorage.setItem(KEY, JSON.stringify({patients: {saved: one.patients.saved}}));
    const {user, onApply} = renderFavourites();

    await pickFavourite(user, 'One');

    expect(onApply).toHaveBeenCalledWith({first: 'a'});
  });

  it('saves nothing when saving is cancelled', async () => {
    const {user} = renderFavourites({current: {last: 'test'}});

    await pickFavourite(user, 'Save this search…');
    await user.type(within(openDialog()!).getByRole('textbox', {name: /^Name/}), 'Test search');
    await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('can start the list on no saved search, says when none are left, and closes', async () => {
    localStorage.setItem(KEY, JSON.stringify(one));
    const {user} = renderFavourites();

    await pickFavourite(user, 'Manage favourites…');
    const dialog = within(openDialog()!);
    await user.click(dialog.getByRole('switch', {name: 'Starting search'}));
    expect(savedFor('patients').startId).toBeUndefined();
    await user.click(dialog.getByRole('button', {name: 'Delete One'}));
    expect(dialog.getByText('No favourites saved.')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Done'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it('closes the manage dialog with Close too', async () => {
    localStorage.setItem(KEY, JSON.stringify(one));
    const {user} = renderFavourites();

    await pickFavourite(user, 'Manage favourites…');
    // The footer's Close (the header has a close button too).
    const close = within(openDialog()!).getAllByRole('button', {name: 'Close'});
    await user.click(close[close.length - 1]);

    await waitFor(() => expect(openDialog()).toBeNull());
    expect(savedFor('patients').saved).toHaveLength(1);
  });
});
