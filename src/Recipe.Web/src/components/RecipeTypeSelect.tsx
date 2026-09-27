import { useState } from "react";
import type { CatalogueLookup } from "../types";

export function matchesCatalogue(item: CatalogueLookup, query: string) {
  const text = query.trim().toLocaleLowerCase("sv");
  return [
    item.name,
    ...(item.names ?? []).map((n) => n.name),
    ...(item.aliases ?? []).map((a) => a.name),
  ].some((name) => name.toLocaleLowerCase("sv").includes(text));
}

export function RecipeTypeSelect({
  items,
  value,
  onChange,
  required = false,
  emptyLabel = "Välj recepttyp",
}: {
  items: CatalogueLookup[];
  value: string;
  onChange: (id: string) => void;
  required?: boolean;
  emptyLabel?: string;
}) {
  const [query, setQuery] = useState("");
  const matches = items.filter((item) => matchesCatalogue(item, query));
  return (
    <div>
      <label>
        Sök recepttyp
        <input
          type="search"
          placeholder="Namn, översättning eller alias"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
      </label>
      <label>
        Recepttyp
        <select
          required={required}
          value={value}
          onChange={(e) => onChange(e.target.value)}
        >
          <option value="">{emptyLabel}</option>
          {items
            .filter((item) => item.id === value || matches.includes(item))
            .map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
                {item.isActive ? "" : " (inaktiv)"}
              </option>
            ))}
        </select>
      </label>
      {query && (
        <small role="status">
          {matches.length} träffar. Vald recepttyp behålls tills du väljer en
          annan.
        </small>
      )}
    </div>
  );
}
