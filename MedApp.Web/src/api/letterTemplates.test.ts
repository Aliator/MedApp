import {json, mockApi} from '../test/render';
import {ApiError, ApiValidationError} from './errors';
import {
  compareLetterTemplates,
  downloadLetterTemplatePreview,
  EMPTY_LETTER_TEMPLATE,
  LETTER_KIND_LABELS,
  letterKindLabel,
  letterTemplateInput,
  letterTemplateSpecialty,
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
      'Other',
    ]);
    expect(letterKindLabel('InformationRequest')).toBe('Request for more information');
    expect(letterKindLabel('Prescription')).toBe('Prescription');
    expect(letterKindLabel(null)).toBe('');
  });

  it("names a template's specialty, or every specialty", () => {
    expect(letterTemplateSpecialty({specialtyId: 'sp1', specialtyName: 'Dermatology'})).toBe('Dermatology');
    expect(letterTemplateSpecialty({specialtyId: 'sp1'})).toBe('Unknown specialty');
    expect(letterTemplateSpecialty({specialtyId: null})).toBe('All specialties');
  });

  it("sorts as the API does: every specialty's first, then by specialty, then by name", () => {
    const templates: LetterTemplate[] = [
      {name: 'b', specialtyId: 'u', specialtyName: 'Urology'},
      {name: 'a', specialtyId: 'u', specialtyName: 'Urology'},
      {name: 'z', specialtyId: 'd', specialtyName: 'dermatology'},
      {name: 'y'},
      {specialtyId: 'x'},
    ];
    expect([...templates].sort(compareLetterTemplates).map((t) => `${t.specialtyName ?? '-'}/${t.name ?? '-'}`)).toEqual([
      '-/y',
      '-/-',
      'dermatology/z',
      'Urology/a',
      'Urology/b',
    ]);
  });

  it('starts the form from a template, or empty for a new one', () => {
    expect(letterTemplateInput(null)).toBe(EMPTY_LETTER_TEMPLATE);
    expect(letterTemplateInput({name: 'Letter', kind: 'Other', specialtyId: 'sp1', letterhead: 'Head', bodySkeleton: '## Plan', closing: 'Bye'})).toEqual({
      name: 'Letter',
      kind: 'Other',
      specialtyId: 'sp1',
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
