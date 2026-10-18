import type { Metadata } from "next";
import { PersonaPage } from "@/views/persona/persona-page";

export const metadata: Metadata = { title: "Persona" };

export default function PersonaRoute() {
  return <PersonaPage />;
}
