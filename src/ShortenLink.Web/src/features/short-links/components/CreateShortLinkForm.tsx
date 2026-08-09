import { FormEvent, useState } from "react";
import { ApiError } from "../api/http";
import { createShortLink } from "../api/shortLinksApi";
import type { CreatedShortLink, ShortLinkFormInput } from "../types";
import { toFriendlyErrorMessage } from "../types";
import { Button } from "@/shared/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/shared/components/ui/card";
import { Input } from "@/shared/components/ui/input";
import { Label } from "@/shared/components/ui/label";
import { showToast } from "@/shared/toast";
import { ExpiryQuickPicks } from "./ExpiryQuickPicks";
import {
  hasShortLinkFieldErrors,
  mapShortLinkApiFieldErrors,
  validateShortLinkForm,
  type ShortLinkFieldErrors
} from "../domain/validation";

type CreateShortLinkFormProps = {
  onCreated: (createdLink: CreatedShortLink) => void;
};

const initialForm: ShortLinkFormInput = {
  originalUrl: "",
  activeFromLocal: "",
  expiredAtLocal: ""
};

export function CreateShortLinkForm({
  onCreated
}: CreateShortLinkFormProps) {
  const [form, setForm] = useState(initialForm);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<ShortLinkFieldErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = validateShortLinkForm(form);
    if (hasShortLinkFieldErrors(nextErrors)) {
      setFieldErrors(nextErrors);
      setErrorMessage(null);
      return;
    }

    setIsSubmitting(true);
    setFieldErrors({});
    setErrorMessage(null);

    try {
      const createdLink = await createShortLink({
        originalUrl: form.originalUrl.trim(),
        activeFromUtc: form.activeFromLocal ? new Date(form.activeFromLocal).toISOString() : null,
        expiredAtUtc: new Date(form.expiredAtLocal).toISOString()
      });

      onCreated(createdLink);
      showToast({
        title: "Short link created",
        message: createdLink.code,
        variant: "success"
      });
    } catch (error) {
      if (error instanceof ApiError) {
        const apiFieldErrors = mapShortLinkApiFieldErrors(error.fieldErrors);
        if (hasShortLinkFieldErrors(apiFieldErrors)) {
          setFieldErrors(apiFieldErrors);
          return;
        }
        setErrorMessage(toFriendlyErrorMessage(error.errorCode, error.message));
      } else {
        setErrorMessage("We hit an unexpected problem while creating the link.");
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Card className="panel-form">
      <form onSubmit={handleSubmit}>
      <CardHeader>
        <p className="eyebrow">Create</p>
        <CardTitle>Ship a short link in one pass.</CardTitle>
      </CardHeader>

      <CardContent>
      <Label className="field">
        <span className="field-label">
          Destination URL <span className="required-marker">*</span>
        </span>
        <Input
          type="url"
          required
          placeholder="https://example.com/really/long/path"
          value={form.originalUrl}
          aria-invalid={fieldErrors.originalUrl ? "true" : undefined}
          aria-describedby={fieldErrors.originalUrl ? "create-original-url-error" : undefined}
          onChange={(event) =>
            setForm((current) => ({ ...current, originalUrl: event.target.value }))
          }
        />
        {fieldErrors.originalUrl ? (
          <span id="create-original-url-error" className="field-error">{fieldErrors.originalUrl}</span>
        ) : null}
      </Label>

      <div className="field-grid">
        <Label className="field">
          <span className="field-label">
            Start at <span className="muted">(optional)</span>
          </span>
          <Input
            type="datetime-local"
            value={form.activeFromLocal}
            aria-invalid={fieldErrors.activeFromLocal ? "true" : undefined}
            aria-describedby={fieldErrors.activeFromLocal ? "create-active-from-error" : undefined}
            onChange={(event) =>
              setForm((current) => ({ ...current, activeFromLocal: event.target.value }))
            }
          />
          {fieldErrors.activeFromLocal ? (
            <span id="create-active-from-error" className="field-error">{fieldErrors.activeFromLocal}</span>
          ) : null}
        </Label>
        <Label className="field">
          <span className="field-label">
            Expiry <span className="required-marker">*</span>
          </span>
          <Input
            type="datetime-local"
            required
            value={form.expiredAtLocal}
            aria-invalid={fieldErrors.expiredAtLocal ? "true" : undefined}
            aria-describedby={fieldErrors.expiredAtLocal ? "create-expiry-error" : undefined}
            onChange={(event) =>
              setForm((current) => ({ ...current, expiredAtLocal: event.target.value }))
            }
          />
          {fieldErrors.expiredAtLocal ? (
            <span id="create-expiry-error" className="field-error">{fieldErrors.expiredAtLocal}</span>
          ) : null}
          <ExpiryQuickPicks
            onChange={(expiredAtLocal) =>
              setForm((current) => ({ ...current, expiredAtLocal }))
            }
          />
        </Label>
      </div>

      {errorMessage ? <p className="feedback feedback-error">{errorMessage}</p> : null}
      </CardContent>

      <CardFooter style={{ flexDirection: "row-reverse" }}>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Creating..." : "Create short link"}
        </Button>
        <Button
          variant="secondary"
          onClick={() => {
            setForm(initialForm);
            setFieldErrors({});
            setErrorMessage(null);
          }}
          disabled={isSubmitting}
        >
          Clear
        </Button>
      </CardFooter>
      </form>
    </Card>
  );
}
