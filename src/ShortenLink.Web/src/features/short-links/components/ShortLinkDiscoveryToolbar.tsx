import { useEffect, useState, type ReactNode } from "react";
import { Input } from "@/shared/components/ui/input";
import type { ShortLinkDiscoveryQuery } from "../types";
import { DiscoverySelect } from "@/shared/components/DiscoverySelect";
import { useDebouncedCallback } from "@/shared/hooks/useDebouncedCallback";

export const defaultShortLinkDiscoveryQuery: ShortLinkDiscoveryQuery = {
  search: "",
  status: "all",
  sortBy: "created",
  sortDirection: "desc",
  folder: "",
  tag: ""
};

export function hasShortLinkDiscoveryCriteria(query: ShortLinkDiscoveryQuery) {
  return query.search.trim() !== ""
    || query.status !== defaultShortLinkDiscoveryQuery.status
    || query.sortBy !== defaultShortLinkDiscoveryQuery.sortBy
    || query.sortDirection !== defaultShortLinkDiscoveryQuery.sortDirection
    || (query.folder ?? "").trim() !== ""
    || (query.tag ?? "").trim() !== "";
}

export function createShortLinkDiscoveryChange(query: ShortLinkDiscoveryQuery) {
  return { query, pageNumber: 1 } as const;
}

type ShortLinkDiscoveryToolbarProps = {
  value: ShortLinkDiscoveryQuery;
  disabled?: boolean;
  onChange: (value: ShortLinkDiscoveryQuery) => void;
  action?: ReactNode;
};

export function ShortLinkDiscoveryToolbar({
  value,
  disabled = false,
  onChange,
  action
}: ShortLinkDiscoveryToolbarProps) {
  const [search, setSearch] = useState(value.search);
  const debouncedSearch = useDebouncedCallback((nextSearch: string) => {
    onChange({ ...value, search: nextSearch.trim() });
  }, 400);

  useEffect(() => {
    debouncedSearch.cancel();
    setSearch(value.search);
  }, [value.search]);

  return (
    <div className="admin-discovery-toolbar" aria-label="Filter short links">
      <div className="admin-discovery-filters">
        <DiscoverySelect label="Status" value={value.status} disabled={disabled} onChange={(status) => onChange({ ...value, status })}>
          <option value="all">All</option>
          <option value="active">Active</option>
          <option value="inactive">Deactive</option>
          <option value="scheduled">Scheduled</option>
        </DiscoverySelect>

        <label className="admin-discovery-field admin-discovery-text-field">
          <span>Folder</span>
          <Input
            value={value.folder ?? ""}
            disabled={disabled}
            aria-label="Filter by folder"
            placeholder="All folders"
            onChange={(event) => onChange({ ...value, folder: event.target.value })}
          />
        </label>

        <label className="admin-discovery-field admin-discovery-text-field">
          <span>Tag</span>
          <Input
            value={value.tag ?? ""}
            disabled={disabled}
            aria-label="Filter by tag"
            placeholder="All tags"
            onChange={(event) => onChange({ ...value, tag: event.target.value })}
          />
        </label>
      </div>

      <div className="admin-discovery-tools">
        <div className="admin-discovery-search">
          <Input
            value={search}
            disabled={disabled}
            aria-label="Search code or destination"
            placeholder="Search code or destination"
            onChange={(event) => {
              setSearch(event.target.value);
              debouncedSearch.invoke(event.target.value);
            }}
          />
        </div>
        {action ? <div className="admin-discovery-action">{action}</div> : null}
      </div>
    </div>
  );
}
