"use client";

import { useEffect, useRef, useState } from "react";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";
import { ApiError } from "@/shared/api/client";
import { useShelfToasts } from "../lib/shelf-toasts";
import {
  useForgetTome,
  useMergeTome,
  usePatchTomeManifest,
  usePurgeTome,
  useRenameTome,
} from "../lib/use-shelf-mutations";
import type { TomeRetomeResult, TomeSummary } from "../types";
import { TomeDeleteConfirm } from "./tome-delete-confirm";
import { TomeDescriptionForm } from "./tome-description-form";
import { TomeHardDeleteConfirm } from "./tome-hard-delete-confirm";
import { TomeMergePicker } from "./tome-merge-picker";
import { TomeRenameForm } from "./tome-rename-form";

type VerbMode = "closed" | "rename" | "merge" | "description" | "delete" | "purge";

interface TomeCardMenuProps {
  summary: TomeSummary;
  tomes: TomeSummary[];
  onGone: () => void;
}

export function TomeCardMenu({ summary, tomes, onGone }: TomeCardMenuProps) {
  const [menuOpen, setMenuOpen] = useState(false);
  const [mode, setMode] = useState<VerbMode>("closed");
  const rename = useRenameTome();
  const merge = useMergeTome();
  const forget = useForgetTome();
  const patch = usePatchTomeManifest();
  const purge = usePurgeTome();
  const { push } = useShelfToasts();
  const triggerRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setMenuOpen(false);
        triggerRef.current?.focus();
      }
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [menuOpen]);

  const pick = (next: VerbMode) => {
    rename.reset();
    merge.reset();
    forget.reset();
    patch.reset();
    purge.reset();
    setMenuOpen(false);
    setMode(next);
  };

  const collapse = () => {
    setMenuOpen(false);
    triggerRef.current?.focus();
  };

  const close = () => {
    setMode("closed");
    triggerRef.current?.focus();
  };

  const undoRename = (result: TomeRetomeResult) => {
    rename.mutate(
      { from: result.to, to: result.from },
      {
        onSuccess: (undone) =>
          push({
            kind: "success",
            text: `Reverted — '${undone.to}' holds the ${undone.retomed} records again.`,
          }),
        onError: (error) => push({ kind: "error", text: error.message }),
      },
    );
  };

  const skippedNote = (skipped: number) => (skipped > 0 ? `, ${skipped} skipped` : "");

  const alreadyTombstonedNote = (count: number) =>
    count > 0 ? `, ${count} already tombstoned` : "";

  return (
    <div className="pointer-events-none absolute inset-0 z-20">
      <div className="pointer-events-auto absolute top-3 right-3">
        <button
          ref={triggerRef}
          type="button"
          aria-label={`Tome ${summary.key} actions`}
          aria-expanded={menuOpen}
          onClick={() => setMenuOpen((open) => !open)}
          className={cn(
            "inline-flex size-7 cursor-pointer items-center justify-center rounded-lg text-muted transition-colors",
            "hover:bg-foreground/5 hover:text-foreground focus-visible:ring-2 focus-visible:ring-jade/60 focus-visible:outline-none",
            menuOpen && "bg-foreground/5 text-foreground",
          )}
        >
          <Icon name="menu" strokeWidth={2.6} className="size-4" />
        </button>
        {menuOpen && (
          <>
            <div aria-hidden className="fixed inset-0" onClick={collapse} />
            <div className="absolute top-8 right-0 z-30 w-40 rounded-lg border border-border-strong bg-surface-raised py-1 text-sm shadow-lg">
              <button
                type="button"
                onClick={() => pick("rename")}
                className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-left transition-colors hover:bg-foreground/5"
              >
                <Icon name="edit" className="size-3.5 text-muted" />
                Rename
              </button>
              <button
                type="button"
                onClick={() => pick("merge")}
                className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-left transition-colors hover:bg-foreground/5"
              >
                <Icon name="link" className="size-3.5 text-muted" />
                Merge into…
              </button>
              {summary.manifest && (
                <button
                  type="button"
                  onClick={() => pick("description")}
                  className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-left transition-colors hover:bg-foreground/5"
                >
                  <Icon name="tag" className="size-3.5 text-muted" />
                  Edit description
                </button>
              )}
              <div aria-hidden className="mx-2 my-1 border-t border-border" />
              <button
                type="button"
                onClick={() => pick("delete")}
                className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-left text-ember transition-colors hover:bg-ember/10"
              >
                <Icon name="tombstone" className="size-3.5" />
                Delete tome
              </button>
              <button
                type="button"
                onClick={() => pick("purge")}
                className="flex w-full cursor-pointer items-center gap-2 px-3 py-1.5 text-left text-ember transition-colors hover:bg-ember/10"
              >
                <Icon name="trash" className="size-3.5" />
                Delete permanently
              </button>
            </div>
          </>
        )}
      </div>

      {mode !== "closed" && (
        <div
          className={cn(
            "pointer-events-auto absolute inset-x-3 bottom-3 z-30 rounded-lg border bg-surface-raised p-3 shadow-lg",
            mode === "purge" ? "border-ember/50" : "border-border-strong",
          )}
        >
          {mode === "rename" && (
            <TomeRenameForm
              summary={summary}
              pending={rename.isPending}
              error={rename.error?.message ?? null}
              onCancel={close}
              onSubmit={(to) =>
                rename.mutate(
                  { from: summary.key, to },
                  {
                    onSuccess: (result) => {
                      close();
                      push({
                        kind: "success",
                        text: `Renamed '${result.from}' to '${result.to}' — ${result.retomed} records moved${skippedNote(result.skipped)}.`,
                        action: { label: "Undo", onClick: () => undoRename(result) },
                      });
                    },
                    onError: (error) => push({ kind: "error", text: error.message }),
                  },
                )
              }
            />
          )}
          {mode === "merge" && (
            <TomeMergePicker
              summary={summary}
              tomes={tomes}
              pending={merge.isPending}
              error={merge.error?.message ?? null}
              onCancel={close}
              onMerge={(into) =>
                merge.mutate(
                  { from: summary.key, into },
                  {
                    onSuccess: (result) => {
                      close();
                      push({
                        kind: "success",
                        text: `Merged '${result.from}' into '${result.to}' — ${result.retomed} records moved${skippedNote(result.skipped)}.`,
                      });
                      onGone();
                    },
                    onError: (error) => push({ kind: "error", text: error.message }),
                  },
                )
              }
            />
          )}
          {mode === "description" && (
            <TomeDescriptionForm
              summary={summary}
              pending={patch.isPending}
              error={patch.error?.message ?? null}
              onCancel={close}
              onSubmit={(description) =>
                patch.mutate(
                  { tome: summary.key, description },
                  {
                    onSuccess: (manifest) => {
                      close();
                      push({ kind: "success", text: `Description of '${manifest.key}' updated.` });
                    },
                    onError: (error) => {
                      push({ kind: "error", text: error.message });
                      if (error instanceof ApiError && error.status === 404) {
                        close();
                      }
                    },
                  },
                )
              }
            />
          )}
          {mode === "delete" && (
            <TomeDeleteConfirm
              summary={summary}
              pending={forget.isPending}
              error={forget.error?.message ?? null}
              onCancel={close}
              onDelete={(reason) =>
                forget.mutate(
                  { tome: summary.key, reason },
                  {
                    onSuccess: (result) => {
                      close();
                      push({
                        kind: "success",
                        text: `Tombstoned tome '${result.tome}' — ${result.forgotten} records hidden from recall until GC${alreadyTombstonedNote(result.skipped)}.`,
                      });
                      onGone();
                    },
                    onError: (error) => push({ kind: "error", text: error.message }),
                  },
                )
              }
            />
          )}
          {mode === "purge" && (
            <TomeHardDeleteConfirm
              summary={summary}
              pending={purge.isPending}
              error={purge.error?.message ?? null}
              onCancel={close}
              onConfirm={(confirm) =>
                purge.mutate(
                  { tome: summary.key, confirm },
                  {
                    onSuccess: (result) => {
                      close();
                      push({
                        kind: "success",
                        text: `Purged '${result.key}' — ${result.purged.active} active, ${result.purged.tombstoned} tombstoned, ${result.purged.superseded} superseded removed${
                          result.manifestPurged ? ", manifest deleted" : ", manifest left behind"
                        }.`,
                      });
                      onGone();
                    },
                    onError: (error) => {
                      push({ kind: "error", text: error.message });
                      if (error instanceof ApiError && error.status === 404) {
                        close();
                      }
                    },
                  },
                )
              }
            />
          )}
        </div>
      )}
    </div>
  );
}
