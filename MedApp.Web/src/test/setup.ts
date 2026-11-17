import '@testing-library/jest-dom/vitest';
import {cleanup, configure} from '@testing-library/react';
import {afterEach, vi} from 'vitest';
import {appSettings} from '../app/settingsStore';
import {FakeBroadcastChannel} from './broadcastChannel';
import {liveHub} from './signalr';

// No live connections to a server in tests: a stand-in that tests can push messages through (`liveHub`).
vi.mock('@microsoft/signalr', () => import('./signalr'));

// Routes are code-split, and loading a route chunk can take over a second while the whole suite runs.
configure({asyncUtilTimeout: 5000});

// jsdom doesn't implement scrolling; the router restores scroll position on navigation.
// (Tests of server rendering run in Node, with no window: `@vitest-environment node`.)
if (typeof window !== 'undefined') window.scrollTo = () => {};

// jsdom has no layout, so ranges have no boxes; the rich text editor (Lexical) measures the selection's.
if (typeof Range !== 'undefined' && !Range.prototype.getBoundingClientRect) {
  Range.prototype.getBoundingClientRect = () => new DOMRect();
  Range.prototype.getClientRects = () => [] as unknown as DOMRectList;
}

// The notepad keeps MedApp's windows in step on a BroadcastChannel: a stand-in in this test's memory, as Node's own
// reaches other test workers (`FakeBroadcastChannel`). Not stubbed with vi.stubGlobal, which each test undoes.
if (typeof window !== 'undefined') globalThis.BroadcastChannel = FakeBroadcastChannel as unknown as typeof BroadcastChannel;

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
  if (typeof window !== 'undefined') localStorage.clear();
  appSettings.reset();
  liveHub.reset();
  FakeBroadcastChannel.reset();
});
