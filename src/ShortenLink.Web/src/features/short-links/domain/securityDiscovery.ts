import type { SecurityRole, SecurityUser } from "../types";

export type SecurityUserStatusFilter = "all" | "enabled" | "disabled";
export type SecurityUserSortField = "email" | "displayName" | "createdAt";
export type SortDirection = "asc" | "desc";

export type SecurityUserDiscovery = {
  search: string;
  status: SecurityUserStatusFilter;
  role: string;
  sortBy: SecurityUserSortField;
  direction: SortDirection;
};

export const defaultSecurityUserDiscovery: SecurityUserDiscovery = {
  search: "",
  status: "all",
  role: "all",
  sortBy: "createdAt",
  direction: "desc"
};

export type PermissionDiscoveryGroup = {
  id: string;
  name: string;
  permissions: string[];
};

export function discoverSecurityRoles(roles: readonly SecurityRole[], search: string): SecurityRole[] {
  const normalizedSearch = search.trim().toLocaleLowerCase();
  return roles.filter((role) =>
    !normalizedSearch
    || role.name.toLocaleLowerCase().includes(normalizedSearch)
    || role.id.toLocaleLowerCase().includes(normalizedSearch)
  );
}

export function discoverPermissionGroups(
  groups: readonly PermissionDiscoveryGroup[],
  search: string,
  getDescription: (permission: string) => string
): PermissionDiscoveryGroup[] {
  const normalizedSearch = search.trim().toLocaleLowerCase();
  return groups
    .map((group) => ({
      ...group,
      permissions: group.permissions.filter((permission) =>
        !normalizedSearch
        || group.name.toLocaleLowerCase().includes(normalizedSearch)
        || permission.toLocaleLowerCase().includes(normalizedSearch)
        || getDescription(permission).toLocaleLowerCase().includes(normalizedSearch)
      )
    }))
    .filter((group) => group.permissions.length > 0);
}

export function discoverSecurityUsers(users: SecurityUser[], query: SecurityUserDiscovery): SecurityUser[] {
  const search = query.search.trim().toLocaleLowerCase();
  const filtered = users.filter((user) => {
    const matchesSearch = !search
      || user.username.toLocaleLowerCase().includes(search)
      || user.displayName.toLocaleLowerCase().includes(search);
    const matchesStatus = query.status === "all"
      || (query.status === "enabled" ? user.isEnabled : !user.isEnabled);
    const matchesRole = query.role === "all"
      || (query.role === "none"
        ? user.roleIds.length === 0
        : user.roleIds.some((roleId) => roleId.toLocaleLowerCase() === query.role.toLocaleLowerCase()));
    return matchesSearch && matchesStatus && matchesRole;
  });

  return filtered.sort((left, right) => {
    const leftValue = query.sortBy === "email"
      ? left.username
      : query.sortBy === "displayName"
        ? left.displayName
        : left.createdAtUtc;
    const rightValue = query.sortBy === "email"
      ? right.username
      : query.sortBy === "displayName"
        ? right.displayName
        : right.createdAtUtc;
    const result = leftValue.localeCompare(rightValue);
    return query.direction === "asc" ? result : -result;
  });
}

export function paginateItems<T>(items: T[], page: number, pageSize: number): T[] {
  return items.slice((page - 1) * pageSize, page * pageSize);
}

export function getVisiblePages(currentPage: number, totalPages: number): Array<number | "gap"> {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, index) => index + 1);
  const pages = new Set([1, totalPages, currentPage - 1, currentPage, currentPage + 1]);
  const visible = [...pages].filter((page) => page >= 1 && page <= totalPages).sort((a, b) => a - b);
  const result: Array<number | "gap"> = [];
  visible.forEach((page, index) => {
    if (index > 0 && page - visible[index - 1] > 1) result.push("gap");
    result.push(page);
  });
  return result;
}
