import { PageHeader } from "@/shared/ui/page-header";
import { PagePlaceholder } from "@/shared/ui/page-placeholder";

export function SettingsPage() {
  return (
    <>
      <PageHeader
        title="Settings"
        description="Engine and serve configuration as reported by the API."
      />
      <PagePlaceholder>
        The settings view is not wired up yet. Until the API accepts updates, this surface
        will stay read-only and point at the config file.
      </PagePlaceholder>
    </>
  );
}
