"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ApiError } from "@/shared/api/client";
import type { PersonaDocument } from "@/shared/api/types";
import { updatePersona } from "../api";
import { personaKeys, personaQueryOptions } from "../queries";
import type { ApiFailure, PersonaSectionKey } from "../types";
import { documentsEqual, personaSectionLimit } from "./persona-document";
import { useUnsavedGuard } from "./use-unsaved-guard";

function toFailure(error: Error | null): ApiFailure | null {
  if (error === null) {
    return null;
  }
  if (error instanceof ApiError) {
    return { code: error.code, message: error.message };
  }
  return { code: "network_error", message: error.message || "The request failed." };
}

export function usePersonaEditor() {
  const queryClient = useQueryClient();
  const query = useQuery(personaQueryOptions());
  const [draft, setDraft] = useState<PersonaDocument | null>(null);
  const [savedTome, setSavedTome] = useState<string | null>(null);
  const dirtyRef = useRef(false);

  useEffect(() => {
    if (query.data === undefined || dirtyRef.current) {
      return;
    }
    setDraft(query.data);
  }, [query.data]);

  const isDirty = draft !== null && query.data !== undefined && !documentsEqual(draft, query.data);

  useEffect(() => {
    dirtyRef.current = isDirty;
  }, [isDirty]);

  useUnsavedGuard(isDirty);

  const saveMutation = useMutation({
    mutationFn: (document: PersonaDocument) => updatePersona(document),
    onSuccess: (saved) => {
      setDraft(saved.document);
      setSavedTome(saved.tome);
      queryClient.setQueryData(personaKeys.document(), saved.document);
    },
  });

  const setSection = (key: PersonaSectionKey, value: string): void => {
    setDraft((current) => {
      if (current === null) {
        return current;
      }
      return { ...current, [key]: value.slice(0, personaSectionLimit) };
    });
  };

  const save = (): void => {
    if (draft === null || !isDirty || saveMutation.isPending) {
      return;
    }
    saveMutation.mutate(draft);
  };

  const discard = (): void => {
    if (!isDirty) {
      return;
    }
    if (!window.confirm("Discard unsaved persona changes?")) {
      return;
    }
    setDraft(query.data ?? null);
    saveMutation.reset();
  };

  return {
    draft,
    isDirty,
    savedTome,
    isSaving: saveMutation.isPending,
    isSaved: saveMutation.isSuccess && !isDirty,
    saveError: toFailure(saveMutation.error),
    loadError: toFailure(query.error),
    isLoading: query.isPending,
    setSection,
    save,
    discard,
    reload: () => {
      void query.refetch();
    },
  };
}
