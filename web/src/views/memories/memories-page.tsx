import { PageHeader } from "@/shared/ui/page-header";
import { PagePlaceholder } from "@/shared/ui/page-placeholder";

export function MemoriesPage() {
  return (
    <>
      <PageHeader
        title="Memories"
        description="Search, inspect, edit and tombstone the memory store."
      />
      <PagePlaceholder>The memory browser is not wired up yet.</PagePlaceholder>
    </>
  );
}
