export function relativeTime(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) {
    return "—";
  }
  const seconds = Math.round((Date.now() - then) / 1000);
  const future = seconds < 0;
  const magnitude = Math.abs(seconds);

  // A tier is (exclusive upper bound in seconds, unit label, divisor for that unit).
  // Pairing the divisor with the unit it PRODUCES is the whole point: /60 yields
  // minutes. The previous table paired 60 with "s", so every tier read one unit
  // too small — 22 minutes rendered as "22s ago".
  const tiers: Array<[number, string, number]> = [
    [3600, "m", 60],
    [86400, "h", 3600],
    [604800, "d", 86400],
    [2629800, "w", 604800],
    [31557600, "mo", 2629800],
  ];

  if (magnitude < 60) {
    return future ? "in just now" : "just now";
  }

  const say = (value: number, unit: string) => (future ? `in ${value}${unit}` : `${value}${unit} ago`);

  for (const [upperBound, unit, divisor] of tiers) {
    if (magnitude < upperBound) {
      return say(Math.floor(magnitude / divisor), unit);
    }
  }

  return say(Math.floor(magnitude / 31557600), "y");
}

export function absoluteTime(iso: string | null | undefined): string {
  if (!iso) {
    return "—";
  }
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) {
    return "—";
  }
  return date.toLocaleString(undefined, {
    year: "numeric",
    month: "short",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function compactNumber(value: number): string {
  return value.toLocaleString(undefined, { maximumFractionDigits: 0 });
}

export function elapsedTime(ms: number): string {
  return `${(ms / 1000).toFixed(2)} s`;
}

export function importanceScore(importance: number): number {
  return Math.round(Math.min(1, Math.max(0, importance)) * 10);
}

export function percent(value: number, total: number): string {
  if (total <= 0) {
    return "0%";
  }
  return `${Math.round((value / total) * 100)}%`;
}

export function shortId(id: string): string {
  return id.replace(/-/g, "").slice(0, 8);
}

export function actorLabel(alias: string, vendor: string | null | undefined): string {
  return vendor ? `${alias} (${vendor})` : alias;
}
