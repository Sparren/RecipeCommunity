import { useEffect, useState } from "react";
import { api } from "../../api";
import type { ManagedCatalogueEntry } from "../../types";

type EditEntry = {
  id?: string;
  names: ManagedCatalogueEntry["names"];
  aliases: NonNullable<ManagedCatalogueEntry["aliases"]>;
  isActive: boolean;
  sortOrder: number;
};
const blank = (): EditEntry => ({
  names: [{ language: "sv", name: "" }],
  aliases: [],
  isActive: true,
  sortOrder: 0,
});

// Tags and recipe types share this small translated-name form; their APIs remain explicit.
export function TagTypeAdmin({
  kind,
  setDirty,
  onSaved,
}: {
  kind: "tags" | "recipe-types";
  setDirty: (dirty: boolean) => void;
  onSaved: () => Promise<void>;
}) {
  const isType = kind === "recipe-types";
  const [form, setForm] = useState<EditEntry>(blank);
  const [baseline, setBaseline] = useState(form);
  const [items, setItems] = useState<ManagedCatalogueEntry[]>([]);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("");
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [listError, setListError] = useState("");
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const dirty = JSON.stringify(form) !== JSON.stringify(baseline);
  useEffect(() => {
    setDirty(dirty);
  }, [dirty, setDirty]);
  useEffect(() => {
    const abort = new AbortController();
    setLoading(true);
    setListError("");
    const timer = setTimeout(() => {
      const params = new URLSearchParams({ language: "sv" });
      params.set("query", query);
      if (status) params.set("isActive", status);
      api<ManagedCatalogueEntry[]>(`/${kind}?${params}`, {
        signal: abort.signal,
      })
        .then((data) => {
          if (!abort.signal.aborted) setItems(data);
        })
        .catch((e) => {
          if (!abort.signal.aborted) setListError(e.message);
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    }, 150);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [kind, isType, query, status, revision]);
  function load(value: EditEntry) {
    if (dirty && !window.confirm("Lämna osparade katalogändringar?")) return;
    setForm(value);
    setBaseline(value);
    setError("");
    setNotice("");
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    setBusy(true);
    setError("");
    setNotice("");
    try {
      const input = isType
        ? {
            names: form.names,
            aliases: form.aliases,
            isActive: form.isActive,
            sortOrder: form.sortOrder,
          }
        : { names: form.names, aliases: form.aliases, isActive: form.isActive };
      const result = await api<{ id: string }>(
        `/${kind}${form.id ? `/${form.id}` : ""}`,
        { method: form.id ? "PUT" : "POST", body: JSON.stringify(input) },
      );
      const saved = { ...form, id: result.id };
      setForm(saved);
      setBaseline(saved);
      setDirty(false);
      setRevision((r) => r + 1);
      setNotice(isType ? "Recepttypen är sparad." : "Taggen är sparad.");
      await onSaved();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">ADMINISTRATION</p>
          <h1>{isType ? "Recepttyper" : "Taggar"}</h1>
          <p>
            Flera språk beskriver samma post. Inaktiva poster finns kvar på
            befintliga recept.
          </p>
        </div>
      </div>
      <section className="panel">
        <fieldset disabled={busy} className="plain-fieldset">
          <div className="columns">
            {
              <label>
                {isType ? "Sök recepttyper" : "Sök taggar"}
                <input
                  type="search"
                  placeholder="Namn, översättning eller alias"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                />
              </label>
            }
            <label>
              Statusfilter
              <select
                value={status}
                onChange={(e) => setStatus(e.target.value)}
              >
                <option value="">Alla</option>
                <option value="true">Aktiva</option>
                <option value="false">Inaktiva</option>
              </select>
            </label>
          </div>
          <button onClick={() => load(blank())}>
            {isType ? "+ Ny recepttyp" : "+ Ny tagg"}
          </button>
          {listError && (
            <p role="alert" className="error">
              {listError}
            </p>
          )}
          <p aria-live="polite">
            {loading ? "Läser katalogen…" : `${items.length} poster`}
          </p>
          {!loading && !listError && (
            <ul
              className="catalogue-list"
              aria-label={isType ? "Recepttypernas lista" : "Taggarnas lista"}
            >
              {items.map((item) => (
                <li key={item.id}>
                  <button
                    aria-pressed={form.id === item.id}
                    onClick={() =>
                      load({
                        id: item.id,
                        names: item.names,
                        aliases: item.aliases ?? [],
                        isActive: item.isActive,
                        sortOrder: item.sortOrder ?? 0,
                      })
                    }
                  >
                    <span>{item.name}</span>
                    <small>
                      {isType ? `Ordning ${item.sortOrder} · ` : ""}
                      {item.isActive ? "Aktiv" : "Inaktiv"}
                    </small>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </fieldset>
      </section>
      <form onSubmit={save} className="panel">
        <fieldset disabled={busy} className="plain-fieldset">
          <h2>
            {form.id
              ? isType
                ? "Redigera recepttyp"
                : "Redigera tagg"
              : isType
                ? "Ny recepttyp"
                : "Ny tagg"}
          </h2>
          <label>
            Status
            <select
              value={String(form.isActive)}
              onChange={(e) =>
                setForm({ ...form, isActive: e.target.value === "true" })
              }
            >
              <option value="true">Aktiv</option>
              <option value="false">Inaktiv</option>
            </select>
          </label>
          {isType && (
            <label>
              Sorteringsordning
              <input
                type="number"
                required
                min={0}
                max={2147483647}
                step={1}
                value={form.sortOrder}
                onChange={(e) =>
                  setForm({ ...form, sortOrder: Number(e.target.value) })
                }
              />
              <span className="hint">
                Lägre tal visas först. Samma tal sorteras alfabetiskt.
              </span>
            </label>
          )}
          <h3>Föredragna namn</h3>
          <p className="hint">
            Ett namn per språk. Använd språkkoder som sv och en.
          </p>
          {form.names.map((name, i) => (
            <div className="name-row" key={i}>
              <label>
                Språk
                <input
                  required
                  maxLength={35}
                  value={name.language}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      names: form.names.map((n, j) =>
                        i === j ? { ...n, language: e.target.value } : n,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Namn
                <input
                  required
                  maxLength={200}
                  value={name.name}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      names: form.names.map((n, j) =>
                        i === j ? { ...n, name: e.target.value } : n,
                      ),
                    })
                  }
                />
              </label>
              <button
                type="button"
                disabled={form.names.length === 1}
                onClick={() =>
                  setForm({
                    ...form,
                    names: form.names.filter((_, j) => i !== j),
                  })
                }
              >
                Ta bort namn
              </button>
            </div>
          ))}
          <button
            type="button"
            onClick={() =>
              setForm({
                ...form,
                names: [...form.names, { language: "", name: "" }],
              })
            }
          >
            + Lägg till språk
          </button>
          <h3>Alias</h3>
          <p className="hint">
            Alternativa namn för samma begrepp, inte närliggande begrepp. Flera
            alias per språk är möjliga; ange språk för varje alias.
          </p>
          {form.aliases.map((alias, i) => (
            <div className="name-row" key={i}>
              <label>
                Aliasspråk
                <input
                  required
                  maxLength={35}
                  value={alias.language}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      aliases: form.aliases.map((a, j) =>
                        i === j ? { ...a, language: e.target.value } : a,
                      ),
                    })
                  }
                />
              </label>
              <label>
                Alias
                <input
                  required
                  maxLength={200}
                  value={alias.name}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      aliases: form.aliases.map((a, j) =>
                        i === j ? { ...a, name: e.target.value } : a,
                      ),
                    })
                  }
                />
              </label>
              <button
                type="button"
                onClick={() =>
                  setForm({
                    ...form,
                    aliases: form.aliases.filter((_, j) => i !== j),
                  })
                }
              >
                Ta bort alias
              </button>
            </div>
          ))}
          <button
            type="button"
            onClick={() =>
              setForm({
                ...form,
                aliases: [...form.aliases, { language: "sv", name: "" }],
              })
            }
          >
            + Lägg till alias
          </button>
          {error && (
            <p role="alert" className="error">
              {error}
            </p>
          )}
          {notice && (
            <p role="status" className="success">
              {notice}
            </p>
          )}
          <div className="form-actions">
            <button type="submit" className="primary">
              {busy ? "Sparar…" : isType ? "Spara recepttyp" : "Spara tagg"}
            </button>
          </div>
        </fieldset>
      </form>
    </>
  );
}
