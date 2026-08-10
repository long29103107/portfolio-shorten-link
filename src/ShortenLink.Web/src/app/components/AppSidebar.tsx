import type { ReactNode } from "react";
import type { AdminPermissionState } from "@/features/short-links/api/adminSecurity";
import type { AppRoute, SecurityCurrentUser } from "@/features/short-links/types";
import { Button } from "@/shared/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@/shared/components/ui/dropdown-menu";
import { APP_ROUTES, buildSecurityRoute } from "@/shared/constants/routes";

type NavigationIconName = "endpoint" | "admin" | "audit" | "bulk" | "users" | "roles" | "sign-in";

const securitySectionIcons = {
  users: "users",
  roles: "roles"
} as const satisfies Record<"users" | "roles", NavigationIconName>;

type AppSidebarProps = {
  route: AppRoute;
  currentUser: SecurityCurrentUser | null;
  adminPermissions: AdminPermissionState;
  isAccountMenuOpen: boolean;
  onAccountMenuOpenChange: (open: boolean) => void;
  navigate: (path: string) => void;
  signOut: () => void;
};

export function AppSidebar({
  route,
  currentUser,
  adminPermissions,
  isAccountMenuOpen,
  onAccountMenuOpenChange,
  navigate,
  signOut
}: AppSidebarProps) {
  return (
    <aside className="sidebar">
      <div className="brand-block">
        <div className="brand-mark">SL</div>
        <div>
          <h1>Shorten Link</h1>
        </div>
      </div>

      <div className="release-note">
        <p>Random code mode enabled</p>
        <code>100% generated links</code>
      </div>

      {route.kind === "dashboard" || route.kind === "security" ? (
        <nav className="sidebar-nav" aria-label="Admin navigation">
          <Button
            className="sidebar-nav-button"
            aria-current={route.kind === "dashboard" ? "page" : undefined}
            variant="ghost"
            onClick={() => navigate(APP_ROUTES.ADMIN_DASHBOARD)}
          >
            <NavigationIcon name="admin" />
            Dashboard
          </Button>
          {adminPermissions.canReadAuditLogs ? (
            <Button
              className="sidebar-nav-button"
              variant="ghost"
              onClick={() => navigate(APP_ROUTES.AUDIT_LOGS)}
            >
              <NavigationIcon name="audit" />
              Audit logs
            </Button>
          ) : null}
          <div className="sidebar-nav-group">
            <p className="sidebar-nav-group-label">Security</p>
            {(["users", "roles"] as const).map((section) => (
              <Button
                key={section}
                className="sidebar-nav-button sidebar-nav-child"
                aria-current={route.kind === "security" && route.section === section ? "page" : undefined}
                variant="ghost"
                onClick={() => navigate(buildSecurityRoute(section))}
              >
                <NavigationIcon name={securitySectionIcons[section]} />
                {section[0].toUpperCase() + section.slice(1)}
              </Button>
            ))}
          </div>
        </nav>
      ) : (
        <nav className="sidebar-nav" aria-label="Short links navigation">
          <div className="sidebar-nav-group">
            <p className="sidebar-nav-group-label">Workspace</p>
            <Button
              className="sidebar-nav-button"
              aria-current={route.kind === "admin" ? "page" : undefined}
              variant="ghost"
              onClick={() => navigate(APP_ROUTES.SHORT_LINKS)}
            >
              <NavigationIcon name="endpoint" />
              Short links
            </Button>
            <Button
              className="sidebar-nav-button"
              aria-current={route.kind === "bulk-jobs" ? "page" : undefined}
              variant="ghost"
              onClick={() => navigate(APP_ROUTES.BULK_JOBS)}
            >
              <NavigationIcon name="bulk" />
              Bulk jobs
            </Button>
            {adminPermissions.canReadAuditLogs ? (
              <Button
                className="sidebar-nav-button"
                aria-current={route.kind === "audit" ? "page" : undefined}
                variant="ghost"
                onClick={() => navigate(APP_ROUTES.AUDIT_LOGS)}
              >
                <NavigationIcon name="audit" />
                Audit logs
              </Button>
            ) : null}
          </div>
        </nav>
      )}

      <div className="session-panel">
        {currentUser ? (
          <>
            <p>{currentUser.displayName || currentUser.username}</p>
            <code>{currentUser.roles.join(", ") || "No role"}</code>
            <DropdownMenu open={isAccountMenuOpen} onOpenChange={onAccountMenuOpenChange}>
              <DropdownMenuTrigger
                className={isAccountMenuOpen ? "sidebar-account-trigger sidebar-account-trigger-open" : "sidebar-account-trigger"}
              >
                <span>Account</span>
                <svg
                  className="sidebar-account-more"
                  aria-hidden="true"
                  viewBox="0 0 24 24"
                  fill="none"
                >
                  <circle cx="5" cy="12" r="1.6" fill="currentColor" stroke="none" />
                  <circle cx="12" cy="12" r="1.6" fill="currentColor" stroke="none" />
                  <circle cx="19" cy="12" r="1.6" fill="currentColor" stroke="none" />
                </svg>
              </DropdownMenuTrigger>
              {isAccountMenuOpen ? (
                <DropdownMenuContent className="sidebar-account-menu" placement="right-end">
                  <DropdownMenuItem onClick={() => navigate(APP_ROUTES.HOME)}>
                    Back to home
                  </DropdownMenuItem>
                  {route.kind === "dashboard" || route.kind === "security" ? (
                    <DropdownMenuItem onClick={() => navigate(APP_ROUTES.SHORT_LINKS)}>
                      Manage short links
                    </DropdownMenuItem>
                  ) : null}
                  {route.kind === "admin" && adminPermissions.canManageSecurityAssignments ? (
                    <DropdownMenuItem onClick={() => navigate(APP_ROUTES.ADMIN_DASHBOARD)}>
                      Admin management
                    </DropdownMenuItem>
                  ) : null}
                  <DropdownMenuItem
                    className="account-sign-out"
                    onClick={() => {
                      signOut();
                      navigate(APP_ROUTES.LOGIN);
                    }}
                  >
                    Sign out
                  </DropdownMenuItem>
                </DropdownMenuContent>
              ) : null}
            </DropdownMenu>
          </>
        ) : (
          <Button
            className="sidebar-nav-button"
            variant="ghost"
            onClick={() => navigate(APP_ROUTES.LOGIN)}
          >
            <NavigationIcon name="sign-in" />
            Sign in
          </Button>
        )}
      </div>
    </aside>
  );
}

function NavigationIcon({ name }: { name: NavigationIconName }) {
  const paths: Record<NavigationIconName, ReactNode> = {
    endpoint: (
      <>
        <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
        <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
      </>
    ),
    admin: (
      <>
        <rect width="18" height="18" x="3" y="3" rx="2" />
        <path d="M8 3v18M8 8h13M8 13h13" />
      </>
    ),
    audit: (
      <>
        <path d="M4 19.5V4.5A2.5 2.5 0 0 1 6.5 2H19v20H6.5A2.5 2.5 0 0 1 4 19.5Z" />
        <path d="M8 7h7M8 11h7M8 15h4" />
      </>
    ),
    bulk: (
      <>
        <rect width="18" height="18" x="3" y="3" rx="2" />
        <path d="M8 8h8M8 12h5M8 16h3" />
      </>
    ),
    users: (
      <>
        <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
        <circle cx="9" cy="7" r="4" />
        <path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75" />
      </>
    ),
    roles: (
      <>
        <path d="M20 13c0 5-3.5 7.5-8 9-4.5-1.5-8-4-8-9V5l8-3 8 3v8Z" />
        <path d="m9 12 2 2 4-4" />
      </>
    ),
    "sign-in": (
      <>
        <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
        <path d="m10 17 5-5-5-5M15 12H3" />
      </>
    )
  };

  return (
    <svg
      className="nav-icon"
      aria-hidden="true"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {paths[name]}
    </svg>
  );
}
