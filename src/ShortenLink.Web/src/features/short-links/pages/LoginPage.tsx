import { FormEvent, useState } from "react";
import { loginSecurityUser } from "../api/shortLinksApi";
import { storeSession } from "../api/adminSecurity";
import { ApiError } from "../api/http";
import { toFriendlyErrorMessage } from "../types";
import { Button } from "@/shared/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/shared/components/ui/card";
import { FormField } from "@/shared/components/FormField";
import { showToast } from "@/shared/toast";
import { createRecoveryNotice, type RecoveryNotice } from "@/shared/api/recovery";
import {
  hasFieldErrors,
  mapLoginApiFieldErrors,
  validateLoginForm,
  type LoginFieldErrors
} from "../domain/identityValidation";

type LoginPageProps = {
  onSignedIn: () => void;
};

export function LoginPage({ onSignedIn }: LoginPageProps) {
  const [email, setEmail] = useState("admin@shortenlink.local");
  const [password, setPassword] = useState("admin");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [failure, setFailure] = useState<RecoveryNotice | null>(null);
  const [fieldErrors, setFieldErrors] = useState<LoginFieldErrors>({});

  const signIn = async () => {
    const nextFieldErrors = validateLoginForm({ email, password });
    if (hasFieldErrors(nextFieldErrors)) {
      setFieldErrors(nextFieldErrors);
      setFailure(null);
      return;
    }

    setIsSubmitting(true);
    setFailure(null);
    setFieldErrors({});

    try {
      const result = await loginSecurityUser(email.trim(), password);
      storeSession(result.accessToken, result.refreshToken, result.user);
      showToast({
        title: "Signed in",
        message: result.user.displayName || result.user.username,
        variant: "success"
      });
      onSignedIn();
    } catch (error) {
      const apiFieldErrors = error instanceof ApiError
        ? mapLoginApiFieldErrors(error.fieldErrors)
        : {};
      setFieldErrors(apiFieldErrors);
      const message = error instanceof ApiError
        ? toFriendlyErrorMessage(error.errorCode, error.message)
        : "Sign in failed.";
      setFailure(hasFieldErrors(apiFieldErrors) ? null : createRecoveryNotice(error, message));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    void signIn();
  };

  return (
    <Card className="login-panel status-page">
      <form onSubmit={handleSubmit}>
        <CardHeader>
          <p className="eyebrow">Admin identity</p>
          <CardTitle>Sign in</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="status-code-mark" aria-hidden="true">Secure access</div>
          <p className="muted-copy">Sign in with your ShortenLink identity to manage protected resources.</p>
          {failure ? (
            <div className={failure.retryable ? "recovery-banner" : "feedback feedback-error"} role="alert">
              {failure.retryable
                ? `${failure.message} Your credentials are still here; choose Sign in to try again.`
                : failure.message}
            </div>
          ) : null}
          <FormField id="login-email" label="Email" type="email" autoComplete="email" value={email} error={fieldErrors.email} onChange={(value) => {
                setEmail(value);
                setFieldErrors((current) => ({ ...current, email: undefined }));
              }} />
          <FormField id="login-password" label="Password" type="password" autoComplete="current-password" value={password} error={fieldErrors.password} onChange={(value) => {
                setPassword(value);
                setFieldErrors((current) => ({ ...current, password: undefined }));
              }} />
        </CardContent>
        <CardFooter>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? "Signing in" : "Sign in"}
          </Button>
        </CardFooter>
      </form>
    </Card>
  );
}
