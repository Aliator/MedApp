import '@testing-library/jest-dom/vitest';
import {cleanup, configure} from '@testing-library/react';
import {afterEach, vi} from 'vitest';
import {appSettings} from '../app/settingsStore';
import {liveHub} from './signalr';

// No live connections to a server in tests: a stand-in that tests can push messages through (`liveHub`).
vi.mock('@microsoft/signalr', () => import('./signalr'));

// Routes are code-split, and loading a route chunk can take over a second while the whole suite runs.
configure({asyncUtilTimeout: 5000});

// jsdom doesn't implement scrolling; the router restores scroll position on navigation.
window.scrollTo = () => {};

// jsdom has no layout, so ranges have no boxes; the rich text editor (Lexical) measures the selection's.
if (typeof Range !== 'undefined' && !Range.prototype.getBoundingClientRect) {
  Range.prototype.getBoundingClientRect = () => new DOMRect();
  Range.prototype.getClientRects = () => [] as unknown as DOMRectList;
}

// jsdom has <dialog> but not its methods, which Astryx's Dialog calls. Opening sets `open`; closing clears it
// and fires `close`, as browsers do.
if (typeof HTMLDialogElement !== 'undefined' && !HTMLDialogElement.prototype.showModal) {
  HTMLDialogElement.prototype.show = function show(this: HTMLDialogElement) {
    this.setAttribute('open', '');
  };
  HTMLDialogElement.prototype.showModal = HTMLDialogElement.prototype.show;
  HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement, returnValue?: string) {
    if (returnValue !== undefined) this.returnValue = returnValue;
    this.removeAttribute('open');
    this.dispatchEvent(new Event('close'));
  };
}

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  localStorage.clear();
  appSettings.reset();
  liveHub.reset();
});
