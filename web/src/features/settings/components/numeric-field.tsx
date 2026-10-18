import { cn } from "@/shared/lib/cn";
import type { SettingsFieldSpec } from "../lib/field-groups";
import type { SettingsFieldKey } from "../types";

interface NumericFieldProps {
  spec: SettingsFieldSpec;
  value: string;
  error: string | undefined;
  disabled: boolean;
  onChange: (key: SettingsFieldKey, value: string) => void;
}

export function NumericField({ spec, value, error, disabled, onChange }: NumericFieldProps) {
  const inputId = `settings-${spec.key.replace(".", "-")}`;

  return (
    <div>
      <div className="flex items-baseline justify-between gap-2">
        <label htmlFor={inputId} className="text-sm font-medium">
          {spec.label}
        </label>
        <span className="text-xs text-muted">
          {spec.integer ? `integer ${spec.min} to ${spec.max}` : `${spec.min} to ${spec.max}`}
        </span>
      </div>
      <input
        id={inputId}
        type="number"
        min={spec.min}
        max={spec.max}
        step={spec.step}
        value={value}
        disabled={disabled}
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? `${inputId}-error` : undefined}
        onChange={(event) => onChange(spec.key, event.target.value)}
        className={cn(
          "mt-1 w-full rounded-md border bg-background px-3 py-2 text-sm outline-none focus:border-accent disabled:opacity-60",
          error === undefined ? "border-border" : "border-red-500/60",
        )}
      />
      {error !== undefined ? (
        <p id={`${inputId}-error`} className="mt-1 text-xs text-red-400">
          {error}
        </p>
      ) : (
        <p className="mt-1 text-xs text-muted">{spec.hint}</p>
      )}
    </div>
  );
}
