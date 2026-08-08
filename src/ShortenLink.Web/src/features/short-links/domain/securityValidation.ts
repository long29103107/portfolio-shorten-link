export type CustomRoleFormInput = {
  id: string;
  name: string;
  permissions: string[];
};

export type CustomRoleFieldErrors = Partial<Record<keyof CustomRoleFormInput, string>>;

export function validateCustomRoleForm(form: CustomRoleFormInput): CustomRoleFieldErrors {
  const errors: CustomRoleFieldErrors = {};
  if (!form.id.trim()) {
    errors.id = "Enter a stable role id.";
  }
  if (!form.name.trim()) {
    errors.name = "Enter a role name.";
  }
  return errors;
}

export function mapCustomRoleApiFieldErrors(
  fieldErrors: Record<string, string>
): CustomRoleFieldErrors {
  const errors: CustomRoleFieldErrors = {};
  if (fieldErrors.id) errors.id = fieldErrors.id;
  if (fieldErrors.name) errors.name = fieldErrors.name;
  if (fieldErrors.permissions) errors.permissions = fieldErrors.permissions;
  return errors;
}
