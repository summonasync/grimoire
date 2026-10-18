import type { ScryItem } from "../types";

interface ScryResultItemProps {
  item: ScryItem;
}

export function ScryResultItem({ item }: ScryResultItemProps) {
  return (
    <li className="rounded-lg border border-border bg-surface px-4 py-3">
      <p className="line-clamp-3 text-sm whitespace-pre-wrap">{item.content}</p>
      <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
        <span className="rounded bg-background px-1.5 py-0.5 font-medium text-foreground">
          tome {item.tome}
        </span>
        <span>{item.scope}</span>
        <span>{item.kind}</span>
        <span>score {item.score.toFixed(3)}</span>
        <span>similarity {item.similarity.toFixed(3)}</span>
        {item.actor && <span>actor {item.actor}</span>}
        <span>created {new Date(item.createdAt).toLocaleString()}</span>
        {item.projectId && <span>project {item.projectId}</span>}
        {item.tags.length > 0 && (
          <span className="flex flex-wrap gap-1">
            {item.tags.map((tag) => (
              <span key={tag} className="rounded-full border border-border px-1.5 py-0.5">
                #{tag}
              </span>
            ))}
          </span>
        )}
      </div>
    </li>
  );
}
