import {screen, within} from '@testing-library/react';
import {calls, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {SPECIMEN_ID, testSpecimen} from '../../test/specimens';
import {labelName, SpecimenPrintPage} from './SpecimenPrintPage';

function renderPage(sheet: 'label' | 'request', answer: () => Response = () => json(testSpecimen())) {
  const fetch = mockApi({[`GET /api/specimens/${SPECIMEN_ID}`]: answer});
  return {...renderWithApp(<SpecimenPrintPage id={SPECIMEN_ID} sheet={sheet} />, {user: staffUser}), fetch};
}

describe('SpecimenPrintPage', () => {
  it('prints a label: the number as text and QR code, and who and what it is, reading the specimen once', async () => {
    const print = vi.spyOn(window, 'print').mockImplementation(() => undefined);
    const {user, fetch} = renderPage('label');

    expect(await screen.findByRole('heading', {name: 'Specimen Label', level: 1})).toBeInTheDocument();
    const label = within(await screen.findByLabelText('Label for specimen MA26-000123'));
    expect(label.getByText('MA26-000123')).toBeInTheDocument();
    expect(label.getByRole('img', {name: 'QR code of specimen number MA26-000123'})).toBeInTheDocument();
    expect(label.getByText('AHMED, Freya')).toBeInTheDocument();
    expect(label.getByText('Born 1 December 1976')).toBeInTheDocument();
    expect(label.getByText('NHS No. 999 000 0026')).toBeInTheDocument();
    expect(label.getByText('Skin biopsy (punch)')).toBeInTheDocument();
    expect(label.getByText('Taken 2 Nov 2026, 09:00')).toBeInTheDocument();
    // The request's details aren't on a label.
    expect(label.queryByText('Left forearm')).not.toBeInTheDocument();
    expect(screen.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(document.querySelector('style[data-medapp-print]')?.textContent).toContain('@media print');

    await user.click(screen.getByRole('button', {name: 'Print'}));
    expect(print).toHaveBeenCalled();
    expect(calls(fetch)).toEqual([`GET /api/specimens/${SPECIMEN_ID}`]);
  });

  it('prints a request sheet, adding the site, the notes, the requester and the lab', async () => {
    renderPage('request');

    expect(await screen.findByRole('heading', {name: 'Request Sheet', level: 1})).toBeInTheDocument();
    const sheet = within(await screen.findByLabelText('Request sheet for specimen MA26-000123'));
    expect(sheet.getByRole('heading', {name: 'Request for Northgate pathology'})).toBeInTheDocument();
    expect(sheet.getByText('MA26-000123')).toBeInTheDocument();
    expect(sheet.getByText('Left forearm')).toBeInTheDocument();
    expect(sheet.getByText('Synthetic notes for the lab.')).toBeInTheDocument();
    expect(sheet.getByText('dr.synthetic')).toBeInTheDocument();
    expect(sheet.getByText('Northgate pathology')).toBeInTheDocument();
  });

  it('says when the specimen is not found', async () => {
    renderPage('label', () => json({title: 'Not found'}, 404));
    expect(await screen.findByText('Specimen not found. It may have been deleted.')).toBeInTheDocument();
  });

  it("prints the patient's name family name first, in capitals", () => {
    expect(labelName({firstName: 'Freya', lastName: 'Ahmed'})).toBe('AHMED, Freya');
    expect(labelName({firstName: 'Freya', lastName: ' '})).toBe('Freya');
    expect(labelName({lastName: 'Ahmed'})).toBe('AHMED');
    expect(labelName(undefined)).toBe('Unknown patient');
  });
});
