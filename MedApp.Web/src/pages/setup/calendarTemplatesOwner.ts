import {useQuery, type QueryClient, type UseQueryResult} from '@tanstack/react-query';
import {staffQuery, type StaffMember} from '../../api/calendar';
import {useCurrentUser, whoamiQuery} from '../../auth/auth';

/** The signed-in user's staff ID: their entry in the staff list, found by username (as the Calendar page does). */
const idOf = (staff: readonly StaffMember[] | undefined, username: string) => staff?.find((s) => s.username === username)?.userId ?? undefined;

/**
 * Whose calendar templates a route's loader preloads: `staff` from the URL, or the signed-in user's own. Rendering on
 * the server it waits for the staff list (to find their ID); in the browser it uses the list if it's already here,
 * and otherwise leaves the page to find it once the list arrives.
 */
export async function templatesOwnerId(queryClient: QueryClient, staff: string | undefined): Promise<string | undefined> {
  if (staff) return staff;
  const me = await queryClient.ensureQueryData(whoamiQuery);
  const list =
    typeof window === 'undefined'
      ? await queryClient.ensureQueryData(staffQuery).catch(() => undefined)
      : queryClient.getQueryData(staffQuery.queryKey);
  return idOf(list, me.username);
}

export interface TemplatesOwner {
  /** Whose templates: `staff`, or the signed-in user's own (undefined until the staff list arrives). */
  ownerId: string | undefined;
  /** The signed-in user's staff ID (undefined until the staff list arrives). */
  myId: string | undefined;
  /** They're the signed-in user's own. */
  isOwn: boolean;
  /** The owner's username ('' until the staff list arrives, or for someone not in it). */
  ownerName: string;
  /** "My calendar templates", or "k.ashford's calendar templates". */
  title: string;
  /** The signed-in user can change them: they're their own, or they're an admin (the API's rule too). */
  canEdit: boolean;
  isAdmin: boolean;
  /** `staff` for links to this person's list and editors: none for your own. */
  staffParam: string | undefined;
  /** The staff list, for a picker. */
  staff: UseQueryResult<StaffMember[]>;
}

/**
 * Whose calendar templates a page shows, from the URL's `staff` (none, or your own ID: yours), and what the signed-in
 * user may do with them. The staff list names them.
 */
export function useTemplatesOwner(staffParam: string | undefined): TemplatesOwner {
  const me = useCurrentUser();
  const staff = useQuery(staffQuery);
  const myId = idOf(staff.data, me.username);
  const isOwn = staffParam === undefined || staffParam === myId;
  const ownerName = isOwn ? me.username : (staff.data?.find((s) => s.userId === staffParam)?.username ?? '');
  return {
    ownerId: isOwn ? myId : staffParam,
    myId,
    isOwn,
    ownerName,
    title: isOwn ? 'My calendar templates' : ownerName ? `${ownerName}'s calendar templates` : 'Calendar templates',
    canEdit: isOwn || me.isAdmin,
    isAdmin: me.isAdmin,
    staffParam: isOwn ? undefined : staffParam,
    staff,
  };
}
