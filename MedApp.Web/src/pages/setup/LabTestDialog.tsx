import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {NumberInput} from '@astryxdesign/core/NumberInput';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Heading} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState} from 'react';
import {fieldStatus} from '../../api/errors';
import {
  LAB_STEP_KINDS,
  LAB_TEST_DEFAULTS,
  LAB_TEST_KINDS,
  LAB_TEST_LIMITS,
  labStepKindLabel,
  labTestKindLabel,
  type LabStepKind,
  type LabTest,
  type LabTestInput,
  type LabTestKind,
} from '../../api/labTests';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {TableActions} from '../../components/tables/TableActions';
import type {SaveProblem} from '../saveProblem';

type DraftStep = {key: number; name: string; kind: LabStepKind; isOptional: boolean};

/** The fields whose 400 messages show on them; the rest (`steps`: none, or too many) go in the banner. */
const FIELDS = ['name', 'kind', 'snomedCode', 'targetDays', 'limitDays', 'atRiskDays'];
const KIND_OPTIONS = LAB_TEST_KINDS.map((k) => ({value: k, label: labTestKindLabel(k)}));
const STEP_KIND_OPTIONS = LAB_STEP_KINDS.map((k) => ({value: k, label: labStepKindLabel(k)}));

let nextKey = 1;
const draftOf = (test: LabTest | null): DraftStep[] =>
  (test?.steps ?? []).map((s) => ({
    key: nextKey++,
    name: s.name ?? '',
    kind: (LAB_STEP_KINDS as readonly string[]).includes(s.kind ?? '') ? (s.kind as LabStepKind) : 'Standard',
    isOptional: s.isOptional ?? false,
  }));

export interface LabTestDialogProps {
  isOpen: boolean;
  /** The test being edited, or null to add one. */
  test: LabTest | null;
  isProcessing: boolean;
  problem: SaveProblem;
  onSave: (input: LabTestInput) => void;
  onCancel: () => void;
}

/**
 * Add or edit a lab test (MC13): its name, kind, SNOMED CT procedure code (optional), target and limit in calendar days,
 * how many days before the due date it's at risk, and its steps in order (each named, of a kind, optional or not),
 * moved up and down or removed. A new test starts at the RCPath histopathology turnaround (7 days, 10 at most, at risk
 * 2 before) and one standard step; nothing is suggested beyond that (decision 191). The API's 400s show on their
 * fields, its 409s above them. Mount it with a `key` per test, so its fields start from the test opened.
 */
export function LabTestDialog({isOpen, test, isProcessing, problem, onSave, onCancel}: LabTestDialogProps) {
  const [name, setName] = useState(test?.name ?? '');
  const [kind, setKind] = useState<LabTestKind | ''>(((test?.kind ?? '') as LabTestKind) || '');
  const [snomedCode, setSnomedCode] = useState(test?.snomedCode ?? '');
  const [targetDays, setTargetDays] = useState<number | null>(test?.targetDays ?? LAB_TEST_DEFAULTS.targetDays);
  const [limitDays, setLimitDays] = useState<number | null>(test?.limitDays ?? LAB_TEST_DEFAULTS.limitDays);
  const [atRiskDays, setAtRiskDays] = useState<number | null>(test?.atRiskDays ?? LAB_TEST_DEFAULTS.atRiskDays);
  const [steps, setSteps] = useState<DraftStep[]>(() =>
    test ? draftOf(test) : [{key: nextKey++, name: '', kind: 'Standard', isOptional: false}],
  );

  const change = (key: number, part: Partial<DraftStep>) => setSteps((all) => all.map((s) => (s.key === key ? {...s, ...part} : s)));
  const move = (index: number, by: -1 | 1) =>
    setSteps((all) => {
      const next = [...all];
      [next[index], next[index + by]] = [next[index + by], next[index]];
      return next;
    });

  const isComplete =
    name.trim() !== '' &&
    kind !== '' &&
    targetDays !== null &&
    limitDays !== null &&
    atRiskDays !== null &&
    steps.length > 0 &&
    steps.every((s) => s.name.trim() !== '');

  const save = () =>
    kind &&
    onSave({
      id: test?.id,
      name: name.trim(),
      kind,
      snomedCode: snomedCode.trim() || null,
      targetDays: targetDays ?? 0,
      limitDays: limitDays ?? 0,
      atRiskDays: atRiskDays ?? 0,
      steps: steps.map((s) => ({name: s.name.trim(), kind: s.kind, isOptional: s.isOptional})),
    });

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title={test ? 'Edit Lab Test' : 'Add Lab Test'}
      message={test ? 'Cases already open keep the steps they started with.' : undefined}
      icon={test ? AppIcon.Edit : AppIcon.Add}
      confirmText={test ? 'Save' : 'Add Lab Test'}
      confirmDisabled={!isComplete}
      isProcessing={isProcessing}
      hasSaveShortcut
      onConfirm={save}
      onCancel={onCancel}>
      <VStack gap={3}>
        {problem.validation && <ValidationAlert errors={problem.validation} fields={[...FIELDS, ...steps.map((_, i) => `steps[${i}].name`)]} />}
        {problem.message && <AlertMessage label={test ? 'Not saved' : 'Not added'} message={problem.message} />}
        <TextInput
          label="Name"
          value={name}
          onChange={setName}
          placeholder="e.g. Skin histology"
          autoComplete="off"
          isRequired
          hasAutoFocus
          status={fieldStatus(problem.validation, 'name')}
        />
        <HStack gap={3} wrap="wrap">
          <StackItem size="fill">
            <AnchoredSelector
              label="Kind"
              placeholder="Choose the kind"
              options={KIND_OPTIONS}
              value={kind || undefined}
              onChange={(value: string) => setKind(value as LabTestKind)}
              isRequired
              status={fieldStatus(problem.validation, 'kind')}
            />
          </StackItem>
          <StackItem size="fill">
            <TextInput
              label="SNOMED CT code"
              isOptional
              value={snomedCode}
              onChange={setSnomedCode}
              autoComplete="off"
              status={fieldStatus(problem.validation, 'snomedCode')}
            />
          </StackItem>
        </HStack>
        <HStack gap={3} wrap="wrap">
          <StackItem size="fill">
            <NumberInput
              label="Target, in days"
              description="Calendar days from receipt."
              value={targetDays}
              onChange={setTargetDays}
              min={LAB_TEST_LIMITS.minDays}
              max={LAB_TEST_LIMITS.maxDays}
              isRequired
              status={fieldStatus(problem.validation, 'targetDays')}
            />
          </StackItem>
          <StackItem size="fill">
            <NumberInput
              label="Limit, in days"
              description="The latest it may be."
              value={limitDays}
              onChange={setLimitDays}
              min={LAB_TEST_LIMITS.minDays}
              max={LAB_TEST_LIMITS.maxDays}
              isRequired
              status={fieldStatus(problem.validation, 'limitDays')}
            />
          </StackItem>
          <StackItem size="fill">
            <NumberInput
              label="At risk, days before"
              description="Before the due date."
              value={atRiskDays}
              onChange={setAtRiskDays}
              min={0}
              max={LAB_TEST_LIMITS.maxAtRiskDays}
              isRequired
              status={fieldStatus(problem.validation, 'atRiskDays')}
            />
          </StackItem>
        </HStack>
        <Heading level={3}>Steps</Heading>
        <VStack gap={2}>
          {steps.map((step, index) => (
            <HStack key={step.key} gap={2} vAlign="end" wrap="wrap">
              <StackItem size="fill">
                <TextInput
                  label={`Step ${index + 1}`}
                  value={step.name}
                  onChange={(value: string) => change(step.key, {name: value})}
                  autoComplete="off"
                  isRequired
                  status={fieldStatus(problem.validation, `steps[${index}].name`)}
                />
              </StackItem>
              <AnchoredSelector
                label={`Step ${index + 1}'s kind`}
                isLabelHidden
                options={STEP_KIND_OPTIONS}
                value={step.kind}
                onChange={(value: string) => change(step.key, {kind: value as LabStepKind})}
                width={210}
              />
              <Switch label="Optional" value={step.isOptional} onChange={(value: boolean) => change(step.key, {isOptional: value})} />
              <TableActions
                actions={[
                  {label: `Move step ${index + 1} up`, icon: AppIcon.ChevronUp, isDisabled: index === 0, onClick: () => move(index, -1)},
                  {label: `Move step ${index + 1} down`, icon: AppIcon.ChevronDown, isDisabled: index === steps.length - 1, onClick: () => move(index, 1)},
                  {
                    label: `Remove step ${index + 1}`,
                    icon: AppIcon.Delete,
                    isDestructive: true,
                    isDisabled: steps.length === 1,
                    onClick: () => setSteps((all) => all.filter((s) => s.key !== step.key)),
                  },
                ]}
              />
            </HStack>
          ))}
        </VStack>
        <HStack>
          <Button
            label="Add Step"
            variant="secondary"
            size="sm"
            icon={<Icon icon={AppIcon.Add} />}
            isDisabled={steps.length >= LAB_TEST_LIMITS.maxSteps}
            onClick={() => setSteps((all) => [...all, {key: nextKey++, name: '', kind: 'Standard', isOptional: false}])}
          />
        </HStack>
      </VStack>
    </ConfirmDialog>
  );
}
