import {screen, within} from '@testing-library/react';
import {isDisabled, renderWithApp} from '../../test/render';
import {PageControls, pageRange} from './PageControls';

describe('pageRange', () => {
  it('shows every page when there are few', () => {
    expect(pageRange(1, 5)).toEqual([1, 2, 3, 4, 5]);
    expect(pageRange(4, 7)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });

  it('shows the first and last pages and the ones around the current page, with gaps between', () => {
    expect(pageRange(1, 20)).toEqual([1, 2, 3, 4, 5, 'gap', 20]);
    expect(pageRange(10, 20)).toEqual([1, 'gap', 9, 10, 11, 'gap', 20]);
    expect(pageRange(20, 20)).toEqual([1, 'gap', 16, 17, 18, 19, 20]);
  });
});

describe('PageControls', () => {
  const renderControls = (page: number, totalPages = 20) => {
    const onPageChange = vi.fn();
    return {onPageChange, ...renderWithApp(<PageControls page={page} totalPages={totalPages} onPageChange={onPageChange} label="Table pagination" />)};
  };

  it('goes to a page, the next or the previous, marking the current one', async () => {
    const {user, onPageChange} = renderControls(1);

    const nav = await screen.findByRole('navigation', {name: 'Table pagination'});
    expect(within(nav).getByRole('button', {name: 'Go to page 1'})).toHaveAttribute('aria-current', 'page');
    expect(isDisabled(within(nav).getByRole('button', {name: 'Go to previous page'}))).toBe(true);

    await user.click(within(nav).getByRole('button', {name: 'Go to page 3'}));
    await user.click(within(nav).getByRole('button', {name: 'Go to next page'}));
    expect(onPageChange.mock.calls).toEqual([[3], [2]]);
  });

  it('opens a box from "…" to type any page number into', async () => {
    const {user, onPageChange} = renderControls(10);

    await user.click((await screen.findAllByRole('button', {name: 'Go to a page'}))[1]);
    await user.type(await screen.findByRole('textbox', {name: /Page/}), '17{Enter}');

    expect(onPageChange).toHaveBeenCalledWith(17);
  });

  it('says which pages there are when the number is not one', async () => {
    const {user, onPageChange} = renderControls(10);

    await user.click((await screen.findAllByRole('button', {name: 'Go to a page'}))[0]);
    await user.type(await screen.findByRole('textbox', {name: /Page/}), '42{Enter}');

    expect((await screen.findAllByText('Enter a page from 1 to 20.')).length).toBeGreaterThan(0);
    expect(onPageChange).not.toHaveBeenCalled();
  });
});
