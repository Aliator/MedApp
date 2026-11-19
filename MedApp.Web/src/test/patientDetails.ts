import {screen} from '@testing-library/react';
import {expect} from 'vitest';

/**
 * Checks a view page shows its patient the way the pathway page does (hazards H-01 and H-02): no `PatientBanner`, and
 * the patient's name (linking to their page) at the top of Details, the first section heading, which never folds.
 */
export function expectPatientFirstInDetails(name: RegExp = /Ahmed, Freya/) {
  expect(screen.queryByRole('region', {name: /^Patient:/})).not.toBeInTheDocument();
  expect(screen.getAllByRole('heading', {level: 2})[0]).toHaveTextContent(/^Details$/);
  expect(screen.queryByRole('button', {name: 'Hide details'})).not.toBeInTheDocument();
  expect(screen.getAllByRole('link', {name}).length).toBeGreaterThan(0);
}
