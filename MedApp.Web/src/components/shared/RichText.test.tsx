import {screen} from '@testing-library/react';
import {renderWithApp} from '../../test/render';
import {RichText} from './RichText';

describe('RichText', () => {
  it('shows Markdown formatted: bold, italics, lists and links', async () => {
    renderWithApp(<RichText>{'Seen **today**, _calm_.\n\n- Bloods taken\n- Leaflet given\n\n[Guidance](https://example.org/guide)'}</RichText>);

    expect((await screen.findByText('today')).closest('strong')).not.toBeNull();
    expect(screen.getByText('calm').closest('em')).not.toBeNull();
    expect(screen.getAllByRole('listitem').map((item) => item.textContent)).toEqual(['Bloods taken', 'Leaflet given']);
    expect(screen.getByRole('link', {name: 'Guidance'})).toHaveAttribute('href', 'https://example.org/guide');
  });

  it('keeps the lines of plain text written before rich text', async () => {
    renderWithApp(<RichText>{'Line one\nLine two'}</RichText>);

    const text = await screen.findByText(/Line one/);
    expect(text.querySelector('br')).not.toBeNull();
  });

  it('never runs HTML or unsafe links', async () => {
    renderWithApp(<RichText>{'<img src=x onerror="alert(1)"> [click](javascript:alert(1))'}</RichText>);

    await screen.findByText(/click/);
    expect(document.querySelector('img')).toBeNull();
    expect(screen.queryByRole('link', {name: 'click'})).not.toBeInTheDocument();
  });
});
