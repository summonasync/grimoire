import { cn } from "@/shared/lib/cn";

export interface Slice {
  label: string;
  value: number;
  color: string;
}

const palette = ["var(--accent)", "var(--jade)", "var(--gilt)", "var(--ember)", "var(--muted)"];

export function resolveSliceColor(index: number): string {
  return palette[index % palette.length]!;
}

interface DonutProps {
  slices: Slice[];
  centerLabel?: string;
  centerValue?: string;
  className?: string;
  size?: number;
}

export function Donut({ slices, centerLabel, centerValue, className, size = 168 }: DonutProps) {
  const total = slices.reduce((sum, slice) => sum + slice.value, 0);
  const radius = size / 2 - 14;
  const circumference = 2 * Math.PI * radius;
  let offset = 0;

  return (
    <div className={cn("flex flex-wrap items-center gap-5", className)}>
      <svg viewBox={`0 0 ${size} ${size}`} width={size} height={size} className="shrink-0">
        <circle
          cx={size / 2}
          cy={size / 2}
          r={radius}
          fill="none"
          stroke="var(--border)"
          strokeWidth={14}
        />
        {total > 0 &&
          slices.map((slice) => {
            const length = (slice.value / total) * circumference;
            const dash = `${length} ${circumference - length}`;
            const element = (
              <circle
                key={slice.label}
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke={slice.color}
                strokeWidth={14}
                strokeDasharray={dash}
                strokeDashoffset={-offset}
                transform={`rotate(-90 ${size / 2} ${size / 2})`}
              />
            );
            offset += length;
            return element;
          })}
        {centerValue !== undefined && (
          <text
            x="50%"
            y="47%"
            textAnchor="middle"
            className="fill-foreground font-display"
            style={{ fontSize: size / 7 }}
          >
            {centerValue}
          </text>
        )}
        {centerLabel !== undefined && (
          <text
            x="50%"
            y="60%"
            textAnchor="middle"
            className="fill-muted"
            style={{ fontSize: size / 15 }}
          >
            {centerLabel}
          </text>
        )}
      </svg>
      <ul className="min-w-40 flex-1 space-y-1.5 text-xs">
        {slices.map((slice) => (
          <li key={slice.label} className="flex items-center gap-2">
            <span
              aria-hidden
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: slice.color }}
            />
            <span className="min-w-0 truncate text-muted">{slice.label}</span>
            <span className="ml-auto pl-3 font-medium tabular-nums">
              {slice.value.toLocaleString()}
            </span>
            <span className="w-10 shrink-0 text-right tabular-nums text-muted">
              {total > 0 ? `${Math.round((slice.value / total) * 100)}%` : "0%"}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}

interface MeterProps {
  value: number;
  max: number;
  color?: string;
  className?: string;
}

export function Meter({ value, max, color = "var(--accent)", className }: MeterProps) {
  const ratio = max > 0 ? Math.min(1, Math.max(0, value / max)) : 0;
  return (
    <div className={cn("h-1.5 w-full overflow-hidden rounded-full bg-border", className)}>
      <div
        className="h-full rounded-full transition-[width] duration-300"
        style={{ width: `${ratio * 100}%`, backgroundColor: color }}
      />
    </div>
  );
}

interface ScoreRingProps {
  score: number;
  className?: string;
}

export function ScoreRing({ score, className = "size-9" }: ScoreRingProps) {
  const clamped = Math.min(1, Math.max(0, score));
  const radius = 16;
  const circumference = 2 * Math.PI * radius;
  const color =
    clamped >= 0.8 ? "var(--jade)" : clamped >= 0.5 ? "var(--gilt)" : "var(--muted)";

  return (
    <div className={cn("relative shrink-0", className)}>
      <svg viewBox="0 0 40 40" className="size-full -rotate-90">
        <circle cx="20" cy="20" r={radius} fill="none" stroke="var(--border)" strokeWidth={3} />
        <circle
          cx="20"
          cy="20"
          r={radius}
          fill="none"
          stroke={color}
          strokeWidth={3}
          strokeLinecap="round"
          strokeDasharray={`${clamped * circumference} ${circumference}`}
        />
      </svg>
    </div>
  );
}
