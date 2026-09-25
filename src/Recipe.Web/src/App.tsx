import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "./api";
import type { Catalogue, RecipeDetail } from "./types";
import { RecipeList } from "./features/recipes/RecipeList";
import { RecipeEditor } from "./features/recipes/RecipeEditor";
import { RecipeView } from "./features/recipes/RecipeView";
import { IngredientAdmin } from "./features/catalogue/IngredientAdmin";

export default function App() {
  const [catalogue, setCatalogue] = useState<Catalogue>();
  const [admin, setAdmin] = useState(false);
  const [path, setPath] = useState(location.hash.slice(1) || "/");
  const [recipe, setRecipe] = useState<RecipeDetail>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const dirty = useRef(false);
  const currentPath = useRef(path);
  const setDirty = useCallback((value: boolean) => {
    dirty.current = value;
  }, []);
  const navigate = useCallback((next: string, saved = false) => {
    if (
      !saved &&
      dirty.current &&
      !window.confirm("Lämna sidan och förlora osparade ändringar?")
    )
      return;
    dirty.current = false;
    currentPath.current = next;
    setPath(next);
    location.hash = next;
    window.scrollTo(0, 0);
  }, []);
  useEffect(() => {
    Promise.all([
      api<Catalogue>("/catalogue"),
      api<{ isAdministrator: boolean }>("/me"),
    ])
      .then(([c, u]) => {
        setCatalogue(c);
        setAdmin(u.isAdministrator);
      })
      .catch((e) => setError(e.message));
    const beforeUnload = (e: BeforeUnloadEvent) => {
      if (dirty.current) {
        e.preventDefault();
        e.returnValue = "";
      }
    };
    const changed = () => {
      const next = location.hash.slice(1) || "/";
      if (next === currentPath.current) return;
      if (
        dirty.current &&
        !window.confirm("Lämna sidan och förlora osparade ändringar?")
      ) {
        location.hash = currentPath.current;
        return;
      }
      dirty.current = false;
      currentPath.current = next;
      setPath(next);
    };
    window.addEventListener("beforeunload", beforeUnload);
    window.addEventListener("hashchange", changed);
    return () => {
      window.removeEventListener("beforeunload", beforeUnload);
      window.removeEventListener("hashchange", changed);
    };
  }, []);
  useEffect(() => {
    setError("");
    setRecipe(undefined);
    const match = path.match(/^\/recipes\/([^/]+)(?:\/edit)?$/);
    if (!match) {
      setLoading(false);
      return;
    }
    const abort = new AbortController();
    setLoading(true);
    api<RecipeDetail>(`/recipes/${match[1]}`, { signal: abort.signal })
      .then(setRecipe)
      .catch((e) => {
        if (e.name !== "AbortError") setError(e.message);
      })
      .finally(() => {
        if (!abort.signal.aborted) setLoading(false);
      });
    return () => abort.abort();
  }, [path]);
  return (
    <>
      <header>
        <button className="brand" onClick={() => navigate("/")}>
          Recipe<span>RECEPTSAMLING</span>
        </button>
        <nav aria-label="Huvudmeny">
          <button
            className={path === "/" ? "active" : ""}
            onClick={() => navigate("/")}
          >
            Recept
          </button>
          {admin && (
            <button
              className={path === "/ingredients" ? "active" : ""}
              onClick={() => navigate("/ingredients")}
            >
              Ingredienskatalog
            </button>
          )}
        </nav>
        <span className="local-badge">LOKAL · 0.1</span>
      </header>
      <main>
        {error && (
          <p role="alert" className="error">
            {error}
            <br />
            <button onClick={() => location.reload()}>Försök igen</button>
          </p>
        )}
        {!catalogue && !error && <p role="status">Läser katalogen…</p>}
        {catalogue && !error && (
          <>
            {path === "/" ? (
              <RecipeList catalogue={catalogue} navigate={navigate} />
            ) : path === "/new" ? (
              <RecipeEditor
                key="new"
                catalogue={catalogue}
                setDirty={setDirty}
                onSaved={(id) => navigate(`/recipes/${id}`, true)}
                onCancel={() => navigate("/")}
              />
            ) : path === "/ingredients" ? (
              admin ? (
                <IngredientAdmin setDirty={setDirty} />
              ) : (
                <p>
                  Endast administratören har tillgång till katalogändringar.
                </p>
              )
            ) : loading ? (
              <p role="status">Läser receptet…</p>
            ) : recipe ? (
              path.endsWith("/edit") ? (
                <RecipeEditor
                  key={path}
                  recipe={recipe}
                  catalogue={catalogue}
                  setDirty={setDirty}
                  onSaved={(id) => navigate(`/recipes/${id}`, true)}
                  onCancel={() => navigate(`/recipes/${recipe.id}`)}
                />
              ) : (
                <RecipeView
                  recipe={recipe}
                  catalogue={catalogue}
                  navigate={navigate}
                />
              )
            ) : (
              <p>Sidan hittades inte.</p>
            )}
          </>
        )}
      </main>
      <footer>Recipe 0.1 · Ett kök fullt av möjligheter</footer>
    </>
  );
}
