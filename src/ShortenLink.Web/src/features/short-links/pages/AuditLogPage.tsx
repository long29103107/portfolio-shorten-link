import { useState } from "react";
import { EmptyState } from "@/shared/components/EmptyState";
import { Badge } from "@/shared/components/ui/badge";
import { Button } from "@/shared/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle
} from "@/shared/components/ui/card";
import { Input } from "@/shared/components/ui/input";
import { DiscoverySelect } from "@/shared/components/DiscoverySelect";
import {
  emptyAuditLogFilters,
  formatAuditLabel,
  getAuditTimeRange,
  toAuditFilterIso,
  validateAuditTimeRange,
  type AuditTimePreset
} from "../domain/auditDiscovery";
import {
  formatDateTime,
  type AuditLogEvent,
  type AuditLogFilters
} from "../types";
import { useAuditLogData } from "../hooks/useAuditLogData";
import { useDebouncedCallback } from "@/shared/hooks/useDebouncedCallback";

type AuditFilterDraft = {
  action: string;
  search: string;
  timePreset: AuditTimePreset;
  fromLocal: string;
  toLocal: string;
};

const emptyDraft: AuditFilterDraft = {
  action: "",
  search: "",
  timePreset: "today",
  fromLocal: "",
  toLocal: ""
};

const initialFilters: AuditLogFilters = {
  ...emptyAuditLogFilters,
  ...getAuditTimeRange("today")
};

export function AuditLogPage() {
  const [draft, setDraft] = useState<AuditFilterDraft>(emptyDraft);
  const [filters, setFilters] = useState<AuditLogFilters>(initialFilters);
  const [rangeError, setRangeError] = useState<string | null>(null);
  const {
    actions,
    events,
    nextCursor,
    hasPreviousPage,
    pageNumber,
    failure,
    isLoading,
    isLoadingOlder,
    loadNext,
    loadPrevious,
    retry
  } = useAuditLogData(filters);

  const applyFilters = (nextDraft: AuditFilterDraft) => {
    const customRange = {
      from: toAuditFilterIso(nextDraft.fromLocal),
      to: toAuditFilterIso(nextDraft.toLocal)
    };
    const range = nextDraft.timePreset === "custom"
      ? customRange
      : getAuditTimeRange(nextDraft.timePreset);
    const validationError = nextDraft.timePreset === "custom"
      ? validateAuditTimeRange(nextDraft.fromLocal, nextDraft.toLocal)
      : null;
    setRangeError(validationError);
    if (validationError) return;

    setFilters({
      action: nextDraft.action.trim(),
      search: nextDraft.search.trim(),
      ...range
    });
  };

  const debouncedApplyFilters = useDebouncedCallback(applyFilters, 350);
  const updateDraft = (change: Partial<AuditFilterDraft>) => {
    const nextDraft = { ...draft, ...change };
    setDraft(nextDraft);
    debouncedApplyFilters.invoke(nextDraft);
  };

  return (
    <Card className="audit-log-panel">
      <CardHeader className="audit-log-heading">
        <div>
          <p className="eyebrow">Investigation</p>
          <CardTitle>Audit logs</CardTitle>
          <p className="page-description">
            Review durable mutation history. Results are scoped by the server to your current access.
          </p>
        </div>
        <Badge variant="secondary">Newest first</Badge>
      </CardHeader>

      <CardContent>
        <div className="audit-filter-panel">
          <div className="audit-filter-grid">
            <DiscoverySelect
              label="Action"
              value={draft.action}
              onChange={(action) => updateDraft({ action })}
            >
              <option value="">All actions</option>
              {actions.map((action) => <option key={action} value={action}>{formatAuditLabel(action)}</option>)}
            </DiscoverySelect>
            <label className="audit-filter-search">
              <span>Search</span>
              <Input
                aria-label="Search audit logs"
                placeholder="Action, actor, target, or outcome"
                value={draft.search}
                onChange={(event) => updateDraft({ search: event.target.value })}
              />
            </label>
            <label>
              <span>Time range</span>
              <select
                aria-label="Audit log time range"
                value={draft.timePreset}
                onChange={(event) => updateDraft({ timePreset: event.target.value as AuditTimePreset })}
              >
                <option value="today">Today</option>
                <option value="week">Last 7 days</option>
                <option value="month">Last 30 days</option>
                <option value="custom">Custom</option>
              </select>
            </label>
            {draft.timePreset === "custom" ? (
              <>
                <label>
                  <span>From</span>
                  <Input
                    aria-label="Filter from time"
                    type="datetime-local"
                    value={draft.fromLocal}
                    onChange={(event) => updateDraft({ fromLocal: event.target.value })}
                  />
                </label>
                <label>
                  <span>To</span>
                  <Input
                    aria-label="Filter to time"
                    type="datetime-local"
                    value={draft.toLocal}
                    onChange={(event) => updateDraft({ toLocal: event.target.value })}
                  />
                </label>
              </>
            ) : null}
          </div>
          {rangeError ? <p className="field-error" role="alert">{rangeError}</p> : null}
          <div className="audit-filter-actions">
            <span className="audit-filter-hint">Filters update automatically</span>
          </div>
        </div>

        {isLoading ? (
          <div className="audit-loading" role="status">Loading audit events...</div>
        ) : null}

        {!isLoading && failure && events.length === 0 ? (
          <EmptyState
            title="Audit logs are unavailable"
            description={failure.message}
            action={failure.retryable
              ? <Button variant="secondary" onClick={retry}>Retry</Button>
              : undefined}
          />
        ) : null}

        {!isLoading && !failure && events.length === 0 ? (
          <EmptyState
            title="No matching audit events"
            description="Try a different search, action, or time range."
          />
        ) : null}

        {events.length > 0 ? (
          <>
            <div className="audit-table-wrap">
              <table className="audit-table">
                <thead>
                  <tr>
                    <th>Occurred</th>
                    <th>Actor</th>
                    <th>Action</th>
                    <th>Target</th>
                    <th>Context</th>
                    <th>Outcome</th>
                  </tr>
                </thead>
                <tbody>
                  {events.map((auditEvent) => (
                    <AuditEventRow key={auditEvent.id} auditEvent={auditEvent} />
                  ))}
                </tbody>
              </table>
            </div>

            {failure ? (
              <div className="audit-inline-failure" role="alert">
                <span>{failure.message}</span>
                {failure.retryable
                  ? <Button variant="secondary" onClick={retry}>Retry</Button>
                  : null}
              </div>
            ) : null}

            <div className="audit-pagination" role="status">
              <span>Page {pageNumber} · {events.length} event{events.length === 1 ? "" : "s"}</span>
              <div className="audit-pagination-actions">
                <Button
                  variant="secondary"
                  onClick={() => void loadPrevious()}
                  disabled={!hasPreviousPage || isLoadingOlder}
                >
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => void loadNext()}
                  disabled={!nextCursor || isLoadingOlder}
                >
                  {isLoadingOlder ? "Loading..." : "Next"}
                </Button>
              </div>
            </div>
          </>
        ) : null}
      </CardContent>
    </Card>
  );
}

function AuditEventRow({ auditEvent }: { auditEvent: AuditLogEvent }) {
  return (
    <tr>
      <td data-label="Occurred">
        <time dateTime={auditEvent.occurredAtUtc}>
          {formatDateTime(auditEvent.occurredAtUtc)}
        </time>
      </td>
      <td data-label="Actor"><code>{auditEvent.actorId}</code></td>
      <td data-label="Action">
        <strong>{formatAuditLabel(auditEvent.action)}</strong>
        <code>{auditEvent.action}</code>
      </td>
      <td data-label="Target">
        <span>{formatAuditLabel(auditEvent.targetType)}</span>
        <code>{auditEvent.targetId}</code>
      </td>
      <td data-label="Context">
        {auditEvent.subjectUserId || auditEvent.detail ? (
          <dl className="audit-context">
            {auditEvent.subjectUserId ? (
              <div>
                <dt>Subject</dt>
                <dd>{auditEvent.subjectUserId}</dd>
              </div>
            ) : null}
            {auditEvent.detail ? (
              <div>
                <dt>Detail</dt>
                <dd>{auditEvent.detail}</dd>
              </div>
            ) : null}
          </dl>
        ) : <span className="muted-copy">—</span>}
      </td>
      <td data-label="Outcome">
        <Badge variant={auditEvent.outcome === "succeeded" ? "default" : "destructive"}>
          {formatAuditLabel(auditEvent.outcome)}
        </Badge>
      </td>
    </tr>
  );
}
