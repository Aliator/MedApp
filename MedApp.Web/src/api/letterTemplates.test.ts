import {json, mockApi} from '../test/render';
import {ApiError, ApiValidationError} from './errors';
import {
  compareLetterTemplates,
  downloadLetterTemplatePreview,
  EMPTY_LETTER_TEMPLATE,
  LETTER_KIND_LABELS,
  letterKindLabel,
  letterTemplateInput,
  letterTemplateTeam,
  type LetterTemplate,
} from './letterTemplates';

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe('letter templates', () => {
  it('words each kind, and a kind this copy of the app does not know as the API named it', () => {
    expect(Object.values(LETTER_KIND_LABELS)).toEqual([
      'Clinic letter',
      'Discharge summary',
      'Referral response',
      'Request for more information',
      'Appointment letter',
      'Onward referral to hospital',
      'Other',
    ]);
    expect(letterKindLabel('InformationRequest')).toBe('Request for more information');
    expect(letterKindLabel('Prescription')).toBe('Prescription');
    expect(letterKindLabel(null)).toBe('');
  });

  it("names a template's team, or every team", () => {
    expect(letterTemplateTeam({teamId: 'sp1', teamName: 'Dermatology'})).toBe('Dermatology');
    expect(letterTemplateTeam({teamId: 'sp1'})).toBe('Unknown team');
    expect(letterTemplateTeam({teamId: null})).toBe('All teams');
  });

  it("sorts as the API does: every team's first, then by team, then by name", () => {
    const templates: LetterTemplate[] = [
      {name: 'b', teamId: 'u', teamName: 'Urology'},
      {name: 'a', teamId: 'u', teamName: 'Urology'},
      {name: 'z', teamId: 'd', teamName: 'dermatology'},
      {name: 'y'},
      {teamId: 'x'},
    ];
    expect([...templates].sort(compareLetterTemplates).map((t) => `${t.teamName ?? '-'}/${t.name ?? '-'}`)).toEqual([
      '-/y',
      '-/-',
      'dermatology/z',
      'Urology/a',
      'Urology/b',
    ]);
  });

  it('starts the form from a template, or empty for a new one', () => {
    expect(letterTemplateInput(null)).toBe(EMPTY_LETTER_TEMPLATE);
    expect(letterTemplateInput({name: 'Letter', kind: 'Other', teamId: 'sp1', letterhead: 'Head', bodySkeleton: '## Plan', closing: 'Bye'})).toEqual({
      name: 'Letter',
      kind: 'Other',
      teamId: 'sp1',
      letterhead: 'Head',
      bodySkeleton: '## Plan',
      closing: 'Bye',
    });
    expect(letterTemplateInput({})).toEqual(EMPTY_LETTER_TEMPLATE);
  });

  describe('downloadLetterTemplatePreview', () => {
    const input = {name: '  ', letterhead: 'Head', bodySkeleton: 'Body', closing: 'Bye', largePrint: false};

    it('posts the text, with no name when blank, and saves the PDF', async () => {
      const createObjectURL = vi.fn(() => 'blob:preview');
      const revokeObjectURL = vi.fn();
      vi.stubGlobal('URL', Object.assign(URL, {createObjectURL, revokeObjectURL}));
      const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});
      const fetch = mockApi({'POST /api/letter-templates/preview': () => new Response('%PDF', {headers: {'Content-Type': 'application/pdf'}})});

      await downloadLetterTemplatePreview(input);

      expect(await (fetch.mock.calls[0][0] as Request).json()).toEqual({...input, name: null});
      expect(click).toHaveBeenCalledTimes(1);
      expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview');
    });

    it('throws a 400 as field errors, and anything else as an ApiError', async () => {
      mockApi({'POST /api/letter-templates/preview': () => json({title: 'Invalid', errors: {BodySkeleton: ['No.']}}, 400)});
      await expect(downloadLetterTemplatePreview(input)).rejects.toBeInstanceOf(ApiValidationError);

      mockApi({'POST /api/letter-templates/preview': () => json({title: 'Server error'}, 500)});
      await expect(downloadLetterTemplatePreview(input)).rejects.toBeInstanceOf(ApiError);
    });
  });
});
