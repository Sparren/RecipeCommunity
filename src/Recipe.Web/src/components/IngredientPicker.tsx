import { useEffect, useId, useState } from "react";
import { api } from "../api";
import type { Ingredient } from "../types";
import { QuickCreateIngredient } from "./QuickCreateIngredient";

export function IngredientPicker({
  onSelect,
  label = "Sök ingrediens",
  allowCreate = false,
}: {
  onSelect: (value: Ingredient) => void;
  label?: string;
  allowCreate?: boolean;
}) {
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<Ingredient[]>([]);
  const [error, setError] = useState("");
  const [settledQuery, setSettledQuery] = useState("");
  const [active, setActive] = useState(-1);
  const [creating, setCreating] = useState(false);
  const listId = useId();
  const searchable = Array.from(query.trim()).length >= 2;
  const ready = searchable && settledQuery === query;
  const canCreate = ready && !error && !items.length && allowCreate;
  useEffect(() => {
    setActive(-1);
    setItems([]);
    setError("");
    setSettledQuery("");
    if (!searchable) return;
    const abort = new AbortController();
    const timer = setTimeout(() => {
      api<Ingredient[]>(
        "/ingredients?query=" + encodeURIComponent(query.trim()),
        { signal: abort.signal },
      )
        .then((data) => {
          if (!abort.signal.aborted) {
            setItems(data);
            setSettledQuery(query);
          }
        })
        .catch((e) => {
          if (!abort.signal.aborted) {
            setError(e.message);
            setSettledQuery(query);
          }
        });
    }, 200);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [query, searchable]);
  function select(item: Ingredient) {
    onSelect(item);
    setQuery("");
    setItems([]);
    setCreating(false);
  }
  const optionCount = ready ? items.length + (canCreate ? 1 : 0) : 0;
  return (
    <div className="picker">
      <label>
        {label}
        <input
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={searchable && !creating}
          aria-controls={listId}
          aria-activedescendant={
            active >= 0 && active < optionCount
              ? `${listId}-${active}`
              : undefined
          }
          value={query}
          placeholder="Minst 2 tecken, namn eller alias"
          onChange={(e) => {
            setQuery(e.target.value);
            setActive(-1);
          }}
          onKeyDown={(e) => {
            if (e.key === "ArrowDown") {
              e.preventDefault();
              setActive((i) => Math.min(i + 1, optionCount - 1));
            }
            if (e.key === "ArrowUp") {
              e.preventDefault();
              setActive((i) => Math.max(i - 1, 0));
            }
            if (e.key === "Enter") {
              e.preventDefault();
              if (ready && active >= 0) {
                if (items[active]) select(items[active]);
                else if (canCreate) setCreating(true);
              }
            }
            if (e.key === "Escape") setQuery("");
          }}
        />
      </label>
      {query && !searchable && (
        <small>Skriv minst 2 tecken för att söka.</small>
      )}
      {searchable && !creating && (
        <div
          className="suggestions"
          id={listId}
          role="listbox"
          aria-label="Ingredienser"
        >
          {!ready ? (
            <p>Söker…</p>
          ) : (
            items.map((item, index) => (
              <button
                type="button"
                role="option"
                id={`${listId}-${index}`}
                aria-selected={active === index}
                key={item.id}
                onClick={() => select(item)}
              >
                {item.name}
                <small>
                  {item.aliases.map((a) => a.name).join(", ")}
                  {item.status === "PendingReview" && " · Väntar på granskning"}
                </small>
              </button>
            ))
          )}
          {ready && !items.length && !error && <p>Ingen träff.</p>}
          {canCreate && (
            <button
              type="button"
              role="option"
              id={`${listId}-0`}
              aria-selected={active === 0}
              onClick={() => setCreating(true)}
            >
              + Skapa ”{query.trim()}”
            </button>
          )}
          {error && <p role="alert">{error}</p>}
        </div>
      )}
      {creating && (
        <QuickCreateIngredient
          initialName={query.trim()}
          onCreated={select}
          onClose={() => setCreating(false)}
        />
      )}
    </div>
  );
}
