"use client";

import { PersonaEditor } from "@/features/persona";
import { PageHeader } from "@/shared/ui/page-header";
import { useTomeFilter } from "@/widgets/app-shell/tome-context";

export function PersonaPage() {
  const { tome } = useTomeFilter();

  return (
    <>
      <PageHeader
        title="Persona"
        description="Edit the persona document agents read through memory_persona_get. Saved as one whole document."
      />
      <PersonaEditor key={tome ?? "own"} tome={tome} />
    </>
  );
}
