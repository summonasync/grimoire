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
    <div className="min-w-0">
      <div className="flex items-baseline justify-between gap-2">
        <label htmlFor={inputId} className="text-sm font-medium">
          {spec.label}
        </label>
        <span className="text-[0.68rem] text-muted tabular-nums">
          {spec.integer ? `int ${spec.min}–${spec.max}` : `${spec.min}–${spec.max}`}
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
          "field mt-1.5",
          error !== undefined && "border-ember focus-visible:border-ember",
          "disabled:opacity-60",
        )}
      />
      {error !== undefined ? (
        <p id={`${inputId}-error`} className="mt-1 text-xs text-ember">
          {error}
        </p>
      ) : (
        <p className="mt-1 text-xs text-muted">{spec.hint}</p>
      )}
    </div>
  );
}
