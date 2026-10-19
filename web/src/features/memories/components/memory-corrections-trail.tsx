import type { CorrectionEntry } from "@/shared/api/types";
import { ActionBadge } from "@/shared/ui/badge";
import { absoluteTime, actorLabel, relativeTime } from "@/shared/ui/format";
import { Icon } from "@/shared/ui/icons";

function CorrectionRow({ entry }: { entry: CorrectionEntry }) {
  return (
    <li className="relative space-y-1 pl-5">
      <span
        aria-hidden
        className="absolute top-1.5 left-0 size-2 rounded-full border border-border-strong bg-surface"
      />
      <div className="flex flex-wrap items-center gap-2">
        <ActionBadge action={entry.action} />
        <span className="text-[0.7rem] text-muted" title={absoluteTime(entry.at)}>
          {relativeTime(entry.at)} · {actorLabel(entry.actor.alias, entry.actor.vendor)}
        </span>
      </div>
      <p className="text-xs break-words text-muted">{entry.reason}</p>
    </li>
  );
}

interface MemoryCorrectionsTrailProps {
  corrections: CorrectionEntry[];
}

export function MemoryCorrectionsTrail({ corrections }: MemoryCorrectionsTrailProps) {
  return (
    <section className="mt-4 border-t border-border pt-3">
      <h3 className="label mb-2 flex items-center gap-1.5">
        <Icon name="clock" className="size-3.5" />
        Corrections ({corrections.length})
      </h3>
      {corrections.length === 0 ? (
        <p className="text-xs text-muted">
          No corrections yet — this record has never been patched, superseded or restored.
        </p>
      ) : (
        <ul className="space-y-3 border-l border-border pl-0">
          {corrections.map((entry, index) => (
            <CorrectionRow key={`${entry.at}-${index}`} entry={entry} />
          ))}
        </ul>
      )}
    </section>
  );
}
