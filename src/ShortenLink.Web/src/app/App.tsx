import { lazy, Suspense, useCallback, useState } from "react";
import { getAdminPermissionState } from "@/features/short-links/api/adminSecurity";
import { CreateShortLinkPage } from "@/features/short-links/pages/CreateShortLinkPage";
import { LoginPage } from "@/features/short-links/pages/LoginPage";
const AdminDashboardPage = lazy(() => import("@/features/short-links/pages/AdminDashboardPage").then(({ AdminDashboardPage }) => ({ default: AdminDashboardPage })));
const AuditLogPage = lazy(() => import("@/features/short-links/pages/AuditLogPage").then(({ AuditLogPage }) => ({ default: AuditLogPage })));
const BulkJobsPage = lazy(() => import("@/features/short-links/pages/BulkJobsPage").then(({ BulkJobsPage }) => ({ default: BulkJobsPage })));
const SecurityManagementPage = lazy(() => import("@/features/short-links/pages/SecurityManagementPage").then(({ SecurityManagementPage }) => ({ default: SecurityManagementPage })));
const ShortLinkAdminPage = lazy(() => import("@/features/short-links/pages/ShortLinkAdminPage").then(({ ShortLinkAdminPage }) => ({ default: ShortLinkAdminPage })));
import { StatusPage } from "@/features/short-links/pages/StatusPage";
const ShortLinkDetailPage = lazy(() => import("@/features/short-links/pages/ShortLinkDetailPage").then(({ ShortLinkDetailPage }) => ({ default: ShortLinkDetailPage })));
import type { CreatedShortLink } from "@/features/short-links/types";
import { ConfirmDialog } from "@/shared/components/ConfirmDialog";
import { Toaster } from "@/shared/components/Toaster";
import { HTTP_STATUS } from "@/shared/constants/http";
import { APP_ROUTES } from "@/shared/constants/routes";
import { AppHomeAccountMenu } from "./components/AppHomeAccountMenu";
import { AppSidebar } from "./components/AppSidebar";
import { AppTopbar } from "./components/AppTopbar";
import { useAppNavigation } from "./hooks/useAppNavigation";
import { useAppSession } from "./hooks/useAppSession";

export function App() {
  const [recentLink, setRecentLink] = useState<CreatedShortLink | null>(null);
  const [hasAdminEditChanges, setHasAdminEditChanges] = useState(false);
  const [isAccountMenuOpen, setIsAccountMenuOpen] = useState(false);
  const {
    route,
    pendingNavigationPath,
    navigate,
    forceNavigate,
    confirmDiscardAndNavigate,
    cancelPendingNavigation
  } = useAppNavigation({
    hasDirtyChanges: hasAdminEditChanges,
    onDiscardChanges: () => setHasAdminEditChanges(false)
  });
  const handleUnauthenticated = useCallback(
    () => forceNavigate(APP_ROUTES.LOGIN),
    [forceNavigate]
  );
  const { currentUser, signOut } = useAppSession({ onUnauthenticated: handleUnauthenticated });
  const adminPermissions = getAdminPermissionState();

  const pageTitle =
    route.kind === "admin"
      ? "Admin"
      : route.kind === "dashboard"
        ? "Dashboard"
      : route.kind === "audit"
        ? "Audit logs"
      : route.kind === "bulk-jobs"
        ? "Bulk jobs"
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
      : route.kind === "bulk-jobs"
        ? "Track background short-link operations"
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
            onBackHome={() => navigate(APP_ROUTES.HOME)}
          />
        ) : (
          <LoginPage
            onSignedIn={() => {
              setRecentLink(null);
              navigate(APP_ROUTES.HOME);
            }}
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
          onCancel={cancelPendingNavigation}
        />
        <Toaster />
      </div>
    );
  }

  return (
    <div className={route.kind === "home" ? "app-shell app-shell-focus" : "app-shell"}>
      {route.kind !== "home" ? (
        <AppSidebar
          route={route}
          currentUser={currentUser}
          adminPermissions={adminPermissions}
          isAccountMenuOpen={isAccountMenuOpen}
          onAccountMenuOpenChange={setIsAccountMenuOpen}
          navigate={navigate}
          signOut={signOut}
        />
      ) : null}

      <main className="app-main">
        {route.kind === "home" && currentUser ? (
          <AppHomeAccountMenu
            currentUser={currentUser}
            adminPermissions={adminPermissions}
            isOpen={isAccountMenuOpen}
            onOpenChange={setIsAccountMenuOpen}
            navigate={navigate}
            signOut={signOut}
          />
        ) : null}

        {route.kind !== "security" && route.kind !== "home" && route.kind !== "admin" && route.kind !== "dashboard" ? (
          <AppTopbar title={pageTitle} description={pageDescription} />
        ) : null}

        <Suspense fallback={<RouteLoading />}>
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
                : <StatusPage statusCode={HTTP_STATUS.FORBIDDEN} onBackHome={() => navigate(APP_ROUTES.HOME)} />
            ) : null}

            {route.kind === "bulk-jobs" ? (
              <BulkJobsPage />
            ) : null}

            {route.kind === "security" ? (
              <SecurityManagementPage section={route.section} onDirtyChange={setHasAdminEditChanges} />
            ) : null}

            {route.kind === "detail" ? (
              <ShortLinkDetailPage
                code={route.code}
                onBackHome={() => navigate(APP_ROUTES.HOME)}
              />
            ) : null}
          </div>
        </Suspense>
      </main>
      <ConfirmDialog
        open={pendingNavigationPath !== null}
        title="Discard form changes?"
        description="You have unsaved changes in the admin form. Leave this page and discard them?"
        confirmLabel="Discard changes"
        cancelLabel="Stay"
        variant="destructive"
        onConfirm={confirmDiscardAndNavigate}
        onCancel={cancelPendingNavigation}
      />
      <Toaster />
    </div>
  );
}

function RouteLoading() {
  return (
    <div className="workspace" role="status" aria-live="polite">
      <p className="eyebrow">Loading workspaceâ€¦</p>
    </div>
  );
}
