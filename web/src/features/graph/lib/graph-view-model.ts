import type { MemoryStatus } from "@/shared/api/types";
import type { GraphNode, RecordEdgeClass, TomeGraph } from "../types";

export const ALL_PROJECTS = "all";
export const NO_PROJECT = "";

export interface GraphVertex {
  id: string;
  ghost: boolean;
  ghostStatus: MemoryStatus | null;
  node: GraphNode | null;
}

export interface GraphLink {
  source: number;
  target: number;
  class: RecordEdgeClass;
}

export interface GraphModel {
  vertices: GraphVertex[];
  links: GraphLink[];
  projects: string[];
  hasUnprojected: boolean;
  ghostCount: number;
}

export function buildGraphModel(graph: TomeGraph): GraphModel {
  const vertices: GraphVertex[] = graph.nodes.map((node) => ({
    id: node.id,
    ghost: false,
    ghostStatus: null,
    node,
  }));

  const indexOf = new Map<string, number>(graph.nodes.map((node, index) => [node.id, index]));

  const vertexFor = (id: string): number => {
    const existing = indexOf.get(id);
    if (existing !== undefined) {
      return existing;
    }
    const index = vertices.length;
    indexOf.set(id, index);
    vertices.push({ id, ghost: true, ghostStatus: null, node: null });
    return index;
  };

  const links: GraphLink[] = [];
  for (const edge of graph.edges) {
    if (edge.from === edge.to) {
      continue;
    }
    const source = vertexFor(edge.from);
    const target = vertexFor(edge.to);
    const targetVertex = vertices[target]!;
    if (targetVertex.ghost && targetVertex.ghostStatus === null) {
      targetVertex.ghostStatus = edge.targetStatus;
    }
    links.push({ source, target, class: edge.class });
  }

  const projects = [
    ...new Set(
      graph.nodes
        .map((node) => node.project)
        .filter((project): project is string => project !== null),
    ),
  ].sort();

  return {
    vertices,
    links,
    projects,
    hasUnprojected: graph.nodes.some((node) => node.project === null),
    ghostCount: vertices.length - graph.nodes.length,
  };
}

export interface VisibilitySpec {
  classes: Record<RecordEdgeClass, boolean>;
  project: string;
}

export interface VisibleView {
  vertex: boolean[];
  link: boolean[];
  nodeCount: number;
  linkCount: number;
}

export function resolveVisibility(model: GraphModel, spec: VisibilitySpec): VisibleView {
  const projectMatches = (node: GraphNode | null): boolean => {
    const project = node?.project ?? null;
    if (spec.project === ALL_PROJECTS) {
      return true;
    }
    if (spec.project === NO_PROJECT) {
      return project === null;
    }
    return project === spec.project;
  };

  const byFilter = model.vertices.map((vertex) => !vertex.ghost && projectMatches(vertex.node));

  const link = model.links.map(
    (entry) => spec.classes[entry.class] && byFilter[entry.source]! && byFilter[entry.target]!,
  );

  const vertex = [...byFilter];
  model.links.forEach((entry, index) => {
    if (link[index]) {
      vertex[entry.source] = true;
      vertex[entry.target] = true;
    }
  });

  return {
    vertex,
    link,
    nodeCount: countTrue(vertex),
    linkCount: countTrue(link),
  };
}

function countTrue(flags: boolean[]): number {
  let total = 0;
  for (const flag of flags) {
    if (flag) {
      total++;
    }
  }
  return total;
}
