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

  const units: Array<[number, string]> = [
    [60, "s"],
    [3600, "m"],
    [86400, "h"],
    [604800, "d"],
    [2629800, "w"],
    [31557600, "mo"],
  ];

  if (magnitude < 60) {
    return future ? "in just now" : "just now";
  }
  for (let index = 0; index < units.length; index += 1) {
    const [limit] = units[index]!;
    const next = units[index + 1];
    if (!next || magnitude < next[0]) {
      const value = Math.floor(magnitude / limit);
      const suffix = future ? `in ${value}${units[index]![1]}` : `${value}${units[index]![1]} ago`;
      return suffix;
    }
  }
  return `${Math.floor(magnitude / 31557600)}y ago`;
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
