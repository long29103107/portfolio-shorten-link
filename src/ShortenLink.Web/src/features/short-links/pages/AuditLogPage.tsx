import { type FormEvent, useEffect, useState } from "react";
import { createRecoveryNotice, type RecoveryNotice } from "../../../shared/api/recovery";
import { EmptyState } from "../../../shared/components/EmptyState";
import { Badge } from "../../../shared/components/ui/badge";
import { Button } from "../../../shared/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle
} from "../../../shared/components/ui/card";
import { Input } from "../../../shared/components/ui/input";
import {
  emptyAuditLogFilters,
  formatAuditLabel,
  mergeAuditLogEvents,
  toAuditFilterIso,
  validateAuditTimeRange
} from "../auditDiscovery";
import { listAuditLogEvents } from "../api/shortLinksApi";
import {
  formatDateTime,
  type AuditLogEvent,
  type AuditLogFilters
} from "../types";

type AuditFilterDraft = {
  action: string;
  targetId: string;
  actorId: string;
  fromLocal: string;
  toLocal: string;
};

const emptyDraft: AuditFilterDraft = {
  action: "",
  targetId: "",
  actorId: "",
  fromLocal: "",
  toLocal: ""
};

export function AuditLogPage() {
  const [draft, setDraft] = useState<AuditFilterDraft>(emptyDraft);
  const [filters, setFilters] = useState<AuditLogFilters>(emptyAuditLogFilters);
  const [events, setEvents] = useState<AuditLogEvent[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [failure, setFailure] = useState<RecoveryNotice | null>(null);
  const [rangeError, setRangeError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingOlder, setIsLoadingOlder] = useState(false);
  const [reloadVersion, setReloadVersion] = useState(0);

  useEffect(() => {
    let isCurrent = true;
    setIsLoading(true);
    setFailure(null);
    setEvents([]);
    setNextCursor(null);

    void listAuditLogEvents({ limit: 50, filters })
      .then((page) => {
        if (!isCurrent) return;
        setEvents(page.items);
        setNextCursor(page.nextCursor);
      })
      .catch((error) => {
        if (!isCurrent) return;
        setFailure(createRecoveryNotice(
          error,
          error instanceof Error ? error.message : "Audit events could not be loaded."
        ));
      })
      .finally(() => {
        if (isCurrent) setIsLoading(false);
      });

    return () => {
      isCurrent = false;
    };
  }, [filters, reloadVersion]);

  const applyFilters = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const validationError = validateAuditTimeRange(draft.fromLocal, draft.toLocal);
    setRangeError(validationError);
    if (validationError) return;

    setFilters({
      action: draft.action.trim(),
      targetId: draft.targetId.trim(),
      actorId: draft.actorId.trim(),
      from: toAuditFilterIso(draft.fromLocal),
      to: toAuditFilterIso(draft.toLocal)
    });
  };

  const clearFilters = () => {
    setDraft(emptyDraft);
    setRangeError(null);
    setFilters({ ...emptyAuditLogFilters });
  };

  const loadOlder = async () => {
    if (!nextCursor || isLoadingOlder) return;

    setIsLoadingOlder(true);
    setFailure(null);
    try {
      const page = await listAuditLogEvents({
        limit: 50,
        cursor: nextCursor,
        filters
      });
      setEvents((current) => mergeAuditLogEvents(current, page.items));
      setNextCursor(page.nextCursor);
    } catch (error) {
      setFailure(createRecoveryNotice(
        error,
        error instanceof Error ? error.message : "Older audit events could not be loaded."
      ));
    } finally {
      setIsLoadingOlder(false);
    }
  };

  const retry = () => {
    if (events.length > 0 && nextCursor) {
      void loadOlder();
      return;
    }
    setReloadVersion((version) => version + 1);
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
        <form className="audit-filter-panel" onSubmit={applyFilters}>
          <div className="audit-filter-grid">
            <label>
              <span>Action</span>
              <Input
                aria-label="Filter by action"
                placeholder="short_link.updated"
                value={draft.action}
                onChange={(event) => setDraft((current) => ({
                  ...current,
                  action: event.target.value
                }))}
              />
            </label>
            <label>
              <span>Target ID</span>
              <Input
                aria-label="Filter by target ID"
                placeholder="Code, user, role, or API key ID"
                value={draft.targetId}
                onChange={(event) => setDraft((current) => ({
                  ...current,
                  targetId: event.target.value
                }))}
              />
            </label>
            <label>
              <span>Actor ID</span>
              <Input
                aria-label="Filter by actor ID"
                placeholder="user-1"
                value={draft.actorId}
                onChange={(event) => setDraft((current) => ({
                  ...current,
                  actorId: event.target.value
                }))}
              />
            </label>
            <label>
              <span>From</span>
              <Input
                aria-label="Filter from time"
                type="datetime-local"
                value={draft.fromLocal}
                onChange={(event) => setDraft((current) => ({
                  ...current,
                  fromLocal: event.target.value
                }))}
              />
            </label>
            <label>
              <span>To</span>
              <Input
                aria-label="Filter to time"
                type="datetime-local"
                value={draft.toLocal}
                onChange={(event) => setDraft((current) => ({
                  ...current,
                  toLocal: event.target.value
                }))}
              />
            </label>
          </div>
          {rangeError ? <p className="field-error" role="alert">{rangeError}</p> : null}
          <div className="audit-filter-actions">
            <Button type="submit" disabled={isLoading}>Apply filters</Button>
            <Button type="button" variant="secondary" onClick={clearFilters} disabled={isLoading}>
              Clear
            </Button>
          </div>
        </form>

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
            description="Try a wider time range or clear one of the investigation filters."
            action={<Button variant="secondary" onClick={clearFilters}>Clear filters</Button>}
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
              <span>{events.length} event{events.length === 1 ? "" : "s"} loaded</span>
              {nextCursor ? (
                <Button
                  variant="secondary"
                  onClick={() => void loadOlder()}
                  disabled={isLoadingOlder}
                >
                  {isLoadingOlder ? "Loading..." : "Load older events"}
                </Button>
              ) : (
                <span>End of results</span>
              )}
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
