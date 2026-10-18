"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/client";
import { updateSettings } from "../api";
import { settingsKeys, settingsQueryOptions } from "../queries";
import type { ApiFailure, SettingsDraft, SettingsFieldKey, SettingsUpdateOutcome } from "../types";
import { draftFromSettings, draftsEqual, draftToPayload, validateDraft } from "./settings-draft";

function toFailure(error: Error | null): ApiFailure | null {
  if (error === null) {
    return null;
  }
  if (error instanceof ApiError) {
    return { code: error.code, message: error.message };
  }
  return { code: "network_error", message: error.message || "The request failed." };
}

export function useSettingsForm() {
  const queryClient = useQueryClient();
  const query = useQuery(settingsQueryOptions());
  const [draft, setDraft] = useState<SettingsDraft | null>(null);
  const dirtyRef = useRef(false);

  useEffect(() => {
    if (query.data === undefined || dirtyRef.current) {
      return;
    }
    setDraft(draftFromSettings(query.data));
  }, [query.data]);

  const isDirty = draft !== null
    && query.data !== undefined
    && !draftsEqual(draft, draftFromSettings(query.data));

  useEffect(() => {
    dirtyRef.current = isDirty;
  }, [isDirty]);

  const updateMutation = useMutation({ mutationFn: updateSettings });

  const errors: Partial<Record<SettingsFieldKey, string>> = draft === null ? {} : validateDraft(draft);
  const isValid = Object.keys(errors).length === 0;

  const setField = (key: SettingsFieldKey, value: string): void => {
    setDraft((current) => {
      if (current === null) {
        return current;
      }
      return { ...current, [key]: value };
    });
  };

  const save = (): void => {
    if (draft === null || !isDirty || !isValid || updateMutation.isPending) {
      return;
    }
    updateMutation.mutate(draftToPayload(draft), {
      onSuccess: (outcome) => {
        if (outcome.status === "applied") {
          void queryClient.invalidateQueries({ queryKey: settingsKeys.all });
        }
      },
    });
  };

  const discard = (): void => {
    if (!isDirty) {
      return;
    }
    if (!window.confirm("Revert the edited settings?")) {
      return;
    }
    setDraft(query.data === undefined ? null : draftFromSettings(query.data));
    updateMutation.reset();
  };

  return {
    settings: query.data,
    draft,
    errors,
    isDirty,
    isValid,
    isSaving: updateMutation.isPending,
    outcome: (updateMutation.data ?? null) as SettingsUpdateOutcome | null,
    loadError: toFailure(query.error),
    isLoading: query.isPending,
    setField,
    save,
    discard,
    reload: () => {
      void query.refetch();
    },
  };
}
