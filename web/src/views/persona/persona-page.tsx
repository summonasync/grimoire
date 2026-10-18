import { PersonaEditor } from "@/features/persona";
import { PageHeader } from "@/shared/ui/page-header";

export function PersonaPage() {
  return (
    <>
      <PageHeader
        title="Persona"
        description="Edit the persona document agents read through memory_persona_get. Saved as one whole document."
      />
      <PersonaEditor />
    </>
  );
}
