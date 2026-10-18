import { cn } from "@/shared/lib/cn";
import type { PersonaSectionKey, PersonaSectionSpec } from "../types";
import { personaSectionLimit } from "../lib/persona-document";

interface PersonaSectionFieldProps {
  spec: PersonaSectionSpec;
  value: string;
  disabled: boolean;
  onChange: (key: PersonaSectionKey, value: string) => void;
}

export function PersonaSectionField({ spec, value, disabled, onChange }: PersonaSectionFieldProps) {
  const nearLimit = value.length >= Math.round(personaSectionLimit * 0.9);

  return (
    <section className="rounded-lg border border-border bg-surface p-4">
      <div className="flex items-baseline justify-between gap-3">
        <label htmlFor={`persona-${spec.key}`} className="text-sm font-medium">
          {spec.label}
        </label>
        <span className={cn("text-xs", nearLimit ? "text-amber-500" : "text-muted")}>
          {value.length} / {personaSectionLimit}
        </span>
      </div>
      <p className="mt-1 text-xs text-muted">{spec.hint}</p>
      <textarea
        id={`persona-${spec.key}`}
        value={value}
        disabled={disabled}
        maxLength={personaSectionLimit}
        rows={spec.rows}
        placeholder={spec.placeholder}
        onChange={(event) => onChange(spec.key, event.target.value)}
        className="mt-2 w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm leading-relaxed outline-none focus:border-accent disabled:opacity-60"
      />
    </section>
  );
}
