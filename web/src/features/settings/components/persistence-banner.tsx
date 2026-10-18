import type { SettingsPersistence } from "@/shared/api/types";

interface PersistenceBannerProps {
  persistence: SettingsPersistence;
}

export function PersistenceBanner({ persistence }: PersistenceBannerProps) {
  if (persistence.available) {
    return null;
  }

  return (
    <div className="rounded-md border border-border bg-surface px-4 py-3 text-sm">
      <p className="font-medium">Settings are view-only in this build.</p>
      <p className="mt-1 text-muted">{persistence.reason}</p>
    </div>
  );
}
