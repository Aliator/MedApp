import {screen, within} from '@testing-library/react';
import {isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {testCase} from '../../test/labCases';
import {HISTOLOGY_TEST_ID, LAB_TESTS, testSpecimen} from '../../test/specimens';
import {OpenLabCaseDialog} from './OpenLabCaseDialog';

const labUser = userWithRoles('test-lab', 'Lab');
const dialog = () => within(openDialog()!);

describe('OpenLabCaseDialog', () => {
  it('opens a case with the test the lab chooses, none chosen to start with', async () => {
    const onClose = vi.fn();
    const fetch = mockApi({
      'GET /api/lab-tests': () => json(LAB_TESTS),
      'POST /api/lab-cases': () => json(testCase()),
    });
    const specimen = testSpecimen({status: 'Received'});
    const {user} = renderWithApp(<OpenLabCaseDialog specimen={specimen} onClose={onClose} />, {user: labUser});

    expect(await screen.findByRole('heading', {name: 'Open Lab Case'})).toBeInTheDocument();
    const tests = await dialog().findAllByRole('radio');
    expect(tests.map((t) => (t as HTMLInputElement).checked)).toEqual([false, false]);
    expect(isDisabled(dialog().getByRole('button', {name: 'Open Lab Case'}))).toBe(true);
    await user.click(dialog().getByRole('radio', {name: 'Skin histology'}));
    await user.click(dialog().getByRole('button', {name: 'Open Lab Case'}));

    await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
    const post = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await post.clone().json()).toEqual({specimenId: specimen.id, labTestId: HISTOLOGY_TEST_ID, expectedVersion: specimen.version});
  });
});
