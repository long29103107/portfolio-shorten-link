export type LoginFormInput = {
  email: string;
  password: string;
};

export type LoginFieldErrors = Partial<Record<keyof LoginFormInput, string>>;

export type ManagedUserFormInput = {
  email: string;
  displayName: string;
  password: string;
};

export type ManagedUserFieldErrors = Partial<Record<keyof ManagedUserFormInput, string>>;

export function validateLoginForm(form: LoginFormInput): LoginFieldErrors {
  const errors: LoginFieldErrors = {};
  if (!form.email.trim()) {
    errors.email = "Enter your email.";
  }
  if (!form.password.trim()) {
    errors.password = "Enter your password.";
  }
  return errors;
}

export function mapLoginApiFieldErrors(fieldErrors: Record<string, string>): LoginFieldErrors {
  return pickKnownFields(fieldErrors, ["email", "password"]);
}

export function validateManagedUserForm(
  form: ManagedUserFormInput
): ManagedUserFieldErrors {
  const errors: ManagedUserFieldErrors = {};
  const email = form.email.trim();
  if (!email) {
    errors.email = "Enter an email address.";
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    errors.email = "Enter a valid email address.";
  }
  if (!form.displayName.trim()) {
    errors.displayName = "Enter a display name.";
  }
  return errors;
}

export function mapManagedUserApiFieldErrors(
  fieldErrors: Record<string, string>
): ManagedUserFieldErrors {
  const errors: ManagedUserFieldErrors = pickKnownFields(fieldErrors, ["displayName", "password"]);
  if (fieldErrors.username) {
    errors.email = fieldErrors.username;
  }
  return errors;
}

export function validatePasswordReset(password: string): { password?: string } {
  return password.trim() ? {} : { password: "Enter a new password." };
}

export function mapPasswordResetApiFieldErrors(fieldErrors: Record<string, string>): { password?: string } {
  return pickKnownFields(fieldErrors, ["password"]);
}

export function mapRoleAssignmentApiFieldErrors(fieldErrors: Record<string, string>): { roleIds?: string } {
  return pickKnownFields(fieldErrors, ["roleIds"]);
}

export function hasFieldErrors(errors: Record<string, string | undefined>): boolean {
  return Object.values(errors).some(Boolean);
}

function pickKnownFields<TField extends string>(
  fieldErrors: Record<string, string>,
  fields: readonly TField[]
): Partial<Record<TField, string>> {
  const errors: Partial<Record<TField, string>> = {};
  for (const field of fields) {
    if (fieldErrors[field]) {
      errors[field] = fieldErrors[field];
    }
  }
  return errors;
}
