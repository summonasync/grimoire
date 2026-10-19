const glyphs = [
  "M12 3l2.4 5.6L20 11l-5.6 2.4L12 19l-2.4-5.6L4 11l5.6-2.4z",
  "M12 4l7 8-7 8-7-8z",
  "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 4.5a4.5 4.5 0 1 1 0 9 4.5 4.5 0 0 1 0-9z",
  "M5 19L12 5l7 14z",
  "M12 3l3.2 6.6L22 11l-6.8 1.4L12 19l-3.2-6.6L2 11l6.8-1.4z",
  "M6 6h12v12H6z M9 9h6v6H9z",
  "M12 4v16M4 12h16M7 7l10 10M17 7L7 17",
  "M12 3c3.5 3.2 5.3 6.2 5.3 9a5.3 5.3 0 0 1-10.6 0c0-2.8 1.8-5.8 5.3-9z",
];

const palettes = [
  "var(--accent)",
  "var(--gilt)",
  "var(--jade)",
  "var(--ember)",
];

function hash(value: string): number {
  let acc = 2166136261;
  for (let index = 0; index < value.length; index += 1) {
    acc ^= value.charCodeAt(index);
    acc = Math.imul(acc, 16777619);
  }
  return Math.abs(acc);
}

interface SigilProps {
  seed: string;
  className?: string;
}

export function Sigil({ seed, className = "size-5" }: SigilProps) {
  const index = hash(seed) % glyphs.length;
  const glyph = glyphs[index]!;
  const color = palettes[index % palettes.length]!;
  const rotation = ((hash(seed) >> 4) % 4) * 90;

  return (
    <svg
      aria-hidden
      viewBox="0 0 24 24"
      className={className}
      fill="none"
      stroke={color}
      strokeWidth={1.4}
      strokeLinejoin="round"
      style={{ transform: `rotate(${rotation}deg)` }}
    >
      <path d={glyph} />
    </svg>
  );
}
