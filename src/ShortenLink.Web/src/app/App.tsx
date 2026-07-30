import { startTransition, useEffect, useState } from "react";
import { CreateShortLinkPage } from "../features/short-links/pages/CreateShortLinkPage";
import {
  clearStoredSession,
  getAdminPermissionState,
  getStoredCurrentUser,
  getStoredRefreshToken,
  getStoredSessionToken,
  storeSession
} from "../features/short-links/api/adminSecurity";
import { getCurrentSecurityUser } from "../features/short-links/api/shortLinksApi";
import { LoginPage } from "../features/short-links/pages/LoginPage";
import { AdminDashboardPage } from "../features/short-links/pages/AdminDashboardPage";
import { AuditLogPage } from "../features/short-links/pages/AuditLogPage";
import { SecurityManagementPage } from "../features/short-links/pages/SecurityManagementPage";
import { ShortLinkAdminPage } from "../features/short-links/pages/ShortLinkAdminPage";
import { StatusPage } from "../features/short-links/pages/StatusPage";
import { ShortLinkDetailPage } from "../features/short-links/pages/ShortLinkDetailPage";
import type { AppRoute, CreatedShortLink } from "../features/short-links/types";
import { Button } from "../shared/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "../shared/components/ui/dropdown-menu";
import { ConfirmDialog } from "../shared/components/ConfirmDialog";
import { Toaster } from "../shared/components/Toaster";
import { parseRoute } from "./router";

type NavigationIconName = "endpoint" | "admin" | "audit" | "users" | "roles" | "sign-in";

const securitySectionIcons = {
  users: "users",
  roles: "roles"
} as const satisfies Record<"users" | "roles", NavigationIconName>;

export function App() {
  const [route, setRoute] = useState<AppRoute>(() =>
    getStoredSessionToken() ? parseRoute(window.location.pathname) : { kind: "login" }
  );
  const [recentLink, setRecentLink] = useState<CreatedShortLink | null>(null);
  const [hasAdminEditChanges, setHasAdminEditChanges] = useState(false);
  const [pendingNavigationPath, setPendingNavigationPath] = useState<string | null>(null);
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false);
  const [currentUser, setCurrentUser] = useState(() => getStoredCurrentUser());
  const adminPermissions = getAdminPermissionState();

  useEffect(() => {
    const handleAuthChanged = () => {
      const nextUser = getStoredCurrentUser();
      setCurrentUser(nextUser);
      if (!getStoredSessionToken() && window.location.pathname !== "/login") {
        window.history.replaceState({}, "", "/login");
        setRoute({ kind: "login" });
      }
    };

    window.addEventListener("shortenlink-auth-changed", handleAuthChanged);
    return () => window.removeEventListener("shortenlink-auth-changed", handleAuthChanged);
  }, []);

  useEffect(() => {
    const token = getStoredSessionToken();
    if (!token) {
      if (window.location.pathname !== "/login") {
        window.history.replaceState({}, "", "/login");
      }
      return;
    }

    let isCurrent = true;
    void getCurrentSecurityUser()
      .then((user) => {
        if (isCurrent) {
          const refreshToken = getStoredRefreshToken();
          if (refreshToken) {
            storeSession(token, refreshToken, user);
          } else {
            clearStoredSession();
          }
        }
      })
      .catch(() => {
        if (isCurrent) {
          clearStoredSession();
        }
      });

    return () => {
      isCurrent = false;
    };
  }, []);

  useEffect(() => {
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      if (!hasAdminEditChanges) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasAdminEditChanges]);

  useEffect(() => {
    const handlePopState = () => {
      const nextPath = window.location.pathname;
      if (!getStoredSessionToken() && nextPath !== "/login") {
        window.history.replaceState({}, "", "/login");
        setRoute({ kind: "login" });
        return;
      }
      const currentPath = route.kind === "admin"
        ? "/short-links"
        : route.kind === "security"
          ? `/admin/security/${route.section}`
          : route.kind === "audit"
            ? "/audit-logs"
          : route.kind === "dashboard"
            ? "/admin/dashboard"
            : window.location.pathname;
      if (hasAdminEditChanges && nextPath !== currentPath) {
        window.history.pushState({}, "", currentPath);
        setPendingNavigationPath(nextPath);
        startTransition(() => {
          setRoute(parseRoute(currentPath));
        });
        return;
      }

      startTransition(() => {
        setRoute(parseRoute(nextPath));
      });
    };

    window.addEventListener("popstate", handlePopState);
    return () => window.removeEventListener("popstate", handlePopState);
  }, [hasAdminEditChanges, route]);

  const commitNavigation = (requestedPath: string) => {
    const path = !getStoredSessionToken() && requestedPath !== "/login"
      ? "/login"
      : requestedPath;
    if (window.location.pathname !== path) {
      window.history.pushState({}, "", path);
    }

    startTransition(() => {
      setRoute(parseRoute(path));
    });
  };

  const navigate = (path: string) => {
    if (hasAdminEditChanges && path !== window.location.pathname) {
      setPendingNavigationPath(path);
      return;
    }

    commitNavigation(path);
  };

  const confirmDiscardAndNavigate = () => {
    if (!pendingNavigationPath) {
      return;
    }

    setHasAdminEditChanges(false);
    commitNavigation(pendingNavigationPath);
    setPendingNavigationPath(null);
  };

  const pageTitle =
    route.kind === "admin"
      ? "Admin"
      : route.kind === "dashboard"
        ? "Dashboard"
      : route.kind === "audit"
        ? "Audit logs"
      : route.kind === "security"
        ? "Identity & Access"
      : route.kind === "login"
        ? "Sign in"
      : route.kind === "detail"
        ? "Link detail"
        : route.kind === "status"
          ? `${route.statusCode}`
          : "Endpoint";

  const pageDescription =
    route.kind === "admin"
      ? "Manage generated random short links"
      : route.kind === "dashboard"
        ? "Monitor short links and access controls"
      : route.kind === "audit"
        ? "Investigate durable mutation history"
      : route.kind === "security"
        ? `Manage ${route.section} access controls`
      : route.kind === "login"
        ? "Use your ShortenLink identity session"
      : route.kind === "detail"
        ? "Inspect and retire one generated link"
        : route.kind === "status"
          ? "Return to the short-link workspace"
          : "Random short-link creation";

  if (route.kind === "status" || route.kind === "login") {
    return (
      <div className="status-shell">
        {route.kind === "status" ? (
          <StatusPage
            statusCode={route.statusCode}
            onBackHome={() => navigate("/")}
          />
        ) : (
          <LoginPage
            onSignedIn={() => navigate("/")}
          />
        )}
        <ConfirmDialog
          open={pendingNavigationPath !== null}
          title="Discard form changes?"
          description="You have unsaved changes in the admin form. Leave this page and discard them?"
          confirmLabel="Discard changes"
          cancelLabel="Stay"
          variant="destructive"
          onConfirm={confirmDiscardAndNavigate}
          onCancel={() => setPendingNavigationPath(null)}
        />
        <Toaster />
      </div>
    );
  }

  return (
    <div className={route.kind === "home" ? "app-shell app-shell-focus" : "app-shell"}>
      {route.kind !== "home" ? (
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
              onClick={() => navigate("/admin/dashboard")}
            >
              <NavigationIcon name="admin" />
              Dashboard
            </Button>
            {adminPermissions.canReadAuditLogs ? (
              <Button
                className="sidebar-nav-button"
                variant="ghost"
                onClick={() => navigate("/audit-logs")}
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
                  onClick={() => navigate(`/admin/security/${section}`)}
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
                onClick={() => navigate("/short-links")}
              >
                <NavigationIcon name="endpoint" />
                Short links
              </Button>
              {adminPermissions.canReadAuditLogs ? (
                <Button
                  className="sidebar-nav-button"
                  aria-current={route.kind === "audit" ? "page" : undefined}
                  variant="ghost"
                  onClick={() => navigate("/audit-logs")}
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
              <DropdownMenu open={isAccountMenuOpen} onOpenChange={setIsAccountMenuOpen}>
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
                    <DropdownMenuItem onClick={() => navigate("/")}>
                      Back to home
                    </DropdownMenuItem>
                    {route.kind === "dashboard" || route.kind === "security" ? (
                      <DropdownMenuItem onClick={() => navigate("/short-links")}>
                        Manage short links
                      </DropdownMenuItem>
                    ) : null}
                    {route.kind === "admin" && adminPermissions.canManageSecurityAssignments ? (
                      <DropdownMenuItem onClick={() => navigate("/admin/dashboard")}>
                        Admin management
                      </DropdownMenuItem>
                    ) : null}
                    <DropdownMenuItem
                      className="account-sign-out"
                      onClick={() => {
                        clearStoredSession();
                        navigate("/login");
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
              onClick={() => navigate("/login")}
            >
              <NavigationIcon name="sign-in" />
              Sign in
            </Button>
          )}
        </div>
      </aside>
      ) : null}

      <main className="app-main">
        {route.kind === "home" && currentUser ? (
          <div className="endpoint-actions">
            <DropdownMenu open={isAccountMenuOpen} onOpenChange={setIsAccountMenuOpen}>
              <DropdownMenuTrigger className="account-menu-trigger" aria-label="Open account menu">
                <span className="account-avatar" aria-hidden="true">
                  {(currentUser.displayName || currentUser.username).slice(0, 1).toUpperCase()}
                </span>
                <span className="account-trigger-copy">
                  <strong>{currentUser.displayName || currentUser.username}</strong>
                  <span aria-hidden="true">·</span>
                  <small>{currentUser.roles.join(", ") || "No role"}</small>
                </span>
                <svg
                  className="account-menu-chevron"
                  aria-hidden="true"
                  viewBox="0 0 24 24"
                  fill="none"
                >
                  <path d="m6 9 6 6 6-6" />
                </svg>
              </DropdownMenuTrigger>
              {isAccountMenuOpen ? (
                <DropdownMenuContent className="account-menu-content">
                  <DropdownMenuItem onClick={() => navigate("/short-links")}>
                    Short links management
                  </DropdownMenuItem>
                  {adminPermissions.canManageSecurityAssignments ? (
                    <DropdownMenuItem onClick={() => navigate("/admin/dashboard")}>
                      Admin management
                    </DropdownMenuItem>
                  ) : null}
                  <DropdownMenuItem
                    className="account-sign-out"
                    onClick={() => {
                      clearStoredSession();
                      navigate("/login");
                    }}
                  >
                    Sign out
                  </DropdownMenuItem>
                </DropdownMenuContent>
              ) : null}
            </DropdownMenu>
          </div>
        ) : null}

        {route.kind !== "security" && route.kind !== "home" && route.kind !== "admin" && route.kind !== "dashboard" ? (
          <header className="topbar">
            <div>
              <p className="eyebrow">Shorten Link</p>
              <h1 className="app-title">{pageTitle}</h1>
              <p className="page-description">{pageDescription}</p>
            </div>
          </header>
        ) : null}

        <div className="workspace">
        {route.kind === "home" ? (
          <CreateShortLinkPage
            recentLink={recentLink}
            onCreated={(createdLink) => setRecentLink(createdLink)}
          />
        ) : null}

        {route.kind === "admin" ? (
          <ShortLinkAdminPage onDirtyChange={setHasAdminEditChanges} />
        ) : null}

        {route.kind === "dashboard" ? (
          <AdminDashboardPage />
        ) : null}

        {route.kind === "audit" ? (
          adminPermissions.canReadAuditLogs
            ? <AuditLogPage />
            : <StatusPage statusCode={403} onBackHome={() => navigate("/")} />
        ) : null}

        {route.kind === "security" ? (
          <SecurityManagementPage section={route.section} onDirtyChange={setHasAdminEditChanges} />
        ) : null}

        {route.kind === "detail" ? (
          <ShortLinkDetailPage
            code={route.code}
            onBackHome={() => navigate("/")}
          />
        ) : null}

        </div>
      </main>
      <ConfirmDialog
        open={pendingNavigationPath !== null}
        title="Discard form changes?"
        description="You have unsaved changes in the admin form. Leave this page and discard them?"
        confirmLabel="Discard changes"
        cancelLabel="Stay"
        variant="destructive"
        onConfirm={confirmDiscardAndNavigate}
        onCancel={() => setPendingNavigationPath(null)}
      />
      <Toaster />
    </div>
  );
}

function NavigationIcon({ name }: { name: NavigationIconName }) {
  const paths: Record<NavigationIconName, React.ReactNode> = {
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
