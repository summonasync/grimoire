"use client";

import { useEffect } from "react";

const confirmMessage = "You have unsaved persona changes. Leave anyway?";

export function useUnsavedGuard(active: boolean): void {
  useEffect(() => {
    if (!active) {
      return;
    }

    const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
      event.preventDefault();
      event.returnValue = confirmMessage;
    };

    const findTabLink = (target: EventTarget | null): HTMLAnchorElement | null => {
      if (!(target instanceof Element)) {
        return null;
      }
      const anchor = target.closest("a[href^='#']");
      return anchor instanceof HTMLAnchorElement ? anchor : null;
    };

    const handleCaptureClick = (event: MouseEvent): void => {
      const anchor = findTabLink(event.target);
      if (anchor === null || anchor.hash === window.location.hash) {
        return;
      }
      if (!window.confirm(confirmMessage)) {
        event.preventDefault();
        event.stopPropagation();
      }
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    document.addEventListener("click", handleCaptureClick, true);

    return () => {
      window.removeEventListener("beforeunload", handleBeforeUnload);
      document.removeEventListener("click", handleCaptureClick, true);
    };
  }, [active]);
}
