import {json, mockApi} from '../test/render';
import {DATA_REQUEST_ID} from '../test/dataRequests';
import {
  attachmentFileName,
  dataRequestStatusWords,
  dataRequestViewLabel,
  type DataRequestView,
  downloadDataRequestPack,
  identityCheckMethodWords,
  packProblem,
  reviewItemKindWords,
  sendMethodWords,
  viewFilters,
} from './dataRequests';
import {ApiError} from './errors';

describe('dataRequests', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('asks the API for each view: a status, the open ones awaiting review, or everything', () => {
    expect(viewFilters('Open')).toEqual({Status: 'Open'});
    expect(viewFilters('Refused')).toEqual({Status: 'Refused'});
    expect(viewFilters('AwaitingReview')).toEqual({AwaitingReview: true});
    expect(viewFilters('All')).toEqual({});
  });

  it('puts statuses, methods and kinds into words, keeping one it does not know', () => {
    expect(dataRequestStatusWords('Withdrawn')).toBe('Withdrawn');
    expect(identityCheckMethodWords('KnownToService')).toBe('Known to the service');
    expect(sendMethodWords('HandedOver')).toBe('Handed over');
    expect(reviewItemKindWords('MdtItem')).toBe('MDT item');
    expect(reviewItemKindWords('Something')).toBe('Something');
    expect(sendMethodWords(null)).toBe('');
    expect(sendMethodWords('Pigeon')).toBe('Pigeon');
    expect(dataRequestStatusWords('Lost')).toBe('Lost');
    expect(dataRequestStatusWords(undefined)).toBe('');
    expect(identityCheckMethodWords('Fingerprint')).toBe('Fingerprint');
    expect(identityCheckMethodWords(null)).toBe('');
    expect(reviewItemKindWords(null)).toBe('');
    expect(dataRequestViewLabel('AwaitingReview')).toBe('Awaiting review');
    expect(dataRequestViewLabel('Other' as DataRequestView)).toBe('Other');
  });

  it("reads a download's file name from its Content-Disposition", () => {
    expect(attachmentFileName('attachment; filename="pack.zip"; filename*=UTF-8\'\'data%20request.zip')).toBe('data request.zip');
    expect(attachmentFileName('attachment; filename=pack.zip')).toBe('pack.zip');
    expect(attachmentFileName('attachment')).toBeNull();
    expect(attachmentFileName(null)).toBeNull();
  });

  it('downloads the pack under the name the API gives it', async () => {
    const createObjectURL = vi.fn(() => 'blob:pack');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', Object.assign(URL, {createObjectURL, revokeObjectURL}));
    let name = '';
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      name = this.download;
    });
    mockApi({
      [`GET /api/data-requests/${DATA_REQUEST_ID}/pack`]: () =>
        new Response('PK', {headers: {'Content-Type': 'application/zip', 'Content-Disposition': 'attachment; filename="data-request-ahmed.zip"'}}),
    });

    await downloadDataRequestPack(DATA_REQUEST_ID);

    expect(click).toHaveBeenCalledTimes(1);
    expect(name).toBe('data-request-ahmed.zip');
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:pack');
    click.mockRestore();
  });

  it("says why the pack can't be made in the API's words, or apologises", async () => {
    mockApi({[`GET /api/data-requests/${DATA_REQUEST_ID}/pack`]: () => json({title: 'Review the record first.'}, 409)});
    const refused = await downloadDataRequestPack(DATA_REQUEST_ID).catch((e: unknown) => e);
    expect(refused).toBeInstanceOf(ApiError);
    expect(packProblem(refused)).toBe('Review the record first.');
    expect(packProblem(new ApiError(501, undefined, "The data-request pack isn't in the light demo."))).toBe(
      "The data-request pack isn't in the light demo.",
    );
    expect(packProblem(new ApiError(500, undefined, 'Boom'))).toBe('The pack could not be made. Please try again.');
  });
});
