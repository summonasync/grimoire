import { cn } from "@/shared/lib/cn";

const tones = [
  "border-accent/50 text-accent bg-accent-soft",
  "border-jade/45 text-jade bg-jade/10",
  "border-gilt/45 text-gilt bg-gilt/10",
  "border-ember/45 text-ember bg-ember/10",
  "border-arcane/45 text-arcane bg-arcane/10",
  "border-tide/45 text-tide bg-tide/10",
  "border-rose/45 text-rose bg-rose/10",
  "border-moss/45 text-moss bg-moss/10",
] as const;

const neutralTone = "border-border text-muted bg-transparent";

function projectTone(projectId: string): string {
  let hash = 5381;
  for (let index = 0; index < projectId.length; index++) {
    hash = ((hash << 5) + hash + projectId.charCodeAt(index)) | 0;
  }
  return tones[Math.abs(hash) % tones.length]!;
}

export function ProjectBadge({
  projectId,
  className,
}: {
  projectId: string | null;
  className?: string;
}) {
  if (!projectId) {
    return (
      <span
        title="No project — visible across the whole tome"
        className={cn(
          "inline-flex shrink-0 items-center rounded-md border px-1.5 py-0.5 text-[0.68rem] font-medium tracking-wide",
          neutralTone,
          className,
        )}
      >
        cross-project
      </span>
    );
  }

  return (
    <span
      title={`Project: ${projectId}`}
      className={cn(
        "inline-flex shrink-0 items-center rounded-md border px-1.5 py-0.5 text-[0.68rem] font-medium tracking-wide",
        projectTone(projectId),
        className,
      )}
    >
      {projectId}
    </span>
  );
}
