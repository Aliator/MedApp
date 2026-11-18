import {screen, waitFor, within} from '@testing-library/react';
import {HalfViewContext} from '../../components/pages/halfView';
import {adminUser, expectAnnounced, findAlert, json, mockApi, renderWithApp, withAnnouncements} from '../../test/render';
import {LetterTemplateEditorPage, letterSpecialtyChoices, previewProblem} from './LetterTemplateEditorPage';
import {ApiError, ApiValidationError} from '../../api/errors';

const BASE = '/api/letter-templates';

const template = {
  id: 'l1',
  name: 'Skin clinic letter',
  kind: 'ClinicLetter',
  specialtyId: 'sp1',
  specialtyName: 'Dermatology',
  letterhead: 'Example Skin Clinic',
  bodySkeleton: '## Diagnosis\n\n## Plan',
  closing: 'Yours sincerely,',
  mergeFields: [],
  isArchived: false,
  version: 3,
};

/** The active specialties (the editor asks for those only). */
const specialties = [
  {id: 'sp2', name: 'Urology', isArchived: false},
  {id: 'sp1', name: 'Dermatology', isArchived: false},
];

const mergeFields = [
  {name: 'patient.name', label: "Patient's name", description: "The patient's full name, as on their record.", example: 'Sam Example'},
  {name: 'today', label: "Today's date", description: 'The date the letter is rendered.', example: '18 November 2026'},
];

function renderEditor(id?: string, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    [`GET ${BASE}/l1`]: () => json(template),
    [`GET ${BASE}/merge-fields`]: () => json(mergeFields),
    'GET /api/specialties': () => json(specialties),
    ...extra,
  });
  const path = id ? `/admin/letter-templates/${id}` : '/admin/letter-templates/new';
  return {fetch, ...renderWithApp(<LetterTemplateEditorPage id={id} />, {user: adminUser, path})};
}

const sent = (fetch: ReturnType<typeof mockApi>, method: string, path?: string) =>
  fetch.mock.calls.map(([r]) => r as Request).filter((r) => r.method === method && (!path || new URL(r.url).pathname === path));

async function choose(user: ReturnType<typeof renderEditor>['user'], field: RegExp, option: string) {
  await user.click(screen.getByRole('combobox', {name: field}));
  await user.click(screen.getByRole('option', {name: option}));
}

/** Puts text into a field (pasted, as braces are keys to `user.type`). */
async function fill(user: ReturnType<typeof renderEditor>['user'], name: RegExp, text: string) {
  const field = screen.getByRole('textbox', {name});
  await user.clear(field);
  await user.click(field);
  await user.paste(text);
}

/** Stands in for the browser's object URLs, keeping what was saved. */
function stubObjectUrls() {
  const createObjectURL = vi.fn<(blob: Blob) => string>(() => 'blob:preview');
  const revokeObjectURL = vi.fn();
  vi.stubGlobal('URL', Object.assign(URL, {createObjectURL, revokeObjectURL}));
  const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
  return {createObjectURL, revokeObjectURL, click};
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('letterSpecialtyChoices', () => {
  it('offers every specialty first, then the active ones by name, and keeps an archived one the template has', () => {
    expect(letterSpecialtyChoices([...specialties, {id: 'sp9', name: 'Old', isArchived: true}], null).map((c) => c.label)).toEqual([
      'All specialties',
      'Dermatology',
      'Urology',
    ]);
    expect(letterSpecialtyChoices(specialties, {specialtyId: 'sp3', specialtyName: 'Old clinic'}).map((c) => c.label)).toEqual([
      'All specialties',
      'Old clinic (archived)',
      'Dermatology',
      'Urology',
    ]);
    expect(letterSpecialtyChoices([{}], {specialtyId: 'sp3'}).map((c) => c.label)).toEqual(['All specialties', 'Unknown specialty (archived)', '']);
  });
});

describe('previewProblem', () => {
  it("shows a 400 on the fields, a missing permission and an ended session in words, and anything else as a failure", () => {
    const errors = {title: 'Invalid', fields: {letterhead: ['No.']}, general: []};
    expect(previewProblem(new ApiValidationError(errors))).toEqual({validation: errors, message: null});
    expect(previewProblem(new ApiError(403, undefined, 'Your roles do not allow this.', 'permission_missing')).message).toBe('Your roles do not allow this.');
    expect(previewProblem(new ApiError(401)).message).toBe('Your session has ended. Please sign in again.');
    expect(previewProblem(new ApiError(500)).message).toBe('The preview could not be made. Please try again.');
    expect(previewProblem(new TypeError('offline')).message).toBe('The preview could not be made. Please try again.');
  });
});

describe('LetterTemplateEditorPage', () => {
  it('creates a template, then opens it', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor(undefined, {[`POST ${BASE}`]: () => json({...template, id: 'l9'}, 201)});

    await user.type(await screen.findByRole('textbox', {name: /Name/}), ' Discharge letter ');
    await choose(user, /Kind/, 'Discharge summary');
    await user.click(screen.getByRole('combobox', {name: /Specialty/}));
    expect(screen.getAllByRole('option').map((o) => o.textContent)).toEqual(['All specialties', 'Dermatology', 'Urology']);
    await user.click(screen.getByRole('option', {name: 'Urology'}));
    await fill(user, /Letterhead/, 'Example Clinic');
    await fill(user, /Body skeleton/, 'Dear {{patient.name}},\n\n## Summary');
    await fill(user, /Closing/, '{{author.name}}');
    await user.click(screen.getByRole('button', {name: 'Create Letter Template'}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/letter-templates/l9'));
    expect(await sent(fetch, 'POST')[0].json()).toEqual({
      name: 'Discharge letter',
      kind: 'DischargeSummary',
      specialtyId: 'sp2',
      letterhead: 'Example Clinic',
      bodySkeleton: 'Dear {{patient.name}},\n\n## Summary',
      closing: '{{author.name}}',
    });
    await expectAnnounced('Letter template created');
  });

  it('starts a new one as a clinic letter for every specialty', async () => {
    const {user, fetch} = renderEditor(undefined, {[`POST ${BASE}`]: () => json({...template, id: 'l9'}, 201)});

    expect(await screen.findByRole('combobox', {name: /Kind/})).toHaveTextContent('Clinic letter');
    expect(screen.getByRole('combobox', {name: /Specialty/})).toHaveTextContent('All specialties');
    await user.type(screen.getByRole('textbox', {name: /Name/}), 'General');
    await user.click(screen.getByRole('button', {name: 'Create Letter Template'}));

    await waitFor(() => expect(sent(fetch, 'POST')).toHaveLength(1));
    expect(await sent(fetch, 'POST')[0].json()).toMatchObject({kind: 'ClinicLetter', specialtyId: null});
  });

  it('saves a change from the version shown, staying on the page', async () => {
    withAnnouncements();
    const {user, fetch, router} = renderEditor('l1', {[`PUT ${BASE}/l1`]: () => json({...template, name: 'Skin letter', version: 4})});

    expect(await screen.findByRole('textbox', {name: /Name/})).toHaveValue('Skin clinic letter');
    expect(screen.getByRole('textbox', {name: /Body skeleton/})).toHaveValue('## Diagnosis\n\n## Plan');
    expect(screen.getByRole('combobox', {name: /Specialty/})).toHaveTextContent('Dermatology');
    await fill(user, /Name/, 'Skin letter');
    await choose(user, /Specialty/, 'All specialties');
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await expectAnnounced('Letter template saved');
    expect(await sent(fetch, 'PUT')[0].json()).toEqual({
      name: 'Skin letter',
      kind: 'ClinicLetter',
      specialtyId: null,
      letterhead: 'Example Skin Clinic',
      bodySkeleton: '## Diagnosis\n\n## Plan',
      closing: 'Yours sincerely,',
      expectedVersion: 3,
    });
    expect(router.state.location.pathname).toBe('/admin/letter-templates/l1');
  });

  it("shows the API's 400 on the field holding an unknown merge field, and the rest in the header", async () => {
    const {user} = renderEditor(undefined, {
      [`POST ${BASE}`]: () =>
        json(
          {
            title: 'Validation failed',
            status: 400,
            errors: {
              BodySkeleton: ['{{patient.nme}} isn\'t a merge field. Choose one from the list, or remove the double braces.'],
              ExpectedVersion: ['Something else went wrong.'],
            },
          },
          400,
        ),
    });

    await user.type(await screen.findByRole('textbox', {name: /Name/}), 'Letter');
    await fill(user, /Body skeleton/, 'Dear {{patient.nme}}');
    await user.click(screen.getByRole('button', {name: 'Create Letter Template'}));

    await waitFor(() => expect(screen.getByRole('textbox', {name: /Body skeleton/})).toHaveAccessibleDescription(/\{\{patient\.nme\}\} isn't a merge field/));
    expect(await findAlert('Something else went wrong.')).not.toHaveTextContent('patient.nme');
  });

  it("shows the API's words for a 409, reads the template again, and saves from its new version", async () => {
    let version = 3;
    let puts = 0;
    const {user, fetch} = renderEditor('l1', {
      [`GET ${BASE}/l1`]: () => json({...template, version}),
      [`PUT ${BASE}/l1`]: () => {
        puts += 1;
        if (puts === 1) {
          version = 4;
          return json({title: 'Someone else changed this letter template. Check it, then try again.', status: 409}, 409);
        }
        return json({...template, version: 5});
      },
    });

    await screen.findByRole('textbox', {name: /Name/});
    await fill(user, /Name/, 'Changed');
    await user.click(screen.getByRole('button', {name: 'Save'}));
    expect(await findAlert('Someone else changed this letter template. Check it, then try again.')).toBeInTheDocument();
    // What was typed stays.
    expect(screen.getByRole('textbox', {name: /Name/})).toHaveValue('Changed');

    await waitFor(() => expect(sent(fetch, 'GET', `${BASE}/l1`).length).toBeGreaterThan(1));
    await user.click(screen.getByRole('button', {name: 'Save'}));
    await waitFor(() => expect(sent(fetch, 'PUT')).toHaveLength(2));
    const bodies = await Promise.all(sent(fetch, 'PUT').map((r) => r.json()));
    expect(bodies.map((b: {expectedVersion: number}) => b.expectedVersion)).toEqual([3, 4]);
  });

  it('keeps an archived specialty the template has, marked, and says when the template is archived', async () => {
    const {user, fetch} = renderEditor('l1', {
      [`GET ${BASE}/l1`]: () => json({...template, specialtyId: 'sp3', specialtyName: 'Old clinic', isArchived: true}),
      [`PUT ${BASE}/l1`]: () => json(template),
    });

    expect(await screen.findByRole('combobox', {name: /Specialty/})).toHaveTextContent('Old clinic (archived)');
    expect(await findAlert(/This letter template is archived/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Save'}));

    await waitFor(() => expect(sent(fetch, 'PUT')).toHaveLength(1));
    expect(await sent(fetch, 'PUT')[0].json()).toMatchObject({specialtyId: 'sp3'});
  });

  it('lists the merge fields, as they are written, with what they copy', async () => {
    renderEditor('l1');

    const list = within(await screen.findByRole('list', {name: 'Merge fields'}));
    expect(list.getByText('{{patient.name}}')).toBeInTheDocument();
    expect(list.getByText("Patient's name")).toBeInTheDocument();
    expect(list.getByText("The patient's full name, as on their record.")).toBeInTheDocument();
    expect(list.getByText('{{today}}')).toBeInTheDocument();
    expect(screen.getByText(/identity and administrative details only/)).toBeInTheDocument();
    expect(screen.getByText(/merge field that has no value can't be approved/)).toBeInTheDocument();
  });

  it("says when the merge fields couldn't be loaded, and shows them loading", async () => {
    let answer: (response: Response) => void = () => {};
    const {unmount} = renderEditor('l1', {[`GET ${BASE}/merge-fields`]: () => new Promise<Response>((resolve) => (answer = resolve))});
    expect(await screen.findByText('Loading merge fields...')).toBeInTheDocument();
    answer(json({title: 'Server error'}, 500));
    unmount();

    renderEditor('l1', {[`GET ${BASE}/merge-fields`]: () => json({title: 'Server error'}, 500)});
    expect(await findAlert('The merge fields could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('downloads a preview of the text as it is, unsaved, in large print when asked', async () => {
    withAnnouncements();
    const {createObjectURL, revokeObjectURL, click} = stubObjectUrls();
    const {user, fetch} = renderEditor('l1', {
      [`POST ${BASE}/preview`]: () => new Response('%PDF-1.7', {status: 200, headers: {'Content-Type': 'application/pdf'}}),
    });

    await screen.findByRole('textbox', {name: /Letterhead/});
    await fill(user, /Letterhead/, 'New letterhead {{site.name}}');
    expect(screen.getByText(/filled with made-up details, never a real patient's/)).toBeInTheDocument();
    await user.click(screen.getByRole('checkbox', {name: /Large print/}));
    await user.click(screen.getByRole('button', {name: 'Download Preview (PDF)'}));

    await expectAnnounced('Preview downloaded');
    expect(await sent(fetch, 'POST', `${BASE}/preview`)[0].json()).toEqual({
      name: 'Skin clinic letter',
      letterhead: 'New letterhead {{site.name}}',
      bodySkeleton: '## Diagnosis\n\n## Plan',
      closing: 'Yours sincerely,',
      largePrint: true,
    });
    expect(await (createObjectURL.mock.calls[0][0] as Blob).text()).toBe('%PDF-1.7');
    expect(click).toHaveBeenCalledTimes(1);
    expect((click.mock.contexts[0] as HTMLAnchorElement).download).toBe('letter-template-preview.pdf');
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview');
    expect(sent(fetch, 'PUT')).toEqual([]);
  });

  it("shows a preview's 400 on the field, and its other failures in words", async () => {
    const {createObjectURL} = stubObjectUrls();
    let previews = 0;
    const {user} = renderEditor(undefined, {
      [`POST ${BASE}/preview`]: () => {
        previews += 1;
        return previews === 1
          ? json({title: 'Validation failed', status: 400, errors: {Letterhead: ["{{site.nme}} isn't a merge field."]}}, 400)
          : json({title: 'Server error'}, 500);
      },
    });

    await screen.findByRole('textbox', {name: /Letterhead/});
    await fill(user, /Letterhead/, '{{site.nme}}');
    await user.click(screen.getByRole('button', {name: 'Download Preview (PDF)'}));
    await waitFor(() => expect(screen.getByRole('textbox', {name: /Letterhead/})).toHaveAccessibleDescription(/\{\{site\.nme\}\} isn't a merge field/));

    await user.click(screen.getByRole('button', {name: 'Download Preview (PDF)'}));
    expect(await findAlert('The preview could not be made. Please try again.')).toBeInTheDocument();
    expect(createObjectURL).not.toHaveBeenCalled();
  });

  it('goes back to the list from Cancel', async () => {
    const {user, router} = renderEditor('l1');
    await user.click(await screen.findByRole('button', {name: 'Cancel'}));
    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/letter-templates'));
  });

  it('goes back to the list for an unknown template', async () => {
    const {router} = renderEditor('l404', {[`GET ${BASE}/l404`]: () => json({title: 'Not found'}, 404)});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/letter-templates'));
  });

  it('says when it could not be loaded', async () => {
    renderEditor('l1', {[`GET ${BASE}/l1`]: () => json({title: 'Server error'}, 500)});

    expect(await findAlert('The letter template could not be loaded. Please try again.')).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'Edit Letter Template'})).toBeInTheDocument();
  });

  it('puts the merge fields in the form in a half view', async () => {
    mockApi({[`GET ${BASE}/merge-fields`]: () => json(mergeFields), 'GET /api/specialties': () => json(specialties)});
    renderWithApp(
      <HalfViewContext.Provider value={{isHalf: true}}>
        <LetterTemplateEditorPage />
      </HalfViewContext.Provider>,
      {user: adminUser, path: '/admin/letter-templates/new'},
    );

    expect(await screen.findByRole('list', {name: 'Merge fields'})).toBeInTheDocument();
    expect(screen.getByRole('heading', {name: 'New Letter Template'})).toBeInTheDocument();
    expect(screen.getAllByRole('heading', {name: 'Merge Fields'})).toHaveLength(1);
  });
});
