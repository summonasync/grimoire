import type { CorrectionAction, CorrectionEntry } from "@/shared/api/types";
import { cn } from "@/shared/lib/cn";

const actionStyles: Record<CorrectionAction, string> = {
  patch: "border-accent/50 text-accent",
  restore: "border-emerald-600/50 text-emerald-500",
  supersede: "border-border text-muted",
  forget: "border-red-700/60 text-red-400",
};

function CorrectionRow({ entry }: { entry: CorrectionEntry }) {
  return (
    <li className="space-y-1 border-l border-border pl-3">
      <div className="flex items-center gap-2">
        <span
          className={cn(
            "inline-flex shrink-0 items-center rounded-full border px-2 py-0.5 text-xs font-medium",
            actionStyles[entry.action],
          )}
        >
          {entry.action}
        </span>
        <span className="text-xs text-muted">
          {new Date(entry.at).toLocaleString()}
          {" · "}
          {entry.actor.alias}
          {entry.actor.vendor ? ` · ${entry.actor.vendor}` : ""}
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
    <div className="mt-4">
      <h3 className="text-xs font-medium text-muted">Corrections</h3>
      {corrections.length === 0 ? (
        <p className="mt-1 text-xs text-muted">No corrections yet.</p>
      ) : (
        <ul className="mt-2 space-y-3">
          {corrections.map((entry, index) => (
            <CorrectionRow key={`${entry.at}-${index}`} entry={entry} />
          ))}
        </ul>
      )}
    </div>
  );
}
