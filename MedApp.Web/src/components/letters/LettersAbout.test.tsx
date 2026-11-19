import {screen, waitFor, within} from '@testing-library/react';
import {json, mockApi, openDialog, renderWithApp, staffUser, userWithRoles} from '../../test/render';
import {LETTER_PATIENT, LETTER_PATHWAY_ID, letterPage, testLetter, testLetterSummary} from '../../test/letters';
import {LettersAbout} from './LettersAbout';

const writer = userWithRoles('test-writer', 'Clinician');

describe('LettersAbout', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("lists a pathway's letters, each linking to its page, with Write Letter for a writer", async () => {
    const letter = testLetterSummary({approverUsername: 'd.clinician', status: 'AwaitingApproval'});
    const fetch = mockApi({'GET /api/letters/search': () => json(letterPage([letter], 25))});
    renderWithApp(<LettersAbout about={{pathwayId: LETTER_PATHWAY_ID}} teamId="s" patient={LETTER_PATIENT} />, {user: writer});

    const link = await screen.findByRole('link', {name: 'Clinic letter: Clinic follow-up'});
    expect(link).toHaveAttribute('href', `/letters/${letter.id}`);
    const item = within(screen.getByRole('list', {name: 'Letters'}));
    expect(item.getByText('Awaiting approval')).toBeInTheDocument();
    expect(item.getByText(/By k.ashford, approver d.clinician · Updated/)).toBeInTheDocument();
    expect(screen.getByText('Showing the latest 1 of 25.')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Write Letter'})).toBeInTheDocument();
    expect(new URL((fetch.mock.calls[0][0] as Request).url).search).toBe(`?PathwayId=${LETTER_PATHWAY_ID}&PageSize=20`);
  });

  it('says when there are none, and offers nothing to someone who can\'t write letters or while locked', async () => {
    mockApi({'GET /api/letters/search': () => json(letterPage([]))});
    const {unmount} = renderWithApp(<LettersAbout about={{referralId: 'r1'}} teamId="s" />, {user: staffUser});
    expect(await screen.findByText('No letters yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Write Letter'})).not.toBeInTheDocument();
    unmount();

    renderWithApp(<LettersAbout about={{referralId: 'r1'}} teamId="s" isLocked />, {user: writer});
    expect(await screen.findByText('No letters yet.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Write Letter'})).not.toBeInTheDocument();
  });

  it("says when the letters can't be loaded", async () => {
    mockApi({'GET /api/letters/search': () => json({title: 'Boom'}, 500)});
    renderWithApp(<LettersAbout about={{referralId: 'r1'}} teamId="s" />, {user: staffUser});
    expect(await screen.findByText('Letters not loaded')).toBeInTheDocument();
    expect(screen.queryByText('Loading letters...')).not.toBeInTheDocument();
  });

  it('writes a letter: the dialog, then the new draft opens', async () => {
    mockApi({
      'GET /api/letters/search': () => json(letterPage([])),
      'GET /api/letter-templates': () => json([{id: 't1', name: 'Skin follow-up', kind: 'ClinicLetter', teamId: null, isArchived: false}]),
      'POST /api/letters': () => json(testLetter(), 201),
    });
    const {user, router} = renderWithApp(<LettersAbout about={{pathwayId: LETTER_PATHWAY_ID}} teamId="s" patient={LETTER_PATIENT} />, {
      user: writer,
    });

    await user.click(await screen.findByRole('button', {name: 'Write Letter'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(openDialog()).toBeNull());

    await user.click(screen.getByRole('button', {name: 'Write Letter'}));
    const again = within(await waitFor(() => openDialog()!));
    await waitFor(() => expect(again.getByRole('combobox', {name: /Template/})).not.toHaveAttribute('aria-disabled', 'true'));
    await user.click(again.getByRole('combobox', {name: /Template/}));
    await user.click(await screen.findByRole('option', {name: /Skin follow-up/}));
    await user.click(again.getByRole('button', {name: 'Start Letter'}));

    await waitFor(() => expect(router.state.location.pathname).toBe(`/letters/${testLetter().id}`));
  });
});
