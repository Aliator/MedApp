import {screen} from '@testing-library/react';
import {useState} from 'react';
import type {ValidationErrors} from '../../api/errors';
import type {NoteDeletionReason} from '../../api/noteDeletion';
import {renderWithApp} from '../../test/render';
import {NoteDeletionReasonFields, reasonFieldsComplete} from './NoteDeletionReasonFields';

function Fields({errors}: {errors?: ValidationErrors | null}) {
  const [reason, setReason] = useState<NoteDeletionReason | ''>('');
  const [detail, setDetail] = useState('');
  return (
    <>
      <NoteDeletionReasonFields reason={reason} onReasonChange={setReason} detail={detail} onDetailChange={setDetail} errors={errors} />
      <output>{`${reason}|${detail}|${reasonFieldsComplete(reason, detail)}`}</output>
    </>
  );
}

describe('NoteDeletionReasonFields', () => {
  it('offers the four reasons, and a detail that is optional except for Other', async () => {
    const {user} = renderWithApp(<Fields />);

    expect(await screen.findAllByRole('radio')).toHaveLength(4);
    expect(screen.getByRole('radio', {name: 'Wrong patient'})).toBeInTheDocument();
    expect(screen.getByText('||false')).toBeInTheDocument();
    expect(screen.getByRole('textbox', {name: /Detail \(optional\)/})).toBeInTheDocument();

    await user.click(screen.getByRole('radio', {name: 'Duplicate'}));
    expect(screen.getByText('Duplicate||true')).toBeInTheDocument();

    await user.click(screen.getByRole('radio', {name: 'Other'}));
    expect(screen.getByText('Other||false')).toBeInTheDocument();
    await user.type(screen.getByRole('textbox', {name: /^Detail/}), 'Synthetic detail.');
    expect(screen.getByText('Other|Synthetic detail.|true')).toBeInTheDocument();
  });

  it('refuses a detail over 500 characters', () => {
    expect(reasonFieldsComplete('Duplicate', 'a'.repeat(500))).toBe(true);
    expect(reasonFieldsComplete('Duplicate', 'a'.repeat(501))).toBe(false);
    expect(reasonFieldsComplete('', 'Synthetic detail.')).toBe(false);
  });

  it("shows the API's messages on their fields", async () => {
    renderWithApp(<Fields errors={{title: 'Validation failed', fields: {reason: ['Choose why.'], detail: ['Say why.']}, general: []}} />);

    expect(await screen.findByText('Choose why.')).toBeInTheDocument();
    expect(screen.getByText('Say why.')).toBeInTheDocument();
  });
});
