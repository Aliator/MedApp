import { Button } from "@astryxdesign/core/Button";
import { Icon } from "@astryxdesign/core/Icon";
import { IconButton } from "@astryxdesign/core/IconButton";
import { HStack, StackItem, VStack } from "@astryxdesign/core/Stack";
import { Heading, Text } from "@astryxdesign/core/Text";
import type { Procedure } from "../../api/procedures";
import { formatDateTime } from "../../utils/dates";
import { AppIcon } from "../shared/icons";

type Assignment = NonNullable<Procedure["assignments"]>[number];

export interface AssigneeListProps {
  assignments: readonly Assignment[];
  /** Admins assign and unassign staff (and only while the procedure isn't finished with). */
  canManage: boolean;
  isBusy: boolean;
  /** Shows an Assign Staff button by the heading; leave out when the page offers it elsewhere. */
  onAssign?: () => void;
  /** Show the "Assigned staff" heading (the default); leave it off where the list sits under its own title. */
  isTitled?: boolean;
  onUnassign: (assignment: Assignment) => void;
}

/** Who the procedure is assigned to (it's on their task lists), who assigned them and when. */
export function AssigneeList({
  assignments,
  canManage,
  isBusy,
  onAssign,
  onUnassign,
  isTitled = true,
}: AssigneeListProps) {
  return (
    <VStack gap={4}>
      {isTitled && (
        <HStack gap={3} vAlign="center">
          <Heading level={2}>Assigned staff</Heading>
          <StackItem size="fill" />
          {canManage && onAssign && (
            <Button
              label="Assign Staff"
              variant="secondary"
              size="sm"
              icon={<Icon icon={AppIcon.Assign} />}
              onClick={onAssign}
              isDisabled={isBusy}
            />
          )}
        </HStack>
      )}
      {assignments.length === 0 ? (
        <Text type="body" color="secondary">
          Nobody is assigned yet.
        </Text>
      ) : (
        <VStack gap={3}>
          {assignments.map((a) => (
            <HStack key={a.userId} gap={3} vAlign="center">
              <VStack gap={0}>
                <Text type="body" weight="semibold">
                  {a.username}
                </Text>
                <Text type="supporting" color="secondary">
                  {`Assigned by ${a.assignedByUsername}, ${formatDateTime(a.assignedAtUtc ?? "")}`}
                </Text>
              </VStack>
              <StackItem size="fill" />
              {canManage && (
                <IconButton
                  label={`Unassign ${a.username}`}
                  tooltip={`Unassign ${a.username}`}
                  icon={<Icon icon={AppIcon.Revoke} />}
                  variant="ghost"
                  size="sm"
                  onClick={() => onUnassign(a)}
                  isDisabled={isBusy}
                />
              )}
            </HStack>
          ))}
        </VStack>
      )}
    </VStack>
  );
}
