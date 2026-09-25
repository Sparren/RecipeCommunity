import { useEffect, useState } from "react";
import { api } from "../../api";
import type { Ingredient } from "../../types";
import { IngredientPicker } from "../../components/IngredientPicker";

type EditIngredient = Pick<Ingredient, "names" | "aliases"> & { id?: string };
const blank = (): EditIngredient => ({
  names: [{ language: "sv", name: "" }],
  aliases: [],
});
export function IngredientAdmin({
  setDirty,
}: {
  setDirty: (dirty: boolean) => void;
}) {
  const [form, setForm] = useState<EditIngredient>(blank);
  const [baseline, setBaseline] = useState(form);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);
  const dirty = JSON.stringify(form) !== JSON.stringify(baseline);
  useEffect(() => {
    setDirty(dirty);
  }, [dirty, setDirty]);
  function load(value: EditIngredient) {
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
      const result = await api<{ id: string }>(
        form.id ? `/ingredients/${form.id}` : "/ingredients",
        { method: form.id ? "PUT" : "POST", body: JSON.stringify(form) },
      );
      const saved = await api<EditIngredient>(
        `/ingredients/${form.id ?? result.id}`,
      );
      setForm(saved);
      setBaseline(saved);
      setDirty(false);
      setNotice("Ingrediensen är sparad och kan väljas i recept.");
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
          <h1>Ingredienskatalog</h1>
          <p>En ingrediens, flera namn. Förberedelser hör hemma i receptet.</p>
        </div>
      </div>
      <section className="panel">
        <IngredientPicker
          label="Hitta en ingrediens att redigera"
          onSelect={(i) => load(i)}
        />
        <button onClick={() => load(blank())}>+ Ny ingrediens</button>
      </section>
      <form onSubmit={save} className="panel">
        <fieldset disabled={busy} className="plain-fieldset">
          <h2>{form.id ? "Redigera ingrediens" : "Ny kanonisk ingrediens"}</h2>
          <h3>Föredragna namn</h3>
          <p className="hint">
            Ett namn per språk. Använd språkkoder som sv och en.
          </p>
          {form.names.map((n, i) => (
            <div className="name-row" key={i}>
              <label>
                Språk
                <input
                  required
                  maxLength={35}
                  value={n.language}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      names: form.names.map((x, j) =>
                        i === j ? { ...x, language: e.target.value } : x,
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
                  value={n.name}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      names: form.names.map((x, j) =>
                        i === j ? { ...x, name: e.target.value } : x,
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
                    names: form.names.filter((_, j) => j !== i),
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
            Alternativa söknamn, till exempel courgette och squash. Språk är
            valfritt.
          </p>
          {form.aliases.map((a, i) => (
            <div className="name-row" key={i}>
              <label>
                Språk (valfritt)
                <input
                  maxLength={35}
                  value={a.language ?? ""}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      aliases: form.aliases.map((x, j) =>
                        i === j
                          ? { ...x, language: e.target.value || null }
                          : x,
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
                  value={a.name}
                  onChange={(e) =>
                    setForm({
                      ...form,
                      aliases: form.aliases.map((x, j) =>
                        i === j ? { ...x, name: e.target.value } : x,
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
                    aliases: form.aliases.filter((_, j) => j !== i),
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
                aliases: [...form.aliases, { language: null, name: "" }],
              })
            }
          >
            + Lägg till alias
          </button>
          {error && (
            <p className="error" role="alert">
              {error}
            </p>
          )}
          {notice && (
            <p className="success" role="status">
              {notice}
            </p>
          )}
          <div className="form-actions">
            <button type="submit" className="primary">
              {busy ? "Sparar…" : "Spara ingrediens"}
            </button>
          </div>
        </fieldset>
      </form>
    </>
  );
}
