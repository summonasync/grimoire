import type { ScryItem } from "../types";
import { KindBadge, ScopeBadge } from "@/shared/ui/badge";
import { ScoreRing } from "@/shared/ui/charts";
import { Icon } from "@/shared/ui/icons";
import { Sigil } from "@/shared/ui/sigil";
import { relativeTime } from "@/shared/ui/format";

interface ScryResultItemProps {
  item: ScryItem;
}

export function ScryResultItem({ item }: ScryResultItemProps) {
  return (
    <li className="panel flex items-start gap-3 px-4 py-3">
      <span className="relative flex shrink-0 flex-col items-center gap-1 pt-0.5">
        <ScoreRing score={item.score} />
        <span className="text-[0.65rem] text-muted tabular-nums">{item.score.toFixed(2)}</span>
      </span>

      <div className="min-w-0 flex-1">
        <p className="line-clamp-3 text-sm leading-snug whitespace-pre-wrap">{item.content}</p>
        <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1.5 text-xs text-muted">
          <span className="flex items-center gap-1.5">
            <Icon name="book" className="size-3.5" />
            <Sigil seed={item.tome} className="size-3.5" />
            <span className="font-medium text-foreground">{item.tome}</span>
          </span>
          <ScopeBadge scope={item.scope} />
          <KindBadge kind={item.kind} />
          {item.actor && (
            <span className="flex items-center gap-1">
              <Icon name="persona" className="size-3.5" />
              {item.actor}
            </span>
          )}
          <span title={item.createdAt}>created {relativeTime(item.createdAt)}</span>
          <span className="tabular-nums">sim {item.similarity.toFixed(3)}</span>
          {item.tags.length > 0 && (
            <span className="flex flex-wrap gap-1">
              {item.tags.map((tag) => (
                <span key={tag} className="rounded border border-border px-1 text-[0.65rem]">
                  #{tag}
                </span>
              ))}
            </span>
          )}
        </div>
      </div>
    </li>
  );
}
