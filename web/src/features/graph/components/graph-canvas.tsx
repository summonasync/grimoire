"use client";

import {
  useCallback,
  useEffect,
  useRef,
  type PointerEvent as ReactPointerEvent,
} from "react";
import { createSimulation, type Simulation, type SimulationLink } from "../lib/force-simulation";
import { edgeClassStyles, kindColorVars } from "../lib/graph-styles";
import type { GraphModel, VisibleView } from "../lib/graph-view-model";
import { shortId } from "@/shared/ui/format";

interface GraphCanvasProps {
  model: GraphModel;
  visibility: VisibleView;
  onOpenRecord: (id: string) => void;
}

interface ViewTransform {
  scale: number;
  offsetX: number;
  offsetY: number;
}

interface CanvasPalette {
  background: string;
  surface: string;
  foreground: string;
  border: string;
  muted: string;
  accent: string;
}

const padding = 48;
const warmupTicks = 180;
const maxTicks = 900;
const settleEnergy = 0.25;
const maxLabelLength = 72;
const ghostRadius = 4;
const pressTolerance = 5;

function activeRadius(degree: number): number {
  return 5 + Math.min(degree, 10) * 0.8;
}

function vertexRadius(vertex: GraphModel["vertices"][number], degree: number): number {
  return vertex.ghost ? ghostRadius : activeRadius(degree);
}

function clipLabel(label: string): string {
  return label.length > maxLabelLength ? `${label.slice(0, maxLabelLength - 1)}…` : label;
}

export function GraphCanvas({ model, visibility, onOpenRecord }: GraphCanvasProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const simulationRef = useRef<Simulation | null>(null);
  const transformRef = useRef<ViewTransform>({ scale: 1, offsetX: 0, offsetY: 0 });
  const degreeRef = useRef<number[]>([]);
  const hoverRef = useRef<number | null>(null);
  const tickCountRef = useRef(0);
  const frameRef = useRef(0);
  const runningRef = useRef(false);
  const pressRef = useRef<{ x: number; y: number } | null>(null);
  const latestRef = useRef({ model, visibility, onOpenRecord });

  useEffect(() => {
    latestRef.current = { model, visibility, onOpenRecord };
  });

  const draw = useCallback(() => {
    const canvas = canvasRef.current;
    const container = containerRef.current;
    const sim = simulationRef.current;
    if (!canvas || !container || !sim) {
      return;
    }
    const ctx = canvas.getContext("2d");
    if (!ctx) {
      return;
    }

    const dpr = window.devicePixelRatio || 1;
    const width = container.clientWidth;
    const height = container.clientHeight;
    const bitmapWidth = Math.max(1, Math.round(width * dpr));
    const bitmapHeight = Math.max(1, Math.round(height * dpr));
    if (canvas.width !== bitmapWidth || canvas.height !== bitmapHeight) {
      canvas.width = bitmapWidth;
      canvas.height = bitmapHeight;
    }
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, width, height);

    const style = getComputedStyle(canvas);
    const cssVar = (name: string) => style.getPropertyValue(name).trim();
    const palette: CanvasPalette = {
      background: cssVar("--background"),
      surface: cssVar("--surface"),
      foreground: cssVar("--foreground"),
      border: cssVar("--border-strong"),
      muted: cssVar("--muted"),
      accent: cssVar("--accent"),
    };

    const { model: currentModel, visibility: currentVisibility } = latestRef.current;
    const nodes = sim.nodes;
    const hover = hoverRef.current;

    const degree = new Array<number>(nodes.length).fill(0);
    currentModel.links.forEach((link, index) => {
      if (!currentVisibility.link[index]) {
        return;
      }
      degree[link.source] = (degree[link.source] ?? 0) + 1;
      degree[link.target] = (degree[link.target] ?? 0) + 1;
    });
    degreeRef.current = degree;

    let minX = Infinity;
    let minY = Infinity;
    let maxX = -Infinity;
    let maxY = -Infinity;
    let visibleAny = false;
    for (let index = 0; index < currentModel.vertices.length; index++) {
      if (!currentVisibility.vertex[index]) {
        continue;
      }
      const node = nodes[index];
      if (!node) {
        continue;
      }
      visibleAny = true;
      minX = Math.min(minX, node.x);
      minY = Math.min(minY, node.y);
      maxX = Math.max(maxX, node.x);
      maxY = Math.max(maxY, node.y);
    }
    if (!visibleAny) {
      return;
    }

    const scale = Math.min(
      (width - padding * 2) / Math.max(maxX - minX, 1),
      (height - padding * 2) / Math.max(maxY - minY, 1),
      2,
    );
    const offsetX = (width - (minX + maxX) * scale) / 2;
    const offsetY = (height - (minY + maxY) * scale) / 2;
    transformRef.current = { scale, offsetX, offsetY };

    const px = (x: number) => x * scale + offsetX;
    const py = (y: number) => y * scale + offsetY;

    ctx.lineCap = "round";
    for (let index = 0; index < currentModel.links.length; index++) {
      if (!currentVisibility.link[index]) {
        continue;
      }
      const link = currentModel.links[index]!;
      const a = nodes[link.source];
      const b = nodes[link.target];
      if (!a || !b) {
        continue;
      }
      const edgeStyle = edgeClassStyles[link.class];
      const emphasized = hover === null || hover === link.source || hover === link.target;
      ctx.globalAlpha = emphasized ? 0.85 : 0.14;
      ctx.strokeStyle = cssVar(edgeStyle.colorVar) || palette.muted;
      ctx.lineWidth = edgeStyle.width;
      ctx.setLineDash(edgeStyle.dashed ? [4, 4] : []);
      ctx.beginPath();
      ctx.moveTo(px(a.x), py(a.y));
      ctx.lineTo(px(b.x), py(b.y));
      ctx.stroke();
    }
    ctx.setLineDash([]);
    ctx.globalAlpha = 1;

    for (let index = 0; index < currentModel.vertices.length; index++) {
      if (!currentVisibility.vertex[index]) {
        continue;
      }
      const vertex = currentModel.vertices[index]!;
      const node = nodes[index];
      if (!node) {
        continue;
      }
      if (vertex.ghost) {
        ctx.beginPath();
        ctx.arc(px(node.x), py(node.y), ghostRadius, 0, Math.PI * 2);
        ctx.fillStyle = palette.background;
        ctx.fill();
        ctx.lineWidth = 1.2;
        ctx.strokeStyle = hover === index ? palette.accent : palette.muted;
        ctx.stroke();
        continue;
      }
      const radius = activeRadius(degree[index] ?? 0);
      ctx.beginPath();
      ctx.arc(px(node.x), py(node.y), radius, 0, Math.PI * 2);
      ctx.fillStyle = cssVar(kindColorVars[vertex.node!.kind]) || palette.muted;
      ctx.fill();
      ctx.lineWidth = 1.2;
      ctx.strokeStyle = hover === index ? palette.accent : palette.border;
      ctx.stroke();
      if (hover === index) {
        ctx.beginPath();
        ctx.arc(px(node.x), py(node.y), radius + 3, 0, Math.PI * 2);
        ctx.strokeStyle = palette.accent;
        ctx.lineWidth = 1.6;
        ctx.stroke();
      }
    }

    if (hover !== null && currentVisibility.vertex[hover]) {
      const vertex = currentModel.vertices[hover]!;
      const node = nodes[hover]!;
      const label = vertex.ghost
        ? `${vertex.ghostStatus ?? "inactive"} · ${shortId(vertex.id)}`
        : clipLabel(vertex.node?.snippet ?? "");
      ctx.font = "11px ui-sans-serif, system-ui, sans-serif";
      const textWidth = ctx.measureText(label).width;
      const boxWidth = textWidth + 16;
      const boxHeight = 22;
      const boxX = Math.min(
        Math.max(px(node.x) - boxWidth / 2, 4),
        Math.max(width - boxWidth - 4, 4),
      );
      const radius = vertexRadius(vertex, degree[hover] ?? 0);
      const boxY = py(node.y) + radius + 8;
      ctx.fillStyle = palette.surface;
      ctx.strokeStyle = palette.border;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.roundRect(boxX, boxY, boxWidth, boxHeight, 6);
      ctx.fill();
      ctx.stroke();
      ctx.fillStyle = palette.foreground;
      ctx.textBaseline = "middle";
      ctx.fillText(label, boxX + 8, boxY + boxHeight / 2 + 0.5);
    }
  }, []);

  const tickRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    tickRef.current = () => {
      const sim = simulationRef.current;
      if (sim && tickCountRef.current < maxTicks) {
        sim.step();
        tickCountRef.current += 1;
      }
      draw();
      const settled =
        !sim || tickCountRef.current >= maxTicks || sim.energy() <= settleEnergy;
      if (settled) {
        runningRef.current = false;
        return;
      }
      frameRef.current = requestAnimationFrame(tickRef.current!);
    };
    return () => {
      tickRef.current = null;
    };
  }, [draw]);

  const kick = useCallback(() => {
    if (runningRef.current) {
      return;
    }
    const tick = tickRef.current;
    if (!tick) {
      return;
    }
    runningRef.current = true;
    frameRef.current = requestAnimationFrame(tick);
  }, []);

  useEffect(() => {
    const links: SimulationLink[] = model.links.map((link) => ({
      source: link.source,
      target: link.target,
    }));
    const sim = createSimulation(model.vertices.length, links);
    simulationRef.current = sim;
    tickCountRef.current = 0;
    while (tickCountRef.current < warmupTicks) {
      sim.step();
      tickCountRef.current += 1;
      if (sim.energy() <= settleEnergy) {
        break;
      }
    }
    hoverRef.current = null;
    kick();
    return () => {
      cancelAnimationFrame(frameRef.current);
      runningRef.current = false;
      simulationRef.current = null;
    };
  }, [model, kick]);

  useEffect(() => {
    kick();
  }, [visibility, kick]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) {
      return;
    }
    const observer = new ResizeObserver(() => kick());
    observer.observe(container);
    return () => observer.disconnect();
  }, [kick]);

  useEffect(() => {
    const observer = new MutationObserver(() => kick());
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ["class"] });
    return () => observer.disconnect();
  }, [kick]);

  const pickVertex = useCallback((clientX: number, clientY: number): number | null => {
    const canvas = canvasRef.current;
    const sim = simulationRef.current;
    if (!canvas || !sim) {
      return null;
    }
    const rect = canvas.getBoundingClientRect();
    const pointerX = clientX - rect.left;
    const pointerY = clientY - rect.top;
    const { scale, offsetX, offsetY } = transformRef.current;
    const { model: currentModel, visibility: currentVisibility } = latestRef.current;
    let best: number | null = null;
    let bestDist = Infinity;
    for (let index = 0; index < currentModel.vertices.length; index++) {
      if (!currentVisibility.vertex[index]) {
        continue;
      }
      const node = sim.nodes[index];
      if (!node) {
        continue;
      }
      const vertex = currentModel.vertices[index]!;
      const reach = vertexRadius(vertex, degreeRef.current[index] ?? 0) + 6;
      const dx = node.x * scale + offsetX - pointerX;
      const dy = node.y * scale + offsetY - pointerY;
      const dist = dx * dx + dy * dy;
      if (dist < reach * reach && dist < bestDist) {
        best = index;
        bestDist = dist;
      }
    }
    return best;
  }, []);

  const setCursor = (cursor: string) => {
    const canvas = canvasRef.current;
    if (canvas) {
      canvas.style.cursor = cursor;
    }
  };

  const handlePointerMove = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const picked = pickVertex(event.clientX, event.clientY);
    if (picked !== hoverRef.current) {
      hoverRef.current = picked;
      setCursor(picked === null ? "default" : "pointer");
      kick();
    }
  };

  const handlePointerLeave = () => {
    if (hoverRef.current !== null) {
      hoverRef.current = null;
      setCursor("default");
      kick();
    }
  };

  const handlePointerDown = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    pressRef.current = { x: event.clientX, y: event.clientY };
  };

  const handlePointerUp = (event: ReactPointerEvent<HTMLCanvasElement>) => {
    const press = pressRef.current;
    pressRef.current = null;
    if (!press) {
      return;
    }
    const moved =
      Math.abs(event.clientX - press.x) + Math.abs(event.clientY - press.y) > pressTolerance;
    if (moved) {
      return;
    }
    const picked = pickVertex(event.clientX, event.clientY);
    if (picked === null) {
      return;
    }
    const vertex = latestRef.current.model.vertices[picked];
    if (vertex) {
      latestRef.current.onOpenRecord(vertex.id);
    }
  };

  return (
    <div ref={containerRef} className="relative h-full min-h-0 w-full overflow-hidden">
      <canvas
        ref={canvasRef}
        role="img"
        aria-label="Record graph of the selected tome"
        className="absolute inset-0 size-full touch-none select-none"
        onPointerMove={handlePointerMove}
        onPointerLeave={handlePointerLeave}
        onPointerDown={handlePointerDown}
        onPointerUp={handlePointerUp}
      />
    </div>
  );
}
