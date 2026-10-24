export interface SimulationNode {
  x: number;
  y: number;
  vx: number;
  vy: number;
}

export interface SimulationLink {
  source: number;
  target: number;
}

interface SimulationConfig {
  repulsion: number;
  springLength: number;
  springStiffness: number;
  centerPull: number;
  damping: number;
  maxSpeed: number;
}

export const simulationConfig: SimulationConfig = {
  repulsion: 1400,
  springLength: 90,
  springStiffness: 0.08,
  centerPull: 0.02,
  damping: 0.8,
  maxSpeed: 18,
};

export interface Simulation {
  readonly nodes: SimulationNode[];
  step(): void;
  energy(): number;
}

const minimumSeparation = 0.01;

export function createSimulation(
  count: number,
  links: SimulationLink[],
  config: SimulationConfig = simulationConfig,
): Simulation {
  const nodes: SimulationNode[] = [];
  for (let index = 0; index < count; index++) {
    const angle = (index / Math.max(count, 1)) * Math.PI * 2;
    const ring = 110 + (index % 8) * 12;
    nodes.push({ x: Math.cos(angle) * ring, y: Math.sin(angle) * ring, vx: 0, vy: 0 });
  }

  const forceX = new Float64Array(count);
  const forceY = new Float64Array(count);

  const step = () => {
    forceX.fill(0);
    forceY.fill(0);

    for (let i = 0; i < count; i++) {
      const a = nodes[i]!;
      for (let j = i + 1; j < count; j++) {
        const b = nodes[j]!;
        let dx = a.x - b.x;
        let dy = a.y - b.y;
        let dist2 = dx * dx + dy * dy;
        if (dist2 < minimumSeparation * minimumSeparation) {
          const nudge = 1.7 + 0.31 * (i * 7 + j * 13);
          dx = Math.cos(nudge) * minimumSeparation;
          dy = Math.sin(nudge) * minimumSeparation;
          dist2 = minimumSeparation * minimumSeparation;
        }
        const dist = Math.sqrt(dist2);
        const push = config.repulsion / dist2;
        const fx = (dx / dist) * push;
        const fy = (dy / dist) * push;
        forceX[i] = (forceX[i] ?? 0) + fx;
        forceY[i] = (forceY[i] ?? 0) + fy;
        forceX[j] = (forceX[j] ?? 0) - fx;
        forceY[j] = (forceY[j] ?? 0) - fy;
      }
    }

    for (const link of links) {
      const a = nodes[link.source];
      const b = nodes[link.target];
      if (!a || !b) {
        continue;
      }
      const dx = b.x - a.x;
      const dy = b.y - a.y;
      const dist = Math.sqrt(dx * dx + dy * dy);
      if (dist < minimumSeparation) {
        continue;
      }
      const pull = config.springStiffness * (dist - config.springLength);
      const fx = (dx / dist) * pull;
      const fy = (dy / dist) * pull;
      forceX[link.source] = (forceX[link.source] ?? 0) + fx;
      forceY[link.source] = (forceY[link.source] ?? 0) + fy;
      forceX[link.target] = (forceX[link.target] ?? 0) - fx;
      forceY[link.target] = (forceY[link.target] ?? 0) - fy;
    }

    for (let i = 0; i < count; i++) {
      const node = nodes[i]!;
      node.vx = (node.vx + (forceX[i] ?? 0) - node.x * config.centerPull) * config.damping;
      node.vy = (node.vy + (forceY[i] ?? 0) - node.y * config.centerPull) * config.damping;
      const speed = Math.sqrt(node.vx * node.vx + node.vy * node.vy);
      if (speed > config.maxSpeed) {
        const scale = config.maxSpeed / speed;
        node.vx *= scale;
        node.vy *= scale;
      }
      node.x += node.vx;
      node.y += node.vy;
    }
  };

  const energy = () => {
    let total = 0;
    for (const node of nodes) {
      total += node.vx * node.vx + node.vy * node.vy;
    }
    return total;
  };

  return { nodes, step, energy };
}
