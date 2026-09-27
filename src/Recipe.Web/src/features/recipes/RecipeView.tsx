import type { Catalogue, Group, RecipeDetail } from "../../types";
import { useEffect, useRef } from "react";
import { formatQuantity } from "./format";
import "./recipe-print.css";

function keepGroupTogether(group: Group) {
  const text = [
    group.heading ?? "",
    ...group.ingredients.map((i) =>
      [i.ingredientName, i.preparation, i.amountDescription, i.note]
        .filter(Boolean)
        .join(" "),
    ),
  ].join("\n");
  return (
    group.ingredients.length <= 8 &&
    text.length <= 700 &&
    text.split("\n").length <= 12
  );
}
export function RecipeView({
  recipe: r,
  catalogue,
  navigate,
  canEdit = false,
  creatorMode = false,
}: {
  recipe: RecipeDetail;
  catalogue: Catalogue;
  navigate: (path: string) => void;
  canEdit?: boolean;
  creatorMode?: boolean;
}) {
  const unit = (id: string | null) =>
    catalogue.units.find((u) => u.id === id)?.symbol ?? "";
  const primaryImage = useRef<HTMLImageElement>(null);
  useEffect(() => {
    const previous = document.title;
    document.title = r.name || "Namnlöst recept";
    return () => {
      document.title = previous;
    };
  }, [r.name]);
  async function print() {
    await document.fonts.ready;
    await primaryImage.current?.decode().catch(() => {});
    window.print();
  }
  return (
    <article className="recipe-detail">
      <button
        className="quiet"
        onClick={() => navigate(creatorMode ? "/create" : "/")}
      >
        {creatorMode ? "← Mina recept" : "← Alla recept"}
      </button>
      <div className="page-heading">
        <div>
          <p className="eyebrow">
            {catalogue.recipeTypes.find((t) => t.id === r.recipeTypeId)?.name} ·{" "}
            {r.publicationState === "Draft" ? "UTKAST" : "PUBLICERAT LOKALT"}
          </p>
          <h1>{r.name || "Namnlöst recept"}</h1>
          <p className="lead prose">{r.description}</p>
          <p className="meta">
            {r.yield && (
              <>
                Ger: {formatQuantity(r.yield.quantity)} {unit(r.yield.unitId)}
              </>
            )}
            {r.totalTimeMinutes && ` · ${r.totalTimeMinutes} min`} · Av{" "}
            {r.authorName}
          </p>
        </div>
        <div className="recipe-actions">
          <button onClick={print}>Skriv ut</button>
          {canEdit && (
            <button
              className="quiet"
              onClick={() => navigate(`/create/recipes/${r.id}/edit`)}
            >
              Redigera recept
            </button>
          )}
        </div>
      </div>
      <div className="chips">
        {r.tagIds.map((id) => (
          <span key={id}>{catalogue.tags.find((t) => t.id === id)?.name}</span>
        ))}
      </div>
      {r.images.find((i) => i.isPrimary) && (
        <img
          ref={primaryImage}
          className="hero-image"
          src={r.images.find((i) => i.isPrimary)!.url}
          alt={r.images.find((i) => i.isPrimary)!.caption ?? r.name}
        />
      )}
      <div className="detail-columns">
        <section className="panel">
          <h2>Ingredienser</h2>
          {r.ingredientGroups.map((g) => (
            <div
              key={g.id}
              className={`print-ingredient-group ${keepGroupTogether(g) ? "print-keep-together" : ""}`}
            >
              {g.heading?.trim() && <h3>{g.heading}</h3>}
              <ul className="ingredients">
                {g.ingredients.map((i, index) => (
                  <li
                    key={i.id ?? index}
                    className={
                      [
                        i.ingredientName,
                        i.preparation,
                        i.amountDescription,
                        i.note,
                      ].join(" ").length <= 300
                        ? "print-keep-together"
                        : ""
                    }
                  >
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
                <li
                  key={s.id}
                  className={`prose ${s.instruction.length <= 650 && s.instruction.split("\n").length <= 8 ? "print-keep-together" : ""}`}
                >
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
        <section className="panel recipe-gallery">
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
    </article>
  );
}
