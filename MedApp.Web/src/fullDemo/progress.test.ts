import {FULL_DEMO_VERSION} from '../test/fullDemo';
import {megabytes, progressWords} from './progress';
import type {StartupState} from './startup';

const MB = 1024 * 1024;
const downloading = (progress: StartupState['progress'], size: StartupState['size'] = {bytes: 40 * MB, compressedBytes: 16 * MB}): StartupState => ({
  phase: 'downloading',
  progress,
  version: FULL_DEMO_VERSION,
  size,
});

describe('progressWords', () => {
  it('says how many megabytes of the runtime and the database have come', () => {
    const words = progressWords(
      downloading({backend: {loaded: 100, total: 242, bytes: 20 * MB, cached: 0}, database: {loaded: 2 * MB, total: 4 * MB}}),
    );
    expect(words).toEqual({label: 'Getting MedApp ready: 10 of 20 MB', value: 50});
  });

  it('says a percentage when the runtime’s size isn’t known', () => {
    expect(progressWords(downloading({backend: {loaded: 121, total: 242}, database: {loaded: 0, total: 0}}, null))).toEqual({
      label: 'Getting MedApp ready: 50%',
      value: 50,
    });
  });

  it('says it’s coming from this browser when the files were kept here', () => {
    expect(progressWords(downloading({backend: {loaded: 10, total: 242, bytes: 4 * MB, cached: 10}, database: {loaded: 0, total: 0}})).label).toBe(
      'Getting MedApp ready from this browser',
    );
  });

  it('is indeterminate before anything has come, while starting, and says when it’s ready or failed', () => {
    expect(progressWords(downloading(null))).toEqual({label: 'Getting MedApp ready…', value: null});
    expect(progressWords({...downloading(null), phase: 'starting'})).toEqual({label: 'Starting…', value: null});
    expect(progressWords({...downloading(null), phase: 'failed', error: 'x'}).value).toBeNull();
  });

  it('writes megabytes to a decimal place under 10', () => {
    expect(megabytes(3.44 * MB)).toBe('3.4');
    expect(megabytes(15.6 * MB)).toBe('16');
  });
});
