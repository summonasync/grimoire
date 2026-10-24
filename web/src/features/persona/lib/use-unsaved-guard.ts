"use client";

import { useEffect } from "react";

const confirmMessage = "You have unsaved persona changes. Leave anyway?";

let guardConfirm: (() => boolean) | null = null;

export function confirmLeaveUnsaved(): boolean {
  return guardConfirm === null || guardConfirm();
}

export function useUnsavedGuard(active: boolean): void {
  useEffect(() => {
    if (!active) {
      return;
    }

    guardConfirm = () => window.confirm(confirmMessage);

    const handleBeforeUnload = (event: BeforeUnloadEvent): void => {
      event.preventDefault();
      event.returnValue = confirmMessage;
    };

    const isGuardedClick = (target: EventTarget | null): boolean => {
      if (!(target instanceof Element)) {
        return false;
      }
      if (target.closest("[data-unsaved-guard]") !== null) {
        return true;
      }
      const anchor = target.closest<HTMLAnchorElement>("a[href^='#']");
      return anchor !== null && anchor.hash !== window.location.hash;
    };

    const handleCaptureClick = (event: MouseEvent): void => {
      if (!isGuardedClick(event.target)) {
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
      guardConfirm = null;
      window.removeEventListener("beforeunload", handleBeforeUnload);
      document.removeEventListener("click", handleCaptureClick, true);
    };
  }, [active]);
}
