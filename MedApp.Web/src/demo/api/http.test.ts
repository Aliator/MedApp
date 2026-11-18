import {answer, findRoute, NOT_AVAILABLE, on, problem, refuse} from './http';

const afterChange = vi.fn(async () => {});
const ask = (method: string, path: string, body?: unknown) =>
  answer(new Request(`http://localhost${path}`, {method, ...(body ? {body: JSON.stringify(body)} : {})}), afterChange);

describe('the stand-in API', () => {
  beforeAll(() => {
    on('get', '/api/practices/{id}', ({params}) => ({id: params.id, name: 'From the parameter'}));
    on('get', '/api/practices', () => [{name: 'The list'}]);
    on('delete', '/api/practices/{id}', () => undefined);
    on('put', '/api/practices/{id}', () => refuse(problem(409, 'Changed meanwhile.')));
  });
  beforeEach(() => afterChange.mockClear());

  it('prefers a literal path to one with a parameter, and reads the parameter', async () => {
    expect(findRoute('GET', '/api/practices')?.route.path).toBe('/api/practices');
    const response = await ask('GET', '/api/practices/abc');
    expect(await response.json()).toEqual({id: 'abc', name: 'From the parameter'});
  });

  it('answers nothing with a 204, and keeps the state after a change', async () => {
    const response = await ask('DELETE', '/api/practices/abc');
    expect(response.status).toBe(204);
    expect(afterChange).toHaveBeenCalledOnce();
  });

  it('answers a refusal as a ProblemDetails, keeping nothing', async () => {
    const response = await ask('PUT', '/api/practices/abc', {name: 'x'});
    expect(response.status).toBe(409);
    expect(await response.json()).toMatchObject({title: 'Changed meanwhile.', status: 409});
    expect(afterChange).not.toHaveBeenCalled();
  });

  it('says a path with no handler isn’t in the light demo', async () => {
    vi.spyOn(console, 'info').mockImplementation(() => {});
    const response = await ask('GET', '/api/no-such-thing');
    expect(response.status).toBe(501);
    expect(await response.json()).toMatchObject({title: NOT_AVAILABLE});
  });
});
