import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "./api";
import type { Catalogue, RecipeDetail } from "./types";
import { RecipeList } from "./features/recipes/RecipeList";
import { RecipeEditor } from "./features/recipes/RecipeEditor";
import { RecipeView } from "./features/recipes/RecipeView";
import { IngredientAdmin } from "./features/catalogue/IngredientAdmin";
import { TagTypeAdmin } from "./features/catalogue/TagTypeAdmin";
import { IngredientWorkspace } from "./features/catalogue/IngredientWorkspace";

type CurrentUser = {
  id: string;
  displayName: string;
  isAdministrator: boolean;
};
// Retain existing bookmarks while placing editing and catalogue work in their activity modes.
function canonicalPath(path: string) {
  if (path === "/new") return "/create/new";
  if (/^\/recipes\/[^/]+\/edit$/.test(path)) return "/create" + path;
  if (["/ingredients", "/tags", "/recipe-types"].includes(path))
    return "/admin" + path;
  return path;
}

export default function App() {
  const [catalogue, setCatalogue] = useState<Catalogue>();
  const [user, setUser] = useState<CurrentUser>();
  const [path, setPath] = useState(
    canonicalPath(location.hash.slice(1) || "/"),
  );
  const [recipe, setRecipe] = useState<RecipeDetail>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const dirty = useRef(false);
  const currentPath = useRef(path);
  const refreshCatalogue = useCallback(async () => {
    setCatalogue(await api<Catalogue>("/catalogue"));
  }, []);
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
    const target = canonicalPath(next);
    dirty.current = false;
    currentPath.current = target;
    setPath(target);
    location.hash = target;
    window.scrollTo(0, 0);
  }, []);
  useEffect(() => {
    Promise.all([api<Catalogue>("/catalogue"), api<CurrentUser>("/me")])
      .then(([c, u]) => {
        setCatalogue(c);
        setUser(u);
      })
      .catch((e) => setError(e.message));
    const beforeUnload = (e: BeforeUnloadEvent) => {
      if (dirty.current) {
        e.preventDefault();
        e.returnValue = "";
      }
    };
    const changed = () => {
      const next = canonicalPath(location.hash.slice(1) || "/");
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
    const match = path.match(/^(?:\/create)?\/recipes\/([^/]+)(?:\/edit)?$/);
    if (!match) {
      setLoading(false);
      return;
    }
    const abort = new AbortController();
    setLoading(true);
    api<RecipeDetail>("/recipes/" + match[1], { signal: abort.signal })
      .then((value) => {
        if (abort.signal.aborted) return;
        if (
          value.publicationState === "Draft" &&
          !path.startsWith("/create/")
        ) {
          navigate("/create/recipes/" + value.id, true);
          return;
        }
        setRecipe(value);
      })
      .catch((e) => {
        if (e.name !== "AbortError") setError(e.message);
      })
      .finally(() => {
        if (!abort.signal.aborted) setLoading(false);
      });
    return () => abort.abort();
  }, [path, navigate]);
  const creator = path === "/create" || path.startsWith("/create/");
  const administration = path === "/admin" || path.startsWith("/admin/");
  const admin = user?.isAdministrator === true;
  const editAllowed = recipe?.authorId === user?.id;
  return (
    <>
      <header>
        <button className="brand" onClick={() => navigate("/")}>
          Recipe<span>RECEPTSAMLING</span>
        </button>
        <nav aria-label="Aktivitetslägen">
          <button
            className={!creator && !administration ? "active" : ""}
            aria-current={!creator && !administration ? "page" : undefined}
            onClick={() => navigate("/")}
          >
            Hitta &amp; laga
          </button>
          <button
            className={creator ? "active" : ""}
            aria-current={creator ? "page" : undefined}
            onClick={() => navigate("/create")}
          >
            Skapa &amp; utforska
          </button>
          {admin && (
            <button
              className={administration ? "active" : ""}
              aria-current={administration ? "page" : undefined}
              onClick={() => navigate("/admin")}
            >
              Administration
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
        {(!catalogue || !user) && !error && (
          <p role="status">Läser katalogen…</p>
        )}
        {catalogue && user && !error && (
          <>
            {creator && (
              <nav className="mode-navigation" aria-label="Skapa och utforska">
                <button onClick={() => navigate("/create")}>Mina recept</button>
                <button onClick={() => navigate("/create/ingredients")}>
                  Ingredienser
                </button>
              </nav>
            )}
            {administration && admin && path !== "/admin" && (
              <nav
                className="mode-navigation"
                aria-label="Katalogadministration"
              >
                <button onClick={() => navigate("/admin/ingredients")}>
                  Ingredienskatalog
                </button>
                <button onClick={() => navigate("/admin/tags")}>Taggar</button>
                <button onClick={() => navigate("/admin/recipe-types")}>
                  Recepttyper
                </button>
              </nav>
            )}
            {path === "/" || path === "/create" ? (
              <RecipeList
                key={path}
                scope={creator ? "Mine" : "Published"}
                catalogue={catalogue}
                navigate={navigate}
              />
            ) : path === "/create/new" ? (
              <RecipeEditor
                key="new"
                catalogue={catalogue}
                setDirty={setDirty}
                onSaved={(id) => navigate("/create/recipes/" + id, true)}
                onCancel={() => navigate("/create")}
              />
            ) : path === "/create/ingredients" ? (
              <IngredientWorkspace administrator={admin} />
            ) : administration ? (
              !admin ? (
                <p role="alert">
                  Endast administratören har tillgång till katalogändringar.
                </p>
              ) : path === "/admin" ? (
                <>
                  <div className="page-heading">
                    <div>
                      <p className="eyebrow">ADMINISTRATION</p>
                      <h1>Underhåll katalogerna</h1>
                      <p>
                        Hantera gemensamma namn, alias och kataloguppgifter.
                      </p>
                    </div>
                  </div>
                  <div className="administration-grid">
                    <section className="panel">
                      <h2>Ingredienser</h2>
                      <p>
                        Underhåll namn, översättningar och alias i
                        ingredienskatalogen.
                      </p>
                      <button onClick={() => navigate("/admin/ingredients")}>
                        Ingredienskatalog
                      </button>
                    </section>
                    <section className="panel">
                      <h2>Taggar</h2>
                      <p>
                        Hantera översättningar, alias och aktiva eller inaktiva
                        taggar.
                      </p>
                      <button onClick={() => navigate("/admin/tags")}>
                        Taggar
                      </button>
                    </section>
                    <section className="panel">
                      <h2>Recepttyper</h2>
                      <p>
                        Hantera typernas namn, alias, ordning och aktiva status.
                      </p>
                      <button onClick={() => navigate("/admin/recipe-types")}>
                        Recepttyper
                      </button>
                    </section>
                  </div>
                </>
              ) : path === "/admin/ingredients" ? (
                <IngredientAdmin setDirty={setDirty} />
              ) : path === "/admin/tags" || path === "/admin/recipe-types" ? (
                <TagTypeAdmin
                  key={path}
                  kind={path === "/admin/tags" ? "tags" : "recipe-types"}
                  setDirty={setDirty}
                  onSaved={refreshCatalogue}
                />
              ) : (
                <p>Sidan hittades inte.</p>
              )
            ) : loading ? (
              <p role="status">Läser receptet…</p>
            ) : recipe ? (
              path.endsWith("/edit") ? (
                editAllowed ? (
                  <RecipeEditor
                    key={path}
                    recipe={recipe}
                    catalogue={catalogue}
                    setDirty={setDirty}
                    onSaved={(id) => navigate("/create/recipes/" + id, true)}
                    onCancel={() => navigate("/create/recipes/" + recipe.id)}
                  />
                ) : (
                  <p role="alert">Du kan bara redigera dina egna recept.</p>
                )
              ) : (
                <RecipeView
                  recipe={recipe}
                  catalogue={catalogue}
                  navigate={navigate}
                  creatorMode={creator}
                  canEdit={editAllowed}
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
