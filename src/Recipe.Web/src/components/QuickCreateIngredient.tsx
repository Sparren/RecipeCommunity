import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { api } from "../api";
import type { Ingredient } from "../types";

export function QuickCreateIngredient({
  initialName,
  onCreated,
  onClose,
  standalone = false,
}: {
  initialName: string;
  onCreated: (ingredient: Ingredient) => void;
  onClose: () => void;
  standalone?: boolean;
}) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [name, setName] = useState(initialName);
  const [language, setLanguage] = useState("sv");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const createdId = useRef<string | null>(null);
  useEffect(() => {
    const element = dialog.current!;
    const previous = document.activeElement as HTMLElement | null;
    element.showModal();
    return () => {
      element.close();
      previous?.focus();
    };
  }, []);
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    event.stopPropagation();
    setBusy(true);
    setError("");
    try {
      // If the subsequent read fails, retry that read rather than creating a duplicate.
      if (!createdId.current) {
        const result = await api<{ id: string }>("/ingredients", {
          method: "POST",
          body: JSON.stringify({ names: [{ language, name }], aliases: [] }),
        });
        createdId.current = result.id;
      }
      const ingredient = await api<Ingredient>(
        `/ingredients/${createdId.current}`,
      );
      onCreated(ingredient);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return createPortal(
    <dialog
      ref={dialog}
      className="ingredient-dialog"
      aria-labelledby="quick-create-title"
      onCancel={(e) => {
        e.preventDefault();
        if (!busy) onClose();
      }}
    >
      <form onSubmit={submit}>
        <h2 id="quick-create-title">Skapa ingrediens</h2>
        <p>
          {standalone
            ? "Skapa en ingrediens utan att påbörja ett recept. "
            : "Receptets osparade ändringar finns kvar. "}
          Ange ingrediensens namn, utan mängd eller förberedelse.
        </p>
        <fieldset
          disabled={busy || !!createdId.current}
          className="plain-fieldset"
        >
          <label>
            Föredraget namn
            <input
              autoFocus
              required
              maxLength={200}
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </label>
          <label>
            Språk
            <input
              required
              maxLength={35}
              value={language}
              onChange={(e) => setLanguage(e.target.value)}
              placeholder="sv"
            />
          </label>
        </fieldset>
        {error && (
          <p role="alert" className="error">
            {error}
          </p>
        )}
        <div className="form-actions">
          <button type="button" disabled={busy} onClick={onClose}>
            Avbryt
          </button>
          <button type="submit" className="primary" disabled={busy}>
            {busy
              ? "Skapar…"
              : standalone
                ? "Skapa ingrediens"
                : "Skapa och välj"}
          </button>
        </div>
      </form>
    </dialog>,
    document.body,
  );
}
