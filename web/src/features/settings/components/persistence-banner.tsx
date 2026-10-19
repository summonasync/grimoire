import type { SettingsPersistence } from "@/shared/api/types";
import { Icon } from "@/shared/ui/icons";

interface PersistenceBannerProps {
  persistence: SettingsPersistence;
}

export function PersistenceBanner({ persistence }: PersistenceBannerProps) {
  if (persistence.available) {
    return null;
  }

  return (
    <div className="flex items-start gap-3 rounded-lg border border-gilt/45 bg-gilt/10 px-4 py-3">
      <Icon name="alert" className="mt-0.5 size-4 shrink-0 text-gilt" />
      <div className="min-w-0">
        <p className="text-sm font-medium text-gilt">Settings are view-only in this build</p>
        <p className="mt-0.5 text-xs text-muted">{persistence.reason}</p>
        <p className="mt-1 text-xs text-muted">
          Edit the config file by hand — {persistence.hint}
        </p>
      </div>
    </div>
  );
}
