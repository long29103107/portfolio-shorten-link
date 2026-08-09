import { useCallback, useEffect, useState } from "react";
import {
  clearStoredSession,
  getStoredCurrentUser,
  getStoredRefreshToken,
  getStoredSessionToken,
  storeSession
} from "@/features/short-links/api/adminSecurity";
import { getCurrentSecurityUser } from "@/features/short-links/api/shortLinksApi";
import { APP_EVENTS } from "@/shared/constants/events";
import { APP_ROUTES } from "@/shared/constants/routes";

type UseAppSessionOptions = {
  onUnauthenticated: () => void;
};

export function useAppSession({ onUnauthenticated }: UseAppSessionOptions) {
  const [currentUser, setCurrentUser] = useState(() => getStoredCurrentUser());

  useEffect(() => {
    const handleAuthChanged = () => {
      const nextUser = getStoredCurrentUser();
      setCurrentUser(nextUser);
      if (!getStoredSessionToken() && window.location.pathname !== APP_ROUTES.LOGIN) {
        onUnauthenticated();
      }
    };

    window.addEventListener(APP_EVENTS.AUTH_CHANGED, handleAuthChanged);
    return () => window.removeEventListener(APP_EVENTS.AUTH_CHANGED, handleAuthChanged);
  }, [onUnauthenticated]);

  useEffect(() => {
    const token = getStoredSessionToken();
    if (!token) {
      if (window.location.pathname !== APP_ROUTES.LOGIN) {
        onUnauthenticated();
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
  }, [onUnauthenticated]);

  const signOut = useCallback(() => {
    clearStoredSession();
  }, []);

  return { currentUser, signOut };
}

