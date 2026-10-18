import { PageHeader } from "@/shared/ui/page-header";
import { PagePlaceholder } from "@/shared/ui/page-placeholder";

export function StatsPage() {
  return (
    <>
      <PageHeader
        title="Stats"
        description="Store counts, scope distribution and pending compaction."
      />
      <PagePlaceholder>The stats dashboard is not wired up yet.</PagePlaceholder>
    </>
  );
}
