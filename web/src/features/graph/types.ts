import type { MemoryKind, MemoryStatus } from "@/shared/api/types";

export type RecordEdgeClass = "supersede" | "link" | "similarity" | "coMembership";

export interface GraphNode {
  id: string;
  snippet: string;
  kind: MemoryKind;
  project: string | null;
}

export interface GraphEdge {
  from: string;
  to: string;
  class: RecordEdgeClass;
  targetStatus: MemoryStatus;
}

export interface TomeGraph {
  tome: string;
  nodes: GraphNode[];
  edges: GraphEdge[];
}
