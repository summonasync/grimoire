import { PageHeader } from "@/shared/ui/page-header";
import { PagePlaceholder } from "@/shared/ui/page-placeholder";

export function PersonaPage() {
  return (
    <>
      <PageHeader
        title="Persona"
        description="Edit the persona document agents read through memory_persona_get."
      />
      <PagePlaceholder>The persona editor is not wired up yet.</PagePlaceholder>
    </>
  );
}
