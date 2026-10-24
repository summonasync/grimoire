import type { MemoryKind } from "@/shared/api/types";
import type { RecordEdgeClass } from "../types";

export interface EdgeClassStyle {
  code: string;
  label: string;
  colorVar: string;
  width: number;
  dashed: boolean;
}

export const edgeClassOrder: RecordEdgeClass[] = [
  "supersede",
  "link",
  "similarity",
  "coMembership",
];

export const edgeClassStyles: Record<RecordEdgeClass, EdgeClassStyle> = {
  supersede: { code: "a", label: "supersede", colorVar: "--accent", width: 2, dashed: false },
  link: { code: "b", label: "link", colorVar: "--jade", width: 1.6, dashed: false },
  similarity: { code: "c", label: "similar", colorVar: "--gilt", width: 1.2, dashed: false },
  coMembership: { code: "d", label: "co-membership", colorVar: "--ember", width: 1, dashed: true },
};

export const kindColorVars: Record<MemoryKind, string> = {
  journal: "--jade",
  fact: "--gilt",
};
