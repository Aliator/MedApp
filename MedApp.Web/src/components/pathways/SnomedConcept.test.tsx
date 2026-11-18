import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {SnomedCell, SnomedConcept, snomedLabel, stageDescription} from './SnomedConcept';

// Real SNOMED CT concept IDs, used only as codes: 80146002 is Appendectomy, 71388002 is Procedure.
describe('snomedLabel', () => {
  it('writes the code and term as SNOMED CT does, or the code alone', () => {
    expect(snomedLabel({code: ' 80146002 ', term: ' Appendectomy '})).toBe('80146002 | Appendectomy');
    expect(snomedLabel({code: '71388002', term: null})).toBe('71388002');
  });

  it('gives nothing without a code', () => {
    expect(snomedLabel({code: '', term: 'Appendectomy'})).toBeNull();
    expect(snomedLabel({})).toBeNull();
  });
});

describe('SnomedConcept', () => {
  it('shows the code and term on a line', async () => {
    renderWithApp(<SnomedConcept code="80146002" term="Appendectomy" />);

    expect(await screen.findByText('SNOMED CT: 80146002 | Appendectomy')).toBeInTheDocument();
  });

  it('shows nothing without a code', async () => {
    renderWithApp(
      <>
        <p>Stage</p>
        <SnomedConcept code={null} term={null} />
      </>,
    );

    await screen.findByText('Stage');
    expect(screen.queryByText(/SNOMED CT/)).not.toBeInTheDocument();
  });
});

describe('SnomedCell', () => {
  it('shows the code with the term under it', async () => {
    renderWithApp(<SnomedCell code="71388002" term="Procedure" />);

    expect(await screen.findByText('71388002')).toBeInTheDocument();
    expect(screen.getByText('Procedure')).toBeInTheDocument();
  });

  it('shows the code alone without a term, and a dash without a code', async () => {
    renderWithApp(
      <>
        <SnomedCell code="71388002" />
        <SnomedCell code="" term="Procedure" />
      </>,
    );

    expect(await screen.findByText('71388002')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
    expect(screen.queryByText('Procedure')).not.toBeInTheDocument();
  });
});

describe('stageDescription', () => {
  it('is the description as it is without a concept', () => {
    expect(stageDescription('Consent recorded.', {})).toBe('Consent recorded.');
    expect(stageDescription(null, {code: ''})).toBeUndefined();
  });

  it('adds the concept on a line under the description, or alone', async () => {
    renderWithApp(
      <>
        {stageDescription('Appendix removed.', {code: '80146002', term: 'Appendectomy'})}
        {stageDescription('', {code: '71388002', term: 'Procedure'})}
      </>,
    );

    expect(await screen.findByText('Appendix removed.')).toBeInTheDocument();
    expect(screen.getByText('SNOMED CT: 80146002 | Appendectomy')).toBeInTheDocument();
    expect(screen.getByText('SNOMED CT: 71388002 | Procedure')).toBeInTheDocument();
  });
});
