import type {paths} from '../../api/schema';

/**
 * The light demo's stand-in for the API: a table of handlers, each typed by its path and method in the API's own
 * schema (`src/api/schema.d.ts`), so `npm run typecheck` checks that what a handler returns is the DTO the page reads.
 */

export type Method = 'get' | 'post' | 'put' | 'delete' | 'patch';
export type ApiPath = keyof paths;

/** The methods a path has (an absent one is `never` in the schema). */
export type MethodOf<P extends ApiPath> = {[M in Method]: [paths[P][M]] extends [never] ? never : paths[P][M] extends undefined ? never : M}[Method];

type Operation<P extends ApiPath, M extends Method> = NonNullable<paths[P][M]>;
type JsonOf<R> = R extends {content: {'application/json': infer T}} ? T : undefined;

/** What a successful answer carries: the 200's body, else the 201's or 202's, else nothing (a 204). */
export type ResponseOf<P extends ApiPath, M extends Method> =
  Operation<P, M> extends {responses: infer R}
    ? R extends {200: infer A}
      ? JsonOf<A>
      : R extends {201: infer B}
        ? JsonOf<B>
        : R extends {202: infer C}
          ? JsonOf<C>
          : undefined
    : never;

/** The request's JSON body, as the page sends it. */
export type BodyOf<P extends ApiPath, M extends Method> =
  Operation<P, M> extends {requestBody?: {content: {'application/json': infer T}}} ? Partial<T> : undefined;

export interface DemoRequest<B> {
  /** The path's `{name}` parts. */
  params: Record<string, string>;
  query: URLSearchParams;
  body: B;
}

/** A refusal, as the API words one (a ProblemDetails), or any answer other than a plain 200. */
export class DemoReply {
  constructor(
    readonly status: number,
    readonly body?: unknown,
  ) {}
}

/** A refusal thrown from deep in a handler (`refuse`), answered as its reply. */
export class Refusal extends Error {
  constructor(readonly reply: DemoReply) {
    super(`Refused with ${reply.status}`);
  }
}

/** Stops the handler and answers with this refusal. */
export function refuse(reply: DemoReply): never {
  throw new Refusal(reply);
}

/** Refuses the request with a ProblemDetails, as the API does: 404, 409 or 400 with the API's kind of words. */
export function problem(status: number, title: string, detail?: string): DemoReply {
  return new DemoReply(status, {type: 'about:blank', title, status, ...(detail ? {detail} : {})});
}

export const notFound = (what: string) => problem(404, `${what} wasn't found.`);

/** A 400 with errors on fields, as `ValidationProblemDetails`. */
export function invalid(errors: Record<string, string[]>): DemoReply {
  return new DemoReply(400, {type: 'about:blank', title: 'One or more validation errors occurred.', status: 400, errors});
}

export type Handler<P extends ApiPath, M extends Method> = (
  request: DemoRequest<BodyOf<P, M>>,
) => ResponseOf<P, M> | DemoReply | Promise<ResponseOf<P, M> | DemoReply>;

interface Route {
  method: Method;
  path: string;
  pattern: RegExp;
  names: string[];
  /** Literal paths win over ones with `{id}` parts (`/api/patients/search` over `/api/patients/{id}`). */
  params: number;
  handler: (request: DemoRequest<unknown>) => unknown;
}

const routes: Route[] = [];

/** Registers a handler for one of the API's paths. */
export function on<P extends ApiPath, M extends MethodOf<P>>(method: M, path: P, handler: Handler<P, M>) {
  const names: string[] = [];
  const pattern = new RegExp(
    `^${(path as string).replace(/[.*+?^$()|[\]\\]/g, '\\$&').replace(/\{(\w+)\}/g, (_, name: string) => {
      names.push(name);
      return '([^/]+)';
    })}$`,
  );
  routes.push({method, path, pattern, names, params: names.length, handler: handler as Route['handler']});
  routes.sort((a, b) => a.params - b.params);
}

/** The path a request is for, among the registered ones, and its `{name}` parts; null when there's no handler. */
export function findRoute(method: string, pathname: string): {route: Route; params: Record<string, string>} | null {
  const lower = method.toLowerCase();
  for (const route of routes) {
    if (route.method !== lower) continue;
    const match = route.pattern.exec(pathname);
    if (!match) continue;
    const params: Record<string, string> = {};
    route.names.forEach((name, i) => (params[name] = decodeURIComponent(match[i + 1])));
    return {route, params};
  }
  return null;
}

/** Every path with a handler, as `GET /api/...` (for the docs and tests). */
export const handledRoutes = () => routes.map((route) => `${route.method.toUpperCase()} ${route.path}`);

/** The answer to a path the light demo has no handler for: the page shows its usual error state. */
export const NOT_AVAILABLE = 'Not available in the light demo';

const json = (status: number, body: unknown) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: body === undefined ? {} : {'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json'},
  });

/**
 * Answers a request from the handlers: its result as JSON (200, or 204 for nothing), a {@link DemoReply} as it says,
 * and a path with no handler a ProblemDetails that says it isn't in the light demo. `afterChange` runs after a
 * change that succeeded (anything but a GET), to keep the state.
 */
export async function answer(request: Request, afterChange: () => Promise<void>): Promise<Response> {
  const url = new URL(request.url);
  const found = findRoute(request.method, url.pathname);
  if (!found) {
    console.info(`[light demo] ${NOT_AVAILABLE}: ${request.method} ${url.pathname}`);
    return json(501, {
      type: 'about:blank',
      title: NOT_AVAILABLE,
      status: 501,
      detail: 'The full version of MedApp does this on its server.',
    });
  }
  const text = request.method === 'GET' || request.method === 'HEAD' ? '' : await request.text();
  let body: unknown;
  try {
    body = text ? JSON.parse(text) : {};
  } catch {
    body = {};
  }
  try {
    const result = await found.route.handler({params: found.params, query: url.searchParams, body});
    if (result instanceof DemoReply) {
      if (result.status < 400 && request.method !== 'GET') await afterChange();
      return json(result.status, result.body);
    }
    if (request.method !== 'GET') await afterChange();
    return result === undefined ? new Response(null, {status: 204}) : json(200, result);
  } catch (error) {
    if (error instanceof Refusal) return json(error.reply.status, error.reply.body);
    console.error('[light demo]', error);
    return json(500, {type: 'about:blank', title: 'The light demo hit a problem with this request.', status: 500});
  }
}
