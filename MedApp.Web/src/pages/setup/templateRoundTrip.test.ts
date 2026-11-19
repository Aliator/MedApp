import {call, startLightDemo} from '../../test/lightDemo';
import type {Schemas} from '../../api/client';
import {ADMIN} from '../../demo/accounts';
import {startingEdit, templateSaveRequest} from './PathwayTemplateEditorPage';

type Template = Schemas['PathwayTemplateResponse'];

/**
 * A template as the app reads it: a field left out and a null mean the same (older seed stages leave out their SNOMED
 * CT concept), and its links are a set (the editor sends them in stage order).
 */
const asRead = (template: Template): Template => {
  const links = [...(template.links ?? [])].sort((a, b) => a.fromStageOrder! - b.fromStageOrder! || a.toStageOrder! - b.toStageOrder!);
  return JSON.parse(JSON.stringify({...template, links}, (_, v: unknown) => (v === null ? undefined : v)));
};

describe('the template editor', () => {
  it('saves every seeded template opened and saved unchanged exactly as it was', async () => {
    const {state} = startLightDemo(ADMIN);
    const labTeamId = state.teams.find((t) => t.kind === 'Lab')?.id ?? null;
    expect(state.templates.length).toBeGreaterThanOrEqual(10);
    for (const template of structuredClone(state.templates)) {
      const request = templateSaveRequest(template, startingEdit(template), labTeamId);
      const saved = await call('PATCH', `/api/pathways/patient-pathway-stage/templates/${template.id}`, request);
      expect(saved.status, `${template.name}: ${JSON.stringify(saved.body)}`).toBe(200);
      expect(asRead(saved.body), template.name!).toEqual(asRead(template));
    }
  });
});
