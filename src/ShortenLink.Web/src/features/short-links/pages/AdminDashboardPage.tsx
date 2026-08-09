import type { DashboardSource } from "../domain/adminDashboard";
import type { DashboardSnapshot } from "../domain/adminDashboard";
import { DASHBOARD_DEFAULTS } from "../constants/defaults";
import { formatDateTime } from "../types";
import { buildRateLimitPolicyViews } from "../domain/rateLimitPresentation";
import { useAdminDashboardData } from "../hooks/useAdminDashboardData";
import { RefreshButton } from "@/shared/components/RefreshButton";
import { Badge } from "@/shared/components/ui/badge";
import { Card, CardContent } from "@/shared/components/ui/card";

const sourceLabels: Record<DashboardSource, string> = {
  shortLinks: "Short Links",
  users: "Users",
  roles: "Roles"
};

export function AdminDashboardPage() {
  const { snapshot, rateLimitActivity, rateLimitError, isLoading, loadDashboard } = useAdminDashboardData();

  const degraded = hasFailedSource(snapshot);

  return (
    <>
      <nav className="page-breadcrumb-bar" aria-label="Breadcrumb">
        <ol className="page-breadcrumb">
          <li>Shorten Link</li>
          <li aria-current="page">Dashboard</li>
        </ol>
        <RefreshButton isRefreshing={isLoading} label="Refresh dashboard data" onRefresh={loadDashboard} />
      </nav>

      <div className="dashboard-grid">
        <DashboardMetric label="Total short links" value={snapshot?.totalLinks} loading={isLoading} />
        <DashboardMetric label="Active links" value={snapshot?.activeLinks} loading={isLoading} />
        <DashboardMetric label="Deactivated links" value={snapshot?.deactivatedLinks} loading={isLoading} />
        <DashboardMetric label="Managed users" value={snapshot?.users} loading={isLoading} />
        <DashboardMetric label="Enabled users" value={snapshot?.enabledUsers} loading={isLoading} />
        <DashboardMetric label="Available roles" value={snapshot?.roles} loading={isLoading} />
        <Card className="dashboard-health-card">
          <CardContent>
            <p className="eyebrow">System health</p>
            <h2>{isLoading ? "Checking" : degraded ? "Degraded" : "Operational"}</h2>
            <p className="muted-copy">
              {degraded
                ? "Some dashboard sources are unavailable. Healthy metrics remain current."
                : "Short-link, identity, and role data are responding normally."}
            </p>
            <div className="dashboard-health-sources">
              {(Object.keys(sourceLabels) as DashboardSource[]).map((source) => {
                const failed = snapshot?.health[source] === "failed";
                return (
                  <div className="dashboard-health-source" key={source}>
                    <span>{sourceLabels[source]}</span>
                    <Badge variant={failed ? "destructive" : "secondary"}>
                      {isLoading ? "Checking" : failed ? "Unavailable" : "Available"}
                    </Badge>
                  </div>
                );
              })}
            </div>
          </CardContent>
        </Card>
        <Card className="dashboard-rate-limit-card">
          <CardContent>
            <div className="dashboard-section-heading">
              <div>
                <p className="eyebrow">Traffic controls</p>
                <h2>Rate limits</h2>
              </div>
              <Badge variant={isLoading ? "secondary" : rateLimitActivity?.enabled ? "default" : "secondary"}>
                {isLoading ? "Checking" : rateLimitActivity?.enabled ? "Enabled" : "Disabled"}
              </Badge>
            </div>
            {isLoading ? <p className="muted-copy">Loading rate-limit activity...</p> : null}
            {!isLoading && rateLimitError ? <p className="feedback feedback-error">{rateLimitError}</p> : null}
            {!isLoading && !rateLimitError && rateLimitActivity ? (
              <>
                <div className="rate-limit-policy-grid">
                  {buildRateLimitPolicyViews(rateLimitActivity).map((policy) => (
                    <div className="rate-limit-policy" key={policy.label}>
                      <strong>{policy.label}</strong>
                      <span>{policy.permitLimit} permits / {policy.window}</span>
                      <span>Queue: {policy.queueLimit}</span>
                      <span>Rejected: {policy.rejectedCount}</span>
                    </div>
                  ))}
                </div>
                {rateLimitActivity.recentRejections.length > 0 ? (
                  <div className="rate-limit-rejection-list">
                    <span className="dashboard-activity-note">Recent throttles</span>
                    {rateLimitActivity.recentRejections.slice(0, DASHBOARD_DEFAULTS.RECENT_REJECTION_LIMIT).map((rejection, index) => (
                      <div className="rate-limit-rejection" key={`${rejection.occurredAtUtc}-${rejection.policy}-${index}`}>
                        <strong>{rejection.policy}</strong>
                        <time dateTime={rejection.occurredAtUtc}>{formatDateTime(rejection.occurredAtUtc)}</time>
                      </div>
                    ))}
                  </div>
                ) : (
                  <p className="muted-copy">No throttled requests recorded in this process.</p>
                )}
              </>
            ) : null}
          </CardContent>
        </Card>
        <Card className="dashboard-activity-card">
          <CardContent>
            <div className="dashboard-section-heading">
              <div>
                <p className="eyebrow">Recent activity</p>
                <h2>Operational changes</h2>
              </div>
              <Badge variant="secondary">Creation events</Badge>
            </div>
            {isLoading ? (
              <p className="muted-copy">Loading recent activity...</p>
            ) : snapshot?.recentActivity.length ? (
              <div className="dashboard-activity-list">
                {snapshot.recentActivity.map((activity) => (
                  <div className="dashboard-activity-item" key={activity.id}>
                    <span className="dashboard-activity-marker" aria-hidden="true" />
                    <div>
                      <strong>{activity.title}</strong>
                      <span>{activity.detail}</span>
                    </div>
                    <time dateTime={activity.occurredAtUtc}>
                      {formatDateTime(activity.occurredAtUtc)}
                    </time>
                  </div>
                ))}
              </div>
            ) : (
              <p className="muted-copy">
                {degraded
                  ? "Recent activity is unavailable from the failed dashboard sources."
                  : "No recent creation activity yet."}
              </p>
            )}
            <p className="dashboard-activity-note">
              This snapshot shows creation activity from current records; it is not a durable mutation audit log.
            </p>
          </CardContent>
        </Card>
      </div>
    </>
  );
}

function DashboardMetric({ label, value, loading }: { label: string; value?: number; loading: boolean }) {
  return (
    <Card className="dashboard-metric-card">
      <CardContent>
        <span>{label}</span>
        <strong>{loading || value === undefined ? "—" : value}</strong>
      </CardContent>
    </Card>
  );
}

function hasFailedSource(snapshot: DashboardSnapshot | null) {
  return snapshot
    ? Object.values(snapshot.health).some((state) => state === "failed")
    : false;
}
