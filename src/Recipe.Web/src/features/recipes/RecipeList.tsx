import { useEffect, useState } from "react";
import { api } from "../../api";
import type { Catalogue, Ingredient, Page, RecipeSummary } from "../../types";
import { IngredientPicker } from "../../components/IngredientPicker";
import { formatQuantity } from "./format";

export function RecipeList({
  catalogue,
  navigate,
}: {
  catalogue: Catalogue;
  navigate: (path: string) => void;
}) {
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [ingredients, setIngredients] = useState<Ingredient[]>([]);
  const [tags, setTags] = useState<string[]>([]);
  const [page, setPage] = useState(1);
  const [results, setResults] = useState<Page<RecipeSummary>>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  useEffect(() => {
    const abort = new AbortController();
    setLoading(true);
    const timer = setTimeout(() => {
      const query = new URLSearchParams({ name, page: String(page) });
      if (type) query.set("recipeTypeId", type);
      ingredients.forEach((i) => query.append("ingredientIds", i.id));
      tags.forEach((t) => query.append("tagIds", t));
      api<Page<RecipeSummary>>("/recipes?" + query, { signal: abort.signal })
        .then((r) => {
          setResults(r);
          setError("");
        })
        .catch((e) => {
          if (e.name !== "AbortError") setError(e.message);
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    }, 180);
    return () => {
      clearTimeout(timer);
      abort.abort();
    };
  }, [name, type, ingredients, tags, page]);
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">DIN EGEN RECEPTSAMLING</p>
          <h1>Vad vill du laga?</h1>
          <p>Spara idéerna. Hitta favoriterna. Laga något gott.</p>
        </div>
        <button className="primary" onClick={() => navigate("/new")}>
          + Nytt recept
        </button>
      </div>
      <section className="panel search-panel">
        <div className="columns">
          <label>
            Receptnamn
            <input
              type="search"
              placeholder="Sök bland dina recept"
              value={name}
              onChange={(e) => {
                setName(e.target.value);
                setPage(1);
              }}
            />
          </label>
          <label>
            Recepttyp
            <select
              value={type}
              onChange={(e) => {
                setType(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Alla typer</option>
              {catalogue.recipeTypes.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
          </label>
        </div>
        <IngredientPicker
          label="Ingredienser — alla valda ska finnas"
          onSelect={(item) => {
            setIngredients((old) =>
              old.some((i) => i.id === item.id) ? old : [...old, item],
            );
            setPage(1);
          }}
        />
        <div className="chips">
          {ingredients.map((i) => (
            <button
              key={i.id}
              onClick={() => {
                setIngredients(ingredients.filter((x) => x.id !== i.id));
                setPage(1);
              }}
              aria-label={`Ta bort filtret ${i.name}`}
            >
              {i.name} ×
            </button>
          ))}
        </div>
        <fieldset className="tags">
          <legend>Taggar — minst en vald ska finnas</legend>
          {catalogue.tags.map((t) => (
            <label key={t.id}>
              <input
                type="checkbox"
                checked={tags.includes(t.id)}
                onChange={(e) => {
                  setTags(
                    e.target.checked
                      ? [...tags, t.id]
                      : tags.filter((id) => id !== t.id),
                  );
                  setPage(1);
                }}
              />
              {t.name}
            </label>
          ))}
        </fieldset>
        <button
          className="quiet"
          onClick={() => {
            setName("");
            setType("");
            setIngredients([]);
            setTags([]);
            setPage(1);
          }}
        >
          Rensa filter
        </button>
      </section>
      {error && (
        <p className="error" role="alert">
          {error}
        </p>
      )}
      <div aria-live="polite">
        <p className="result-count">
          {loading ? "Söker…" : `${results?.total ?? 0} recept`}
        </p>
      </div>
      {!loading && !error && results?.total === 0 && (
        <section className="panel">
          <h2>Inga recept hittades</h2>
          <p>Prova färre ingredienser eller en annan tagg.</p>
        </section>
      )}
      <div className="recipe-grid" aria-busy={loading}>
        {results?.items.map((r) => (
          <button
            key={r.id}
            className="recipe-card"
            onClick={() => navigate(`/recipes/${r.id}`)}
          >
            {r.primaryImageUrl ? (
              <img src={r.primaryImageUrl} alt="" />
            ) : (
              <div className="recipe-placeholder" aria-hidden="true">
                {r.recipeTypeName}
              </div>
            )}
            <div className="card-body">
              <span className="eyebrow">
                {r.recipeTypeName}
                {r.publicationState === "Draft" && " · UTKAST"}
              </span>
              <h2>{r.name}</h2>
              <p>{r.description}</p>
              <div className="meta">
                {formatQuantity(r.yield.quantity)}{" "}
                {catalogue.units.find((u) => u.id === r.yield.unitId)?.symbol}
                {r.totalTimeMinutes && ` · ${r.totalTimeMinutes} min`}
              </div>
            </div>
          </button>
        ))}
      </div>
      {results && results.total > results.pageSize && (
        <nav className="pagination" aria-label="Resultatsidor">
          <button disabled={page === 1} onClick={() => setPage(page - 1)}>
            Föregående
          </button>
          <span>Sida {page}</span>
          <button
            disabled={page * results.pageSize >= results.total}
            onClick={() => setPage(page + 1)}
          >
            Nästa
          </button>
        </nav>
      )}
    </>
  );
}
