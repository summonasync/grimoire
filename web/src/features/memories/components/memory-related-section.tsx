"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import type { MemoryRecord, MemoryStatus } from "@/shared/api/types";
import { StatusBadge } from "@/shared/ui/badge";
import { Icon } from "@/shared/ui/icons";
import { shortId } from "@/shared/ui/format";
import { cn } from "@/shared/lib/cn";
import { relatedMemoryQueryOptions } from "../queries";
import type { GraphDirection, RelatedEdgeClass } from "../types";

interface MemoryRelatedSectionProps {
  record: MemoryRecord;
  onOpen: (id: string) => void;
}

const classGroupLabels: Record<RelatedEdgeClass, string> = {
  supersede: "Supersede chains",
  link: "Links",
  similarity: "Similar",
  coMembership: "Co-membership",
};

const classOrder: RelatedEdgeClass[] = ["supersede", "link", "similarity", "coMembership"];

function NeighborRow({
  direction,
  status,
  targetId,
  snippet,
  onOpen,
}: {
  direction: GraphDirection;
  status: MemoryStatus;
  targetId: string;
  snippet: string;
  onOpen: (id: string) => void;
}) {
  return (
    <li>
      <button
        type="button"
        onClick={() => onOpen(targetId)}
        className="w-full rounded-lg border border-border bg-background/50 p-2.5 text-left transition-colors hover:border-jade/50"
      >
        <div className="flex flex-wrap items-center gap-2">
          <span
            title={direction === "outbound" ? "Outbound edge" : "Inbound edge"}
            className="flex text-muted"
          >
            <Icon
              name={direction === "outbound" ? "arrowRight" : "arrowLeft"}
              className="size-3.5"
            />
          </span>
          {status !== "active" && <StatusBadge status={status} />}
          <span className="ml-auto font-mono text-[0.65rem] text-muted">{shortId(targetId)}</span>
        </div>
        <p className="mt-1.5 line-clamp-2 text-xs break-words text-foreground/90">{snippet}</p>
      </button>
    </li>
  );
}

export function MemoryRelatedSection({ record, onOpen }: MemoryRelatedSectionProps) {
  const [includeCoMembership, setIncludeCoMembership] = useState(false);
  const related = useQuery(relatedMemoryQueryOptions(record.id, includeCoMembership));
  const edges = related.data?.edges ?? [];

  return (
    <section className="mt-4 border-t border-border pt-3">
      <div className="mb-2 flex items-center gap-1.5">
        <h3 className="label flex items-center gap-1.5">
          <Icon name="link" className="size-3.5" />
          Related ({edges.length})
        </h3>
        <button
          type="button"
          aria-pressed={includeCoMembership}
          onClick={() => setIncludeCoMembership((current) => !current)}
          className={cn(
            "ml-auto rounded-md border px-2 py-0.5 text-[0.7rem] font-medium transition-colors",
            includeCoMembership
              ? "border-jade/55 bg-jade/10 text-foreground"
              : "border-border text-muted hover:border-jade/40 hover:text-foreground",
          )}
        >
          co-membership
        </button>
      </div>

      {related.isPending ? (
        <p className="text-xs text-muted">Finding neighbors…</p>
      ) : related.error ? (
        <p className="text-xs text-ember">{related.error.message}</p>
      ) : edges.length === 0 ? (
        <p className="text-xs text-muted">
          No related records — nothing supersedes, links or matches this one yet.
        </p>
      ) : (
        <div className="space-y-3">
          {classOrder.map((edgeClass) => {
            const group = edges.filter((edge) => edge.class === edgeClass);
            if (group.length === 0) {
              return null;
            }
            return (
              <div key={edgeClass}>
                <p className="label mb-1.5 text-muted">
                  {classGroupLabels[edgeClass]} ({group.length})
                </p>
                <ul className="space-y-1.5">
                  {group.map((edge) => (
                    <NeighborRow
                      key={`${edge.direction}-${edge.target.id}`}
                      direction={edge.direction}
                      status={edge.target.status}
                      targetId={edge.target.id}
                      snippet={edge.target.snippet}
                      onOpen={onOpen}
                    />
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}
