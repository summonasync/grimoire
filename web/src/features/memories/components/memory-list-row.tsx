"use client";

import type { MemoryRecord } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";
import { KindBadge, ScopeBadge, StatusBadge } from "@/shared/ui/badge";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";
import { importanceScore, relativeTime } from "@/shared/ui/format";

interface MemoryListRowProps {
  record: MemoryRecord;
  selected: boolean;
  onOpen: (id: string) => void;
}

export function MemoryListRow({ record, selected, onOpen }: MemoryListRowProps) {
  return (
    <tr
      onClick={() => onOpen(record.id)}
      aria-selected={selected}
      className={cn(
        "cursor-pointer border-b border-border/60 transition-colors last:border-0",
        selected ? "bg-accent-soft" : "hover:bg-foreground/[0.03]",
      )}
    >
      <td className="max-w-md px-3 py-2.5 align-top">
        <p className="line-clamp-2 text-sm leading-snug">{record.content}</p>
        {record.tags.length > 0 && (
          <p className="mt-1 flex flex-wrap gap-1">
            {record.tags.slice(0, 4).map((tag) => (
              <span
                key={tag}
                className="rounded border border-border px-1 text-[0.65rem] text-muted"
              >
                #{tag}
              </span>
            ))}
          </p>
        )}
      </td>
      <td className="px-3 py-2.5 align-top whitespace-nowrap">
        <KindBadge kind={record.kind} />
      </td>
      <td className="px-3 py-2.5 align-top whitespace-nowrap">
        <ScopeBadge scope={record.scope} />
      </td>
      <td className="px-3 py-2.5 align-top whitespace-nowrap">
        <StatusBadge status={record.status} />
      </td>
      <td className="px-3 py-2.5 align-top">
        <span className="flex items-center gap-1.5 text-xs whitespace-nowrap text-muted">
          <Sigil seed={record.actor.alias} className="size-3.5 shrink-0" />
          {record.actor.alias}
        </span>
      </td>
      <td className="px-3 py-2.5 align-top text-xs whitespace-nowrap text-muted tabular-nums">
        {relativeTime(record.createdAt)}
      </td>
      <td className="px-3 py-2.5 align-top whitespace-nowrap">
        <span className="flex items-center gap-1 text-xs text-gilt tabular-nums">
          <Icon name="star" className="size-3.5" />
          {importanceScore(record.importance)}
        </span>
      </td>
    </tr>
  );
}
