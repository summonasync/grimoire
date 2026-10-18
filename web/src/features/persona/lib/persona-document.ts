import type { PersonaDocument } from "@/shared/api/types";
import type { PersonaSectionSpec } from "../types";

export const personaSectionLimit = 8000;

export const personaSections: PersonaSectionSpec[] = [
  {
    key: "identity",
    label: "Identity",
    hint: "Who the agent is and whose memory it serves.",
    placeholder: "You are Grimoire, the memory layer for my coding agents.",
    rows: 5,
  },
  {
    key: "tone",
    label: "Tone",
    hint: "Voice and register the agent should use in replies.",
    placeholder: "Direct and informal, technical terms stay in English.",
    rows: 4,
  },
  {
    key: "preferences",
    label: "Preferences",
    hint: "Standing preferences the agent should honor by default.",
    placeholder: "Prefer diff-first answers. Ask before adding dependencies.",
    rows: 4,
  },
  {
    key: "rules",
    label: "Rules",
    hint: "Hard rules that must never be broken.",
    placeholder: "Never commit without an explicit ask. Never print secrets.",
    rows: 6,
  },
];

export function documentsEqual(left: PersonaDocument, right: PersonaDocument): boolean {
  return left.identity === right.identity
    && left.tone === right.tone
    && left.preferences === right.preferences
    && left.rules === right.rules;
}
