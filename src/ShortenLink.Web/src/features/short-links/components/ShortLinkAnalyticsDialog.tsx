import type { ShortLinkAnalytics, ShortLinkAnalyticsDimension } from "../types";
import { formatDateTime } from "../types";
import { EmptyState } from "@/shared/components/EmptyState";
import { Button } from "@/shared/components/ui/button";

type ShortLinkAnalyticsDialogProps = {
  code: string;
  data: ShortLinkAnalytics | null;
  error: string | null;
  isRetryable: boolean;
  isLoading: boolean;
  onClose: () => void;
  onRetry: () => void;
};

function AnalyticsBreakdown({
  title,
  items
}: {
  title: string;
  items: ShortLinkAnalyticsDimension[] | null;
}) {
  if (!items || items.length === 0) {
    return null;
  }

  return (
    <div className="analytics-breakdown">
      <h3>{title}</h3>
      <ul>
        {items.map((item) => (
          <li key={`${title}-${item.name}`}>
            <span>{item.name}</span>
            <strong>{item.count}</strong>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function ShortLinkAnalyticsDialog({
  code,
  data,
  error,
  isRetryable,
  isLoading,
  onClose,
  onRetry
}: ShortLinkAnalyticsDialogProps) {
  return (
    <div className="dialog-backdrop" role="presentation">
      <div
        className="analytics-dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="analytics-dialog-title"
      >
        <div className="analytics-dialog-header">
          <div>
            <p className="eyebrow">Analytics</p>
            <h2 id="analytics-dialog-title">{code}</h2>
          </div>
          <Button variant="secondary" onClick={onClose}>Close</Button>
        </div>

        {isLoading ? (
          <div className="analytics-loading">
            <span className="skeleton skeleton-button" />
            <span className="skeleton skeleton-url" />
            <span className="skeleton skeleton-url" />
          </div>
        ) : null}

        {!isLoading && error ? (
          <EmptyState
            title="Analytics unavailable"
            description={error}
            action={isRetryable
              ? <Button variant="secondary" onClick={onRetry}>Retry</Button>
              : undefined}
          />
        ) : null}

        {!isLoading && !error && data ? (
          <div className="analytics-panel">
            <div className="analytics-metrics">
              <div>
                <span>Clicks</span>
                <strong>{data.clickCount}</strong>
              </div>
              <div>
                <span>Unique visitors</span>
                <strong>{data.uniqueClickCount ?? "Unavailable"}</strong>
              </div>
              <div>
                <span>Last clicked</span>
                <strong>{data.lastClickedAtUtc ? formatDateTime(data.lastClickedAtUtc) : "No clicks yet"}</strong>
              </div>
            </div>

            <div className="analytics-breakdown-grid">
              <AnalyticsBreakdown title="Devices" items={data.devices} />
              <AnalyticsBreakdown title="Browsers" items={data.browsers} />
              <AnalyticsBreakdown title="Operating systems" items={data.operatingSystems} />
              <AnalyticsBreakdown title="Countries" items={data.countries} />
              <AnalyticsBreakdown title="Referrers" items={data.referrers} />
            </div>

            {data.recentClicks.length === 0 ? (
              <EmptyState
                title="No clicks yet"
                description="Redirect analytics will appear here after visitors use this short link."
              />
            ) : (
              <div className="analytics-activity-list">
                {data.recentClicks.map((click, index) => (
                  <div className="analytics-activity-item" key={`${click.clickedAtUtc}-${index}`}>
                    <div>
                      <span className="activity-time">{formatDateTime(click.clickedAtUtc)}</span>
                      <strong>{click.userAgent || "Unknown user agent"}</strong>
                    </div>
                    <dl>
                      <div>
                        <dt>Referrer</dt>
                        <dd>{click.referrer || "Direct or unavailable"}</dd>
                      </div>
                      <div>
                        <dt>Device</dt>
                        <dd>{click.device || "Unavailable"}</dd>
                      </div>
                      <div>
                        <dt>Browser</dt>
                        <dd>{click.browser || "Unavailable"}</dd>
                      </div>
                      <div>
                        <dt>Operating system</dt>
                        <dd>{click.operatingSystem || "Unavailable"}</dd>
                      </div>
                      <div>
                        <dt>Country</dt>
                        <dd>{click.countryCode || "Unavailable"}</dd>
                      </div>
                      <div>
                        <dt>Remote IP</dt>
                        <dd>{click.remoteIpAddress || "Unavailable"}</dd>
                      </div>
                    </dl>
                  </div>
                ))}
              </div>
            )}
          </div>
        ) : null}
      </div>
    </div>
  );
}
