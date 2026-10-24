"use client";

import type { TomeSummary } from "../types";
import { Sigil } from "@/shared/ui/sigil";
import { Button } from "@/shared/ui/button";

interface TomeMergePickerProps {
  summary: TomeSummary;
  tomes: TomeSummary[];
  pending: boolean;
  error: string | null;
  onMerge: (into: string) => void;
  onCancel: () => void;
}

export function TomeMergePicker({
  summary,
  tomes,
  pending,
  error,
  onMerge,
  onCancel,
}: TomeMergePickerProps) {
  const targets = tomes.filter((tome) => tome.key !== summary.key);

  return (
    <div className="space-y-2">
      <p className="text-xs leading-snug text-muted">
        Moves all {summary.active} active records of{" "}
        <span className="font-medium text-foreground">{summary.key}</span> into the tome you pick.
        The source tome drops out of the census once nothing active is left.
      </p>
      {error && <p className="text-xs text-ember">{error}</p>}
      {targets.length === 0 ? (
        <p className="text-xs text-muted">
          No other tome holds records — merge needs an existing target. Rename instead.
        </p>
      ) : (
        <ul className="max-h-40 space-y-0.5 overflow-y-auto">
          {targets.map((tome) => (
            <li key={tome.key}>
              <button
                type="button"
                disabled={pending}
                onClick={() => onMerge(tome.key)}
                className="flex w-full cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-left text-sm transition-colors hover:bg-foreground/5 disabled:cursor-wait disabled:opacity-50"
              >
                <Sigil seed={tome.key} className="size-4 shrink-0" />
                <span className="min-w-0 flex-1 truncate font-mono">{tome.key}</span>
                <span className="shrink-0 text-[0.7rem] tabular-nums text-muted">
                  {tome.active}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="flex justify-end">
        <Button variant="ghost" size="sm" onClick={onCancel}>
          Cancel
        </Button>
      </div>
    </div>
  );
}
