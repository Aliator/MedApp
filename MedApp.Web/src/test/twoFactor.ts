import {onTestFinished} from 'vitest';

/** Synthetic two-factor set-up data for tests: an authenticator key and recovery codes. */

/** A new authenticator key, as `POST api/auth/me/two-factor/authenticator` returns it (a tiny stand-in QR code). */
export const authenticatorSetup = {
  sharedKey: 'abcd efgh ijkl mnop',
  authenticatorUri: 'otpauth://totp/MedApp:test-admin?secret=ABCDEFGHIJKLMNOP&issuer=MedApp',
  qrCodePath: 'M4,4h1v1h-1zM6,4h1v1h-1z',
  qrCodeSize: 29,
};

/** Ten synthetic recovery codes. */
export const recoveryCodes = Array.from({length: 10}, (_, i) => `syn${i}-code${i}`);

/**
 * Stands in for the browser's object URLs (jsdom has none) and link clicks (jsdom can't download), recording what was
 * downloaded, until the test ends.
 */
export function stubDownloads() {
  const blobs: Blob[] = [];
  const clicks: {href: string; download: string}[] = [];
  const original = {create: URL.createObjectURL, revoke: URL.revokeObjectURL};
  const revoke = vi.fn();
  URL.createObjectURL = (blob: Blob) => {
    blobs.push(blob);
    return `blob:synthetic/${blobs.length}`;
  };
  URL.revokeObjectURL = revoke;
  onTestFinished(() => {
    URL.createObjectURL = original.create;
    URL.revokeObjectURL = original.revoke;
  });
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    clicks.push({href: this.href, download: this.download});
  });
  return {blobs, clicks, revoke};
}
