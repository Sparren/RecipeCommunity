import { useEffect, useId, useState } from "react";
import { api } from "../api";
import type { Ingredient } from "../types";

export function IngredientPicker({
  onSelect,
  label = "Sök ingrediens",
}: {
  onSelect: (value: Ingredient) => void;
  label?: string;
}) {
  const [query, setQuery] = useState("");
  const [items, setItems] = useState<Ingredient[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const [active, setActive] = useState(-1);
  const listId = useId();
  useEffect(() => {
    setActive(-1);
    if (!query.trim()) {
      setItems([]);
      setLoading(false);
      return;
    }
    const abort = new AbortController();
    setLoading(true);
    const timer = setTimeout(() => {
      api<Ingredient[]>("/ingredients?query=" + encodeURIComponent(query), {
        signal: abort.signal,
      })
        .then((data) => {
          setItems(data);
          setError("");
        })
        .catch((e) => {
          if (e.name !== "AbortError") setError(e.message);
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    }, 200);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [query]);
  function select(item: Ingredient) {
    onSelect(item);
    setQuery("");
    setItems([]);
  }
  return (
    <div className="picker">
      <label>
        {label}
        <input
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={!!query}
          aria-controls={listId}
          aria-activedescendant={
            active >= 0 ? `${listId}-${active}` : undefined
          }
          value={query}
          placeholder="Namn eller alias, t.ex. squash"
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "ArrowDown") {
              e.preventDefault();
              setActive((i) => Math.min(i + 1, items.length - 1));
            }
            if (e.key === "ArrowUp") {
              e.preventDefault();
              setActive((i) => Math.max(i - 1, 0));
            }
            if (e.key === "Enter") {
              e.preventDefault();
              if (active >= 0 && items[active]) select(items[active]);
            }
            if (e.key === "Escape") setQuery("");
          }}
        />
      </label>
      {query && (
        <div
          className="suggestions"
          id={listId}
          role="listbox"
          aria-label="Ingredienser"
        >
          {loading ? (
            <p>Söker…</p>
          ) : (
            items.map((item, index) => (
              <button
                type="button"
                role="option"
                id={`${listId}-${index}`}
                aria-selected={index === active}
                key={item.id}
                onClick={() => select(item)}
              >
                {item.name}
                <small>{item.aliases.map((a) => a.name).join(", ")}</small>
              </button>
            ))
          )}
          {!loading && !items.length && !error && (
            <p>
              Ingen träff. Lägg till ingrediensen i katalogen; fritext skapar
              ingen ingrediens.
            </p>
          )}
          {error && <p role="alert">{error}</p>}
        </div>
      )}
    </div>
  );
}
