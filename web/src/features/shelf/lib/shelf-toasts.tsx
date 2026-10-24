"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { cn } from "@/shared/lib/cn";
import { Icon } from "@/shared/ui/icons";

export interface ShelfToast {
  id: number;
  kind: "success" | "error";
  text: string;
  action?: { label: string; onClick: () => void };
}

interface ShelfToastContextValue {
  push: (toast: Omit<ShelfToast, "id">) => void;
}

const ShelfToastContext = createContext<ShelfToastContextValue | null>(null);

const dismissAfterMs = 8000;
const maxVisible = 4;

export function ShelfToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ShelfToast[]>([]);
  const nextId = useRef(1);
  const timers = useRef(new Map<number, number>());

  useEffect(
    () => () => {
      for (const timer of timers.current.values()) {
        window.clearTimeout(timer);
      }
    },
    [],
  );

  const dismiss = useCallback((id: number) => {
    timers.current.delete(id);
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const push = useCallback(
    (toast: Omit<ShelfToast, "id">) => {
      const id = nextId.current++;
      setToasts((current) => [...current.slice(-(maxVisible - 1)), { ...toast, id }]);
      timers.current.set(id, window.setTimeout(() => dismiss(id), dismissAfterMs));
    },
    [dismiss],
  );

  const value = useMemo(() => ({ push }), [push]);

  return (
    <ShelfToastContext.Provider value={value}>
      {children}
      <div
        aria-live="polite"
        className="pointer-events-none fixed right-4 bottom-4 z-50 flex w-80 flex-col gap-2"
      >
        {toasts.map((toast) => {
          const action = toast.action;
          return (
            <div
              key={toast.id}
              className={cn(
                "panel-raised pointer-events-auto flex items-start gap-2.5 px-3.5 py-2.5 shadow-lg",
                toast.kind === "error" && "border-ember/50",
              )}
            >
              <Icon
                name={toast.kind === "error" ? "alert" : "check"}
                className={cn(
                  "mt-0.5 size-4 shrink-0",
                  toast.kind === "error" ? "text-ember" : "text-jade",
                )}
              />
              <p className="min-w-0 flex-1 text-xs leading-snug break-words">{toast.text}</p>
              {action && (
                <button
                  type="button"
                  onClick={() => {
                    action.onClick();
                    dismiss(toast.id);
                  }}
                  className="shrink-0 cursor-pointer text-xs font-medium text-accent hover:underline"
                >
                  {action.label}
                </button>
              )}
              <button
                type="button"
                aria-label="Dismiss notification"
                onClick={() => dismiss(toast.id)}
                className="shrink-0 cursor-pointer text-muted transition-colors hover:text-foreground"
              >
                <Icon name="close" className="size-3.5" />
              </button>
            </div>
          );
        })}
      </div>
    </ShelfToastContext.Provider>
  );
}

export function useShelfToasts(): ShelfToastContextValue {
  const value = useContext(ShelfToastContext);
  if (value === null) {
    throw new Error("useShelfToasts must be used inside ShelfToastProvider");
  }
  return value;
}
