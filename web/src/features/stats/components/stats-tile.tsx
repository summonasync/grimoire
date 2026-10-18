interface StatsTileProps {
  label: string;
  value: number;
}

export function StatsTile({ label, value }: StatsTileProps) {
  return (
    <div className="rounded-lg border border-border bg-surface px-4 py-3">
      <p className="text-xs font-medium text-muted uppercase">{label}</p>
      <p className="mt-1 text-2xl font-semibold tracking-tight">{value.toLocaleString()}</p>
    </div>
  );
}
