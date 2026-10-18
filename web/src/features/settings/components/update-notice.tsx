import type { SettingsUpdateOutcome } from "../types";

interface UpdateNoticeProps {
  outcome: SettingsUpdateOutcome;
  hint: string | null;
}

export function UpdateNotice({ outcome, hint }: UpdateNoticeProps) {
  if (outcome.status === "applied") {
    return (
      <div role="status" className="rounded-md border border-emerald-500/40 bg-emerald-500/10 px-4 py-3 text-sm">
        <p className="font-medium text-emerald-500">The settings update was accepted.</p>
      </div>
    );
  }

  if (outcome.status === "deferred") {
    return (
      <div role="status" className="rounded-md border border-amber-500/40 bg-amber-500/10 px-4 py-3 text-sm">
        <p className="font-medium text-amber-500">Not saved — settings persistence is not implemented yet.</p>
        <p className="mt-1 text-muted">
          {outcome.code}: {outcome.message}
        </p>
        {hint !== null && <p className="mt-2 text-xs text-muted">How to change settings instead: {hint}</p>}
      </div>
    );
  }

  return (
    <div role="alert" className="rounded-md border border-red-500/40 bg-red-500/10 px-4 py-3 text-sm">
      <p className="font-medium">The settings update failed.</p>
      <p className="mt-0.5 text-muted">
        {outcome.code}: {outcome.message}
      </p>
    </div>
  );
}
