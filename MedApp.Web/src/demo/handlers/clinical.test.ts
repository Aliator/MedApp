import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN, HALLWORTH, LAB, STAFF} from '../accounts';
import {MEETINGS, PATHWAYS, TEAMS, USERS} from '../ids';
import {CLINICAL_IDS, CONSENT, LETTER_TEMPLATES, LETTERS, MDT_ITEMS} from '../slices/clinical';
import {db} from '../store';
import {today} from '../time';
import {canRecordConsent, clinicalGate} from './clinical';

const pathwayOf = (id: string) => db().pathways.find((p) => p.id === id)!;
const stageNamed = (pathwayId: string, name: string) => pathwayOf(pathwayId).stages.find((s) => s.name === name)!;

describe('the light demo’s letters, MDT and consent', () => {
  it('approves a letter submitted to you, and tells its author', async () => {
    startLightDemo(STAFF);
    // Samuel Okafor's letter is approved and still to send.
    expect((await call('GET', '/api/letters/count')).body).toEqual({toApprove: 1, toSend: 1});
    const awaiting = (await call('GET', '/api/letters/search?View=AwaitingMyApproval')).body;
    expect(awaiting.items.map((l: {id: string}) => l.id)).toEqual([LETTERS.awaitingApproval]);
    const letter = (await call('GET', `/api/letters/${LETTERS.awaitingApproval}`)).body;
    expect(letter).toMatchObject({canApprove: true, canReturn: true, canEdit: false, canPreview: false});
    expect((await call('POST', `/api/letters/${letter.id}/approve`, {expectedVersion: letter.version - 1})).status).toBe(409);
    const approved = await call('POST', `/api/letters/${letter.id}/approve`, {expectedVersion: letter.version});
    expect(approved.body).toMatchObject({status: 'Approved', approverUsername: STAFF.username});
    expect(db().notifications.some((n) => n.kind === 'LetterApproved' && n.subjectId === letter.id && n.title === 'Letter approved')).toBe(true);
  });

  it('edits, submits and returns a draft', async () => {
    startLightDemo(STAFF);
    const draft = (await call('GET', `/api/letters/${LETTERS.draft}`)).body;
    expect(draft.canEdit).toBe(true);
    const saved = await call('PUT', `/api/letters/${draft.id}`, {
      letterhead: draft.letterhead,
      body: `${draft.body}Punch biopsy within two weeks.`,
      closing: draft.closing,
      largePrint: false,
      recipients: [{kind: 'Other', name: 'Example Lane Surgery (synthetic)', addressLines: '1 Example Lane'}, {kind: 'Patient'}],
      expectedVersion: draft.version,
    });
    expect(saved.status).toBe(200);
    expect(saved.body.recipients.map((r: {kind: string}) => r.kind)).toEqual(['Other', 'Patient']);
    expect((await call('PUT', `/api/letters/${draft.id}`, {recipients: [{kind: 'GpPractice', practiceId: null}], expectedVersion: saved.body.version})).status).toBe(400);
    const unknownField = await call('PUT', `/api/letters/${draft.id}`, {body: '{{patient.diagnosis}}', recipients: [], expectedVersion: saved.body.version});
    expect(unknownField.status).toBe(400);
    expect((await call('POST', `/api/letters/${draft.id}/submit`, {approverUserId: STAFF.id, expectedVersion: saved.body.version})).status).toBe(400);
    // The admin is read only, so can't approve one.
    expect((await call('POST', `/api/letters/${draft.id}/submit`, {approverUserId: ADMIN.id, expectedVersion: saved.body.version})).status).toBe(400);
    expect((await call('POST', `/api/letters/${draft.id}/submit`, {approverUserId: HALLWORTH.id, expectedVersion: saved.body.version})).body.status).toBe(
      'AwaitingApproval',
    );

    startLightDemo(HALLWORTH);
    db().letters.find((l) => l.id === LETTERS.draft)!.status = 'AwaitingApproval';
    db().letters.find((l) => l.id === LETTERS.draft)!.approverUserId = HALLWORTH.id;
    const returned = await call('POST', `/api/letters/${LETTERS.draft}/return`, {comment: 'Add the plan.', expectedVersion: 2});
    expect(returned.body).toMatchObject({status: 'Draft', isReturned: true, returnComment: 'Add the plan.'});
  });

  it('records a letter as sent by hand, and refuses to send it by email', async () => {
    startLightDemo(HALLWORTH);
    const letter = (await call('GET', `/api/letters/${LETTERS.toSend}`)).body;
    const [gp, patient] = letter.recipients;
    expect(gp).toMatchObject({canRecordSent: true, canSendSecureEmail: false});
    const send = await call('POST', `/api/letters/${letter.id}/recipients/${gp.id}/send`, {expectedVersion: letter.version});
    expect(send.status).toBe(409);
    expect(send.body.title).toMatch(/no email or post/);
    const handedToGp = await call('POST', `/api/letters/${letter.id}/recipients/${gp.id}/record-sent`, {
      method: 'HandedToPatient',
      sentOn: today(),
      expectedVersion: letter.version,
    });
    expect(handedToGp.status).toBe(400);
    const once = (await call('POST', `/api/letters/${letter.id}/recipients/${gp.id}/record-sent`, {method: 'Post', sentOn: today(), expectedVersion: letter.version})).body;
    const twice = (await call('POST', `/api/letters/${letter.id}/recipients/${patient.id}/record-sent`, {method: 'Post', sentOn: today(), expectedVersion: once.version})).body;
    expect(twice.unsentCount).toBe(0);
    expect((await call('GET', '/api/letters/count')).body.toSend).toBe(0);
  });

  it('keeps the referral stage unfinished until its onward referral is approved and sent to everyone', async () => {
    startLightDemo(STAFF);
    const pathway = pathwayOf(PATHWAYS.referredToHospital);
    const stage = stageNamed(PATHWAYS.referredToHospital, 'Refer to hospital');
    // Harriet Quill's onward referral to the hospital is seeded as n.thornbury's draft (MC28); a clinic letter doesn't count.
    expect(clinicalGate(pathway, stage)).toMatch(/needs an onward referral.*still a draft/);
    expect((await call('POST', '/api/letters', {templateId: LETTER_TEMPLATES.clinicLetter, pathwayId: pathway.id})).status).toBe(201);
    const draft = (await call('GET', `/api/letters/${LETTERS.onwardReferral}`)).body;
    expect(draft).toMatchObject({kind: 'OnwardReferral', templateName: 'Onward referral to hospital', status: 'Draft'});
    expect(draft.recipients.map((r: {kind: string}) => r.kind)).toEqual(['Hospital', 'GpPractice']);
    const approved = (await call('POST', `/api/letters/${draft.id}/approve`, {expectedVersion: draft.version})).body;
    expect(clinicalGate(pathway, stage)).toMatch(/not yet sent to every recipient/);
    let version = approved.version;
    for (const recipient of approved.recipients)
      version = (await call('POST', `/api/letters/${approved.id}/recipients/${recipient.id}/record-sent`, {method: 'Post', sentOn: today(), expectedVersion: version})).body
        .version;
    expect(clinicalGate(pathway, stage)).toBeNull();
  });

  it('keeps a biopsy stage unfinished until a valid consent record is there', async () => {
    startLightDemo(STAFF);
    const pathway = pathwayOf(PATHWAYS.biopsyToBook);
    const treatment = stageNamed(PATHWAYS.biopsyToBook, 'Take biopsy');
    expect(canRecordConsent(pathway, treatment)).toBe(true);
    expect(canRecordConsent(pathway, pathway.stages[0])).toBe(false);
    // Ellis Varden withdrew his first consent.
    expect(clinicalGate(pathway, treatment)).toMatch(/needs the patient's consent recorded.*has been withdrawn/);
    const records = `/api/pathways/${pathway.id}/consent-records`;
    const base = {stageId: treatment.id, signedOn: today(), signedBy: 'Patient'};
    expect((await call('POST', records, {...base, consentFormId: CONSENT.adultFormOld})).status).toBe(400);
    const recorded = await call('POST', records, {...base, consentFormId: CLINICAL_IDS.consentFormToChoose});
    expect(recorded.status).toBe(201);
    expect(clinicalGate(pathway, treatment)).toBeNull();
    const withdrawn = await call('POST', `/api/consent-records/${recorded.body.id}/withdraw`, {
      withdrawnOn: today(),
      detail: 'Changed their mind.',
      expectedVersion: recorded.body.version,
    });
    expect(withdrawn.body).toMatchObject({status: 'Withdrawn', canWithdraw: false});
    expect(clinicalGate(pathway, treatment)).toMatch(/has been withdrawn/);
  });

  it('archives a consent form once used, and deletes an unused one', async () => {
    startLightDemo(ADMIN);
    expect((await call('DELETE', `/api/consent-forms/${CONSENT.adultForm}`)).status).toBe(409);
    expect((await call('DELETE', `/api/consent-forms/${CONSENT.bestInterestsForm}`)).status).toBe(204);
    expect((await call('POST', `/api/consent-forms/${CONSENT.adultForm}/archive`)).body.isArchived).toBe(true);
    // The photo consent form (MC27) is left.
    expect((await call('GET', '/api/consent-forms')).body.map((f: {id: string}) => f.id)).toEqual([CONSENT.photographyForm]);
  });

  it('keeps a photographs stage unfinished until the patient’s consent to clinical photography is valid (MC27)', async () => {
    startLightDemo(STAFF);
    const pathway = pathwayOf(PATHWAYS.biopsyToBook);
    const biopsy = stageNamed(PATHWAYS.biopsyToBook, 'Take biopsy');
    // A photographs stage (the Mole check template's Take photographs), as the gate sees it.
    const photographs = {...biopsy, name: 'Take photographs'};
    const records = `/api/pathways/${pathway.id}/consent-records`;
    const base = {stageId: biopsy.id, signedOn: today(), signedBy: 'Patient'};
    expect((await call('POST', records, {...base, consentFormId: CONSENT.adultForm})).status).toBe(201);
    expect(clinicalGate(pathway, biopsy)).toBeNull();
    expect(clinicalGate(pathway, photographs)).toMatch(/needs the patient's consent to clinical photography.*Clinical photography form/);
    const photo = await call('POST', records, {...base, consentFormId: CONSENT.photographyForm});
    expect(photo.status).toBe(201);
    expect(clinicalGate(pathway, photographs)).toBeNull();
    await call('POST', `/api/consent-records/${photo.body.id}/withdraw`, {withdrawnOn: today(), detail: 'Changed their mind.', expectedVersion: 1});
    expect(clinicalGate(pathway, photographs)).toMatch(/photo consent recorded has been withdrawn/);
  });

  it('records an MDT outcome once the meeting has started, and tells the pathway’s assignees', async () => {
    startLightDemo(HALLWORTH);
    const meetings = (await call('GET', `/api/mdt/meetings?teamId=${TEAMS.dermatology}`)).body;
    expect(meetings.map((m: {id: string}) => m.id)).toEqual([CLINICAL_IDS.mdtToday, MEETINGS.skinMdt]);
    const list = (await call('GET', `/api/mdt/meetings/${CLINICAL_IDS.mdtToday}/items`)).body;
    const item = list.items.find((i: {id: string}) => i.id === CLINICAL_IDS.mdtItemToDiscuss);
    expect(item).toMatchObject({status: 'Listed', canRecordOutcome: true});
    const ahead = (await call('GET', `/api/mdt/meetings/${MEETINGS.skinMdt}/items`)).body.items[0];
    expect((await call('POST', `/api/mdt/items/${ahead.id}/outcome`, {outcome: 'x', presentUserIds: [STAFF.id], expectedVersion: ahead.version})).status).toBe(409);
    expect((await call('POST', `/api/mdt/items/${item.id}/outcome`, {outcome: 'Excise.', presentUserIds: ['nobody'], expectedVersion: item.version})).status).toBe(400);
    const recorded = await call('POST', `/api/mdt/items/${item.id}/outcome`, {outcome: 'Excise.', presentUserIds: [STAFF.id], expectedVersion: item.version});
    expect(recorded.body).toMatchObject({status: 'Discussed', outcome: 'Excise.', present: [STAFF.username], canRecordOutcome: false});
    expect(db().notifications.some((n) => n.userId === STAFF.id && n.kind === 'MdtOutcomeRecorded' && n.title === 'MDT outcome recorded')).toBe(true);
    expect((await call('POST', `/api/mdt/items/${item.id}/withdraw`, {expectedVersion: recorded.body.version})).status).toBe(409);
  });

  it('lists a pathway for an MDT once, defers it and withdraws it', async () => {
    startLightDemo(STAFF);
    const body = {pathwayId: PATHWAYS.referredToHospital, meetingId: CLINICAL_IDS.mdtToday, question: 'Margins?'};
    expect((await call('POST', '/api/mdt/items', body)).status).toBe(409);
    const listed = await call('POST', '/api/mdt/items', {...body, meetingId: MEETINGS.skinMdt});
    expect(listed.status).toBe(201);
    expect((await call('POST', '/api/mdt/items', {...body, meetingId: MEETINGS.skinMdt})).status).toBe(409);
    const items = (await call('GET', `/api/mdt/pathways/${PATHWAYS.referredToHospital}/items`)).body;
    expect(items.map((i: {id: string}) => i.id)).toEqual([listed.body.id, MDT_ITEMS.harrietPresented]);
    expect((await call('POST', `/api/mdt/items/${MDT_ITEMS.priyaListed}/defer`, {meetingId: CLINICAL_IDS.mdtToday, expectedVersion: 1})).status).toBe(409);
    expect((await call('POST', `/api/mdt/items/${listed.body.id}/withdraw`, {expectedVersion: 1})).body.status).toBe('Withdrawn');
  });

  it('lets the lab, invited to an MDT, read its listed cases until each is discussed, with the presenter and lab work', async () => {
    startLightDemo(LAB);
    const list = (await call('GET', `/api/mdt/meetings/${CLINICAL_IDS.mdtToday}/items`)).body;
    const harriet = list.items.find((i: {id: string}) => i.id === CLINICAL_IDS.mdtItemPresented);
    expect(harriet).toMatchObject({presenterUserId: LAB.id, presenterUsername: LAB.username, canChoosePresenter: false, canWithdraw: false, canDefer: false});
    expect(harriet.labWork).toHaveLength(1);
    expect(harriet.labWork[0]).toMatchObject({status: 'Reported', resultId: expect.any(String)});
    // Ellis Varden's pathway was never passed to the lab: the MDT grant shows it, read only, while the item is open.
    expect(list.items.map((i: {id: string}) => i.id)).toContain(MDT_ITEMS.toDiscuss);
    expect((await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`)).status).toBe(200);
    expect((await call('POST', `/api/mdt/items/${MDT_ITEMS.toDiscuss}/withdraw`, {expectedVersion: 1})).status).toBe(409);
    expect((await call('POST', `/api/mdt/items/${MDT_ITEMS.harrietPresented}/presenter`, {presenterUserId: null, expectedVersion: harriet.version})).status).toBe(409);
    // Discussed, with its meeting over, the grant ends.
    Object.assign(db().mdtItems.find((i) => i.id === MDT_ITEMS.toDiscuss)!, {status: 'Discussed'});
    expect((await call('GET', `/api/pathways/patient-pathways/${PATHWAYS.biopsyToBook}`)).status).toBe(404);
  });

  it('chooses a presenter from the MDT’s attendees, from the version shown', async () => {
    startLightDemo(STAFF);
    const url = `/api/mdt/items/${MDT_ITEMS.priyaListed}/presenter`;
    expect((await call('POST', url, {presenterUserId: LAB.id, expectedVersion: 1})).status).toBe(400);
    const chosen = await call('POST', url, {presenterUserId: USERS.sandiford, expectedVersion: 1});
    expect(chosen.body).toMatchObject({presenterUsername: 'b.sandiford', canChoosePresenter: true, version: 2});
    expect((await call('POST', url, {presenterUserId: null, expectedVersion: 1})).status).toBe(409);
    expect((await call('POST', url, {presenterUserId: null, expectedVersion: 2})).body.presenterUserId).toBeNull();
  });
});
