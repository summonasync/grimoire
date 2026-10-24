"use client";

import { TomeCardMenu } from "@/features/shelf";
import type { TomeSummary } from "@/features/shelf";
import { cn } from "@/shared/lib/cn";
import { absoluteTime, compactNumber, relativeTime } from "@/shared/ui/format";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";

interface TomeCardProps {
  summary: TomeSummary;
  tomes: TomeSummary[];
  picked: boolean;
  onPick: (tome: string) => void;
  onGone: () => void;
}

export function TomeCard({ summary, tomes, picked, onPick, onGone }: TomeCardProps) {
  return (
    <div className="relative">
      <a
        href="#memories"
        onClick={() => onPick(summary.key)}
        className={cn(
          "panel flex h-full min-w-0 flex-col gap-3 p-4 transition-colors hover:border-accent",
          picked && "border-accent/60 ring-1 ring-accent/40",
        )}
      >
        <div className="flex min-w-0 items-center gap-2.5 pr-8">
          <Sigil seed={summary.key} className="size-8 shrink-0" />
          <div className="min-w-0">
            <p className="truncate font-display text-base leading-tight font-semibold">
              {summary.key}
            </p>
            <p
              className="mt-0.5 text-[0.7rem] text-muted"
              title={absoluteTime(summary.lastWriteAt)}
            >
              wrote {relativeTime(summary.lastWriteAt)}
            </p>
          </div>
          {summary.own && (
            <span className="ml-auto shrink-0 rounded-md border border-accent/45 bg-accent-soft px-2 py-0.5 text-[0.7rem] font-medium text-accent">
              your tome
            </span>
          )}
        </div>
        <div className="mt-auto flex items-center gap-3 border-t border-border pt-2.5 text-xs">
          <span className="flex items-center gap-1.5 tabular-nums">
            <Icon name="memories" className="size-3.5 text-jade" />
            {compactNumber(summary.active)} active
          </span>
          <span className="flex items-center gap-1.5 tabular-nums text-muted">
            <Icon name="tombstone" className="size-3.5" />
            {compactNumber(summary.tombstoned)} tombstoned
          </span>
          <span className="ml-auto flex shrink-0 items-center gap-1 text-accent">
            Memories
            <Icon name="arrowRight" className="size-3.5" />
          </span>
        </div>
      </a>
      <TomeCardMenu summary={summary} tomes={tomes} onGone={onGone} />
    </div>
  );
}
