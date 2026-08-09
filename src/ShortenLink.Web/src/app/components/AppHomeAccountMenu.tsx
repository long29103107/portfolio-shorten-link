import type { AdminPermissionState } from "@/features/short-links/api/adminSecurity";
import type { SecurityCurrentUser } from "@/features/short-links/types";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger
} from "@/shared/components/ui/dropdown-menu";
import { APP_ROUTES } from "@/shared/constants/routes";

type AppHomeAccountMenuProps = {
  currentUser: SecurityCurrentUser;
  adminPermissions: AdminPermissionState;
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  navigate: (path: string) => void;
  signOut: () => void;
};

export function AppHomeAccountMenu({
  currentUser,
  adminPermissions,
  isOpen,
  onOpenChange,
  navigate,
  signOut
}: AppHomeAccountMenuProps) {
  return (
    <div className="endpoint-actions">
      <DropdownMenu open={isOpen} onOpenChange={onOpenChange}>
        <DropdownMenuTrigger className="account-menu-trigger" aria-label="Open account menu">
          <span className="account-avatar" aria-hidden="true">
            {(currentUser.displayName || currentUser.username).slice(0, 1).toUpperCase()}
          </span>
          <span className="account-trigger-copy">
            <strong>{currentUser.displayName || currentUser.username}</strong>
            <span aria-hidden="true">Â·</span>
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
        {isOpen ? (
          <DropdownMenuContent className="account-menu-content">
            <DropdownMenuItem onClick={() => navigate(APP_ROUTES.SHORT_LINKS)}>
              Short links management
            </DropdownMenuItem>
            {adminPermissions.canManageSecurityAssignments ? (
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
    </div>
  );
}
