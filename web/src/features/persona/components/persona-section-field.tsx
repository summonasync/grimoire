import { cn } from "@/shared/lib/cn";
import { Icon, type IconName } from "@/shared/ui/icons";
import type { PersonaSectionKey, PersonaSectionSpec } from "../types";
import { personaSectionLimit } from "../lib/persona-document";

const sectionIcons: Record<PersonaSectionKey, IconName> = {
  identity: "persona",
  tone: "spark",
  preferences: "settings",
  rules: "alert",
};

interface PersonaSectionFieldProps {
  spec: PersonaSectionSpec;
  index: number;
  value: string;
  disabled: boolean;
  onChange: (key: PersonaSectionKey, value: string) => void;
}

export function PersonaSectionField({
  spec,
  index,
  value,
  disabled,
  onChange,
}: PersonaSectionFieldProps) {
  const nearLimit = value.length >= Math.round(personaSectionLimit * 0.9);

  return (
    <section className="panel px-4 py-3.5">
      <div className="flex items-start gap-3">
        <span className="flex size-7 shrink-0 items-center justify-center rounded-full border border-accent/50 bg-accent-soft font-display text-sm text-accent">
          {index}
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-3">
            <label
              htmlFor={`persona-${spec.key}`}
              className="flex items-center gap-2 font-display text-base font-semibold"
            >
              <Icon name={sectionIcons[spec.key]} className="size-4 text-muted" />
              {spec.label}
            </label>
            <span className={cn("text-xs tabular-nums", nearLimit ? "text-ember" : "text-muted")}>
              {value.length} / {personaSectionLimit}
            </span>
          </div>
          <p className="mt-0.5 text-xs text-muted">{spec.hint}</p>
          <textarea
            id={`persona-${spec.key}`}
            value={value}
            disabled={disabled}
            maxLength={personaSectionLimit}
            rows={spec.rows}
            placeholder={spec.placeholder}
            onChange={(event) => onChange(spec.key, event.target.value)}
            className="field mt-2 resize-y leading-relaxed disabled:opacity-60"
          />
        </div>
      </div>
    </section>
  );
}
