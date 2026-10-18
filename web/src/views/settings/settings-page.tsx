import { SettingsTuning } from "@/features/settings";
import { PageHeader } from "@/shared/ui/page-header";

export function SettingsPage() {
  return (
    <>
      <PageHeader
        title="Settings"
        description="Engine and serve configuration as reported by the API. Edits are sent to PUT /api/settings and honestly report the v1 deferral."
      />
      <SettingsTuning />
    </>
  );
}
