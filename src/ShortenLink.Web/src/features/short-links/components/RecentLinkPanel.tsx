import { useEffect, useRef, useState, type MouseEvent } from "react";
import type { CreatedShortLink } from "../types";
import { formatDateTime } from "../types";
import { Card, CardContent, CardHeader, CardTitle } from "../../../shared/components/ui/card";
import { PortalTooltip } from "../../../shared/components/ui/portal-tooltip";

type RecentLinkPanelProps = {
  recentLink: CreatedShortLink | null;
};

export function RecentLinkPanel({ recentLink }: RecentLinkPanelProps) {
  const [copyState, setCopyState] = useState<"idle" | "copied" | "error">("idle");
  const [copyTooltip, setCopyTooltip] = useState<{ x: number; y: number } | null>(null);
  const copyResetTimer = useRef<number | null>(null);

  useEffect(() => () => {
    if (copyResetTimer.current !== null) {
      window.clearTimeout(copyResetTimer.current);
    }
  }, []);

  const handleCopy = async (event: MouseEvent<HTMLButtonElement>) => {
    if (!recentLink) {
      return;
    }

    const buttonRect = event.currentTarget.getBoundingClientRect();
    try {
      await navigator.clipboard.writeText(recentLink.shortUrl);
      setCopyState("copied");
      setCopyTooltip({
        x: buttonRect.left,
        y: buttonRect.top - 8
      });
      if (copyResetTimer.current !== null) {
        window.clearTimeout(copyResetTimer.current);
      }
      copyResetTimer.current = window.setTimeout(() => {
        setCopyState("idle");
        setCopyTooltip(null);
        copyResetTimer.current = null;
      }, 1600);
    } catch {
      setCopyState("error");
      setCopyTooltip(null);
    }
  };

  if (!recentLink) {
    return (
      <Card className="panel-preview panel-empty">
        <CardHeader>
          <p className="eyebrow">Result</p>
          <CardTitle>Your latest link will land here.</CardTitle>
        </CardHeader>
        <CardContent>
        <p className="muted-copy">
          Create a link to get a copy-ready short URL with a random code generated
          by the app.
        </p>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="panel-preview">
      <CardHeader>
        <p className="eyebrow">Result</p>
        <CardTitle>{recentLink.code}</CardTitle>
      </CardHeader>

      <CardContent>
      <dl className="detail-list">
        <div className="short-url-detail">
          <dt>Short URL</dt>
          <dd>
            <a href={recentLink.shortUrl} target="_blank" rel="noreferrer">
              {recentLink.shortUrl}
            </a>
            <button
              className={copyState === "copied" ? "copy-icon-button copy-icon-button-done" : "copy-icon-button"}
              type="button"
              disabled={copyState === "copied"}
              aria-label={copyState === "copied" ? "Short URL copied" : "Copy short URL"}
              title={copyState === "copied" ? "Copied" : "Copy short URL"}
              onClick={handleCopy}
            >
              <span aria-hidden="true" />
            </button>
          </dd>
        </div>
        <div>
          <dt>Destination</dt>
          <dd>{recentLink.originalUrl}</dd>
        </div>
        <div>
          <dt>Created</dt>
          <dd>{formatDateTime(recentLink.createdAtUtc)}</dd>
        </div>
      </dl>

      {copyState === "error" ? (
        <p className="feedback feedback-error">
          Clipboard access was blocked, so the URL could not be copied.
        </p>
      ) : null}
      </CardContent>
      <PortalTooltip position={copyTooltip}>Copied</PortalTooltip>
    </Card>
  );
}
