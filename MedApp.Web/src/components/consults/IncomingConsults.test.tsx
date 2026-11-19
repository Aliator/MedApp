import {screen} from '@testing-library/react';
import {testConsult} from '../../test/consults';
import {json, mockApi, renderWithApp} from '../../test/render';
import {IncomingConsults} from './IncomingConsults';

describe('IncomingConsults', () => {
  it("lists the team's open consults with their count, each linking its pathway", async () => {
    mockApi({'GET /api/consults/incoming': () => json([testConsult({canReply: true})])});
    renderWithApp(<IncomingConsults title="Consults for the lab" />);

    expect(await screen.findByRole('link', {name: 'Skin lesion'})).toHaveAttribute('href', '/pathways/proc-1');
    expect(screen.getByRole('heading', {name: 'Consults for the lab'})).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByRole('button', {name: 'Reply'})).toBeInTheDocument();
  });

  it('says when there are none', async () => {
    mockApi({'GET /api/consults/incoming': () => json([])});
    renderWithApp(<IncomingConsults title="Consults for the lab" />);
    expect(await screen.findByText(/^No open consults/)).toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    mockApi({'GET /api/consults/incoming': () => json({}, 500)});
    renderWithApp(<IncomingConsults title="Consults for the lab" />);
    expect(await screen.findByText('Consults for the lab could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
