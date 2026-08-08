export const permissionDescriptions: Record<string, string> = {
  "short_links.read": "View short links and export the data you can access.",
  "short_links.create": "Create new short links.",
  "short_links.update": "Edit destinations, aliases, and expiration settings.",
  "short_links.status": "Activate or deactivate short links.",
  "short_links.delete": "Permanently delete short links.",
  "short_links.import": "Import short-link data into your account.",
  "analytics.read": "View traffic and redirect analytics.",
  "audit_logs.read": "Review security and administrative activity logs."
};

export function getPermissionDescription(permission: string) {
  return permissionDescriptions[permission] ?? "Use this application permission.";
}
