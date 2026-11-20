import type { SettingsUpdateOutcome } from "../types";
import { Icon } from "@/shared/ui/icons";

interface UpdateNoticeProps {
  outcome: SettingsUpdateOutcome;
  hint: string | null;
}

export function UpdateNotice({ outcome, hint }: UpdateNoticeProps) {
  if (outcome.status === "applied") {
    return (
      <div
        role="status"
        className="flex items-start gap-2.5 rounded-lg border border-jade/45 bg-jade/10 px-4 py-3"
      >
        <Icon name="check" className="mt-0.5 size-4 shrink-0 text-jade" />
        <p className="text-sm text-jade">The settings update was accepted.</p>
      </div>
    );
  }

  if (outcome.status === "deferred") {
    return (
      <div
        role="status"
        className="flex items-start gap-2.5 rounded-lg border border-gilt/45 bg-gilt/10 px-4 py-3"
      >
        <Icon name="alert" className="mt-0.5 size-4 shrink-0 text-gilt" />
        <div className="min-w-0">
          <p className="text-sm font-medium text-gilt">
            Not saved — the engine refuses this write (HTTP 501).
          </p>
          <p className="mt-0.5 text-xs break-words text-muted">
            {outcome.code}: {outcome.message}
          </p>
          {hint !== null && !outcome.message.includes(hint) && (
            <p className="mt-1.5 text-xs text-muted">How to change settings instead: {hint}</p>
          )}
        </div>
      </div>
    );
  }

  return (
    <div
      role="alert"
      className="flex items-start gap-2.5 rounded-lg border border-ember/45 bg-ember/10 px-4 py-3"
    >
      <Icon name="alert" className="mt-0.5 size-4 shrink-0 text-ember" />
      <div className="min-w-0">
        <p className="text-sm font-medium text-ember">The settings update failed.</p>
        <p className="mt-0.5 text-xs break-words text-muted">
          {outcome.code}: {outcome.message}
        </p>
      </div>
    </div>
  );
}
