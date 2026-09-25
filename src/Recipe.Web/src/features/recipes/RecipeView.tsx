import type { Catalogue, RecipeDetail } from "../../types";
import { formatQuantity } from "./format";
export function RecipeView({
  recipe: r,
  catalogue,
  navigate,
}: {
  recipe: RecipeDetail;
  catalogue: Catalogue;
  navigate: (path: string) => void;
}) {
  const unit = (id: string | null) =>
    catalogue.units.find((u) => u.id === id)?.symbol ?? "";
  return (
    <>
      <button className="quiet" onClick={() => navigate("/")}>
        ← Alla recept
      </button>
      <div className="page-heading">
        <div>
          <p className="eyebrow">
            {catalogue.recipeTypes.find((t) => t.id === r.recipeTypeId)?.name} ·{" "}
            {r.publicationState === "Draft" ? "UTKAST" : "PUBLICERAT LOKALT"}
          </p>
          <h1>{r.name}</h1>
          <p className="lead prose">{r.description}</p>
          <p className="meta">
            {formatQuantity(r.yield.quantity)} {unit(r.yield.unitId)}
            {r.totalTimeMinutes && ` · ${r.totalTimeMinutes} min`} · Av{" "}
            {r.authorName}
          </p>
        </div>
        <button
          className="primary"
          onClick={() => navigate(`/recipes/${r.id}/edit`)}
        >
          Redigera recept
        </button>
      </div>
      <div className="chips">
        {r.tagIds.map((id) => (
          <span key={id}>{catalogue.tags.find((t) => t.id === id)?.name}</span>
        ))}
      </div>
      {r.images.find((i) => i.isPrimary) && (
        <img
          className="hero-image"
          src={r.images.find((i) => i.isPrimary)!.url}
          alt={r.images.find((i) => i.isPrimary)!.caption ?? r.name}
        />
      )}
      <div className="detail-columns">
        <section className="panel">
          <h2>Ingredienser</h2>
          {r.ingredientGroups.map((g) => (
            <div key={g.id}>
              {g.heading?.trim() && <h3>{g.heading}</h3>}
              <ul className="ingredients">
                {g.ingredients.map((i, index) => (
                  <li key={i.id ?? index}>
                    <span className="amount">
                      {formatQuantity(i.quantity)} {unit(i.unitId)}
                    </span>
                    <div>
                      <strong>{i.ingredientName}</strong>
                      {i.preparation && `, ${i.preparation}`}
                      {i.amountDescription && (
                        <small>{i.amountDescription}</small>
                      )}
                      {i.note && <small>{i.note}</small>}
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </section>
        <section className="panel">
          <h2>Gör så här</h2>
          {r.cookingSteps.length ? (
            <ol className="steps">
              {r.cookingSteps.map((s) => (
                <li key={s.id} className="prose">
                  {s.instruction}
                </li>
              ))}
            </ol>
          ) : (
            <p>Inga steg ännu.</p>
          )}
        </section>
      </div>
      {!!r.images.length && (
        <section className="panel">
          <h2>Bilder</h2>
          <div className="image-grid">
            {r.images.map((i) => (
              <figure key={i.id}>
                <img src={i.url} alt={i.caption ?? r.name} />
                <figcaption>{i.caption}</figcaption>
              </figure>
            ))}
          </div>
        </section>
      )}
    </>
  );
}
