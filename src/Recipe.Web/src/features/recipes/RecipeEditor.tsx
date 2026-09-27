import { useEffect, useState } from "react";
import { api } from "../../api";
import type {
  Catalogue,
  Entry,
  Group,
  RecipeDetail,
  RecipeForm,
} from "../../types";
import { IngredientPicker } from "../../components/IngredientPicker";
import { QuantityFields } from "../../components/QuantityFields";
import { move, OrderButtons } from "../../components/OrderButtons";
import {
  matchesCatalogue,
  RecipeTypeSelect,
} from "../../components/RecipeTypeSelect";
import { ImageEditor } from "./ImageEditor";

export function RecipeEditor({
  recipe,
  catalogue,
  onSaved,
  onCancel,
  setDirty,
}: {
  recipe?: RecipeDetail;
  catalogue: Catalogue;
  onSaved: (id: string) => void;
  onCancel: () => void;
  setDirty: (dirty: boolean) => void;
}) {
  const initial: RecipeForm = recipe ?? {
    name: "",
    description: "",
    recipeTypeId: null,
    yield: null,
    totalTimeMinutes: null,
    publicationState: "Draft",
    tagIds: [],
    ingredientGroups: [{ heading: "", ingredients: [] }],
    cookingSteps: [],
  };
  const [form, setForm] = useState<RecipeForm>(initial);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const [tagQuery, setTagQuery] = useState("");
  const [imageDirty, setImageDirty] = useState(false);
  const contentDirty = JSON.stringify(form) !== JSON.stringify(initial);
  const pendingIngredients = [
    ...new Map(
      form.ingredientGroups
        .flatMap((g) => g.ingredients)
        .filter((i) => i.ingredientStatus === "PendingReview")
        .map((i) => [i.ingredientId, i.ingredientName]),
    ).values(),
  ];
  useEffect(() => {
    setDirty(contentDirty || imageDirty);
  }, [contentDirty, imageDirty, setDirty]);
  function patch(value: Partial<RecipeForm>) {
    setForm((f) => ({ ...f, ...value }));
  }
  function group(index: number, value: Partial<Group>) {
    patch({
      ingredientGroups: form.ingredientGroups.map((g, i) =>
        i === index ? { ...g, ...value } : g,
      ),
    });
  }
  function entry(gi: number, ii: number, value: Partial<Entry>) {
    group(gi, {
      ingredients: form.ingredientGroups[gi].ingredients.map((item, i) =>
        i === ii ? { ...item, ...value } : item,
      ),
    });
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    await saveRecipe(form.publicationState);
  }
  async function saveRecipe(publicationState: RecipeForm["publicationState"]) {
    setBusy(true);
    setError("");
    try {
      const result = await api<{ id: string }>(
        recipe ? `/recipes/${recipe.id}` : "/recipes",
        {
          method: recipe ? "PUT" : "POST",
          body: JSON.stringify({ ...form, publicationState }),
        },
      );
      setDirty(false);
      onSaved(recipe?.id ?? result.id);
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
          <p className="eyebrow">RECEPTVERKSTADEN</p>
          <h1>{recipe ? "Redigera recept" : "Nytt recept"}</h1>
        </div>
        <button onClick={onCancel}>Avbryt</button>
      </div>
      <form onSubmit={save}>
        <fieldset disabled={busy} className="plain-fieldset">
          {pendingIngredients.length > 0 && (
            <aside
              className="publication-notice"
              aria-label="Publiceringshinder"
            >
              <h2>Kan sparas som utkast – inte publiceras ännu</h2>
              <p>
                Följande ingredienser väntar på granskning:{" "}
                <strong>{pendingIngredients.join(", ")}</strong>.
              </p>
              <p>
                Behåll dem i ditt utkast eller ersätt dem med godkända
                ingredienser. Publicering blir möjlig när alla ingredienser är
                aktiva.
              </p>
            </aside>
          )}
          <section className="panel">
            <h2>Grunderna</h2>
            <label>
              Receptnamn
              <input
                autoFocus
                required={form.publicationState === "Published"}
                maxLength={200}
                value={form.name}
                onChange={(e) => patch({ name: e.target.value })}
                placeholder="Vad ska vi laga?"
              />
            </label>
            <label>
              Beskrivning
              <textarea
                rows={3}
                value={form.description ?? ""}
                onChange={(e) => patch({ description: e.target.value })}
              />
            </label>
            <div className="columns">
              <RecipeTypeSelect
                items={catalogue.recipeTypes.filter(
                  (t) => t.isActive || t.id === recipe?.recipeTypeId,
                )}
                value={form.recipeTypeId ?? ""}
                required={form.publicationState === "Published"}
                onChange={(id) => patch({ recipeTypeId: id || null })}
              />
              <label>
                Total tid (minuter)
                <input
                  type="number"
                  min="1"
                  step="1"
                  value={form.totalTimeMinutes ?? ""}
                  onChange={(e) =>
                    patch({
                      totalTimeMinutes: e.target.value
                        ? Number(e.target.value)
                        : null,
                    })
                  }
                />
              </label>
              <label>
                Status
                <select
                  value={form.publicationState}
                  onChange={(e) =>
                    patch({
                      publicationState: e.target
                        .value as RecipeForm["publicationState"],
                    })
                  }
                >
                  <option value="Draft">Utkast</option>
                  <option value="Published">Publicerat lokalt</option>
                </select>
              </label>
            </div>
            <h3>Ger</h3>
            <p className="hint">
              Hur mycket blir det? Ange en exakt mängd eller ett intervall, till
              exempel 20–24 stycken.
            </p>
            <QuantityFields
              required={form.publicationState === "Published"}
              prefix="Ger: "
              quantity={
                form.yield?.quantity ?? { value: null, maximumValue: null }
              }
              unitId={form.yield?.unitId ?? null}
              units={catalogue.units.filter((u) => u.forYield)}
              onChange={(quantity, unitId) =>
                patch({
                  yield:
                    quantity.value === null &&
                    quantity.maximumValue === null &&
                    !unitId
                      ? null
                      : { quantity, unitId: unitId ?? "" },
                })
              }
            />
            <fieldset className="tags">
              <legend>Taggar</legend>
              <label>
                Sök taggar att välja
                <input
                  type="search"
                  value={tagQuery}
                  onChange={(e) => setTagQuery(e.target.value)}
                  placeholder="Namn eller alias"
                />
              </label>
              {catalogue.tags
                .filter(
                  (t) =>
                    (t.isActive || recipe?.tagIds.includes(t.id)) &&
                    (form.tagIds.includes(t.id) ||
                      matchesCatalogue(t, tagQuery)),
                )
                .map((t) => (
                  <label key={t.id}>
                    <input
                      type="checkbox"
                      checked={form.tagIds.includes(t.id)}
                      onChange={(e) =>
                        patch({
                          tagIds: e.target.checked
                            ? [...form.tagIds, t.id]
                            : form.tagIds.filter((id) => id !== t.id),
                        })
                      }
                    />
                    {t.name}
                    {!t.isActive ? " (inaktiv)" : ""}
                  </label>
                ))}
            </fieldset>
          </section>
          <section className="panel">
            <h2>Ingredienser</h2>
            <p className="hint">
              Sök och välj ur katalogen. Lämna mängd och enhet tomma för
              exempelvis ”efter smak”.
            </p>
            {form.ingredientGroups.map((g, gi) => (
              <section
                className="ingredient-group"
                key={g.id ?? g.clientKey ?? `group-${gi}`}
                aria-label={`Ingrediensgrupp ${gi + 1}`}
              >
                <div className="group-heading">
                  <label>
                    Grupprubrik (valfri)
                    <input
                      value={g.heading ?? ""}
                      placeholder="Utan rubrik, eller t.ex. Sås"
                      onChange={(e) => group(gi, { heading: e.target.value })}
                    />
                  </label>
                  <OrderButtons
                    label={`Grupp ${gi + 1}`}
                    index={gi}
                    count={form.ingredientGroups.length}
                    onMove={(d) =>
                      patch({
                        ingredientGroups: move(form.ingredientGroups, gi, d),
                      })
                    }
                    onRemove={() => {
                      if (
                        g.ingredients.length &&
                        !window.confirm(
                          "Ta bort gruppen och dess ingredienser?",
                        )
                      )
                        return;
                      patch({
                        ingredientGroups:
                          form.ingredientGroups.length === 1
                            ? [{ heading: "", ingredients: [] }]
                            : form.ingredientGroups.filter((_, i) => i !== gi),
                      });
                    }}
                  />
                </div>
                {g.ingredients.map((item, ii) => (
                  <div
                    className="ingredient-row"
                    key={item.id ?? item.clientKey ?? ii}
                  >
                    <div className="row-heading">
                      <strong>{item.ingredientName}</strong>
                      {item.ingredientStatus === "PendingReview" && (
                        <small>Väntar på granskning</small>
                      )}
                      <OrderButtons
                        label={item.ingredientName}
                        index={ii}
                        count={g.ingredients.length}
                        onMove={(d) =>
                          group(gi, { ingredients: move(g.ingredients, ii, d) })
                        }
                        onRemove={() =>
                          group(gi, {
                            ingredients: g.ingredients.filter(
                              (_, i) => i !== ii,
                            ),
                          })
                        }
                      />
                    </div>
                    <QuantityFields
                      prefix={`${item.ingredientName}: `}
                      quantity={item.quantity}
                      unitId={item.unitId}
                      units={catalogue.units.filter((u) => u.forIngredients)}
                      onChange={(quantity, unitId) =>
                        entry(gi, ii, { quantity, unitId })
                      }
                    />
                    <div className="columns">
                      <label>
                        Mängdbeskrivning
                        <input
                          placeholder="t.ex. efter smak"
                          value={item.amountDescription ?? ""}
                          onChange={(e) =>
                            entry(gi, ii, { amountDescription: e.target.value })
                          }
                        />
                      </label>
                      <label>
                        Förberedelse
                        <input
                          placeholder="t.ex. finhackad"
                          value={item.preparation ?? ""}
                          onChange={(e) =>
                            entry(gi, ii, { preparation: e.target.value })
                          }
                        />
                      </label>
                      <label>
                        Anteckning
                        <input
                          value={item.note ?? ""}
                          onChange={(e) =>
                            entry(gi, ii, { note: e.target.value })
                          }
                        />
                      </label>
                    </div>
                    {form.ingredientGroups.length > 1 && (
                      <label>
                        Flytta till grupp
                        <select
                          aria-label={`Flytta ${item.ingredientName} till grupp`}
                          value={gi}
                          onChange={(e) => {
                            const target = Number(e.target.value);
                            patch({
                              ingredientGroups: form.ingredientGroups.map(
                                (x, i) =>
                                  i === gi
                                    ? {
                                        ...x,
                                        ingredients: x.ingredients.filter(
                                          (_, j) => j !== ii,
                                        ),
                                      }
                                    : i === target
                                      ? {
                                          ...x,
                                          ingredients: [...x.ingredients, item],
                                        }
                                      : x,
                              ),
                            });
                          }}
                        >
                          {form.ingredientGroups.map((x, i) => (
                            <option key={i} value={i}>
                              {x.heading || `Grupp ${i + 1} (utan rubrik)`}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                  </div>
                ))}
                <IngredientPicker
                  allowCreate
                  label={`Lägg till ingrediens i ${g.heading || `grupp ${gi + 1}`}`}
                  onSelect={(item) =>
                    group(gi, {
                      ingredients: [
                        ...g.ingredients,
                        {
                          clientKey: crypto.randomUUID(),
                          ingredientId: item.id,
                          ingredientName: item.name,
                          ingredientStatus: item.status,
                          quantity: { value: null, maximumValue: null },
                          unitId: null,
                          amountDescription: null,
                          preparation: null,
                          note: null,
                        },
                      ],
                    })
                  }
                />
              </section>
            ))}
            <button
              type="button"
              onClick={() =>
                patch({
                  ingredientGroups: [
                    ...form.ingredientGroups,
                    {
                      clientKey: crypto.randomUUID(),
                      heading: "",
                      ingredients: [],
                    },
                  ],
                })
              }
            >
              + Lägg till grupp
            </button>
          </section>
          <section className="panel">
            <h2>Gör så här</h2>
            {form.cookingSteps.map((s, index) => (
              <div className="step-editor" key={s.id ?? s.clientKey ?? index}>
                <label>
                  Steg {index + 1}
                  <textarea
                    required
                    rows={3}
                    value={s.instruction}
                    onChange={(e) =>
                      patch({
                        cookingSteps: form.cookingSteps.map((x, i) =>
                          i === index
                            ? { ...x, instruction: e.target.value }
                            : x,
                        ),
                      })
                    }
                  />
                </label>
                <OrderButtons
                  label={`Steg ${index + 1}`}
                  index={index}
                  count={form.cookingSteps.length}
                  onMove={(d) =>
                    patch({ cookingSteps: move(form.cookingSteps, index, d) })
                  }
                  onRemove={() =>
                    patch({
                      cookingSteps: form.cookingSteps.filter(
                        (_, i) => i !== index,
                      ),
                    })
                  }
                />
              </div>
            ))}
            <button
              type="button"
              onClick={() =>
                patch({
                  cookingSteps: [
                    ...form.cookingSteps,
                    { clientKey: crypto.randomUUID(), instruction: "" },
                  ],
                })
              }
            >
              + Lägg till steg
            </button>
          </section>
          <div className="save-bar">
            {error && (
              <p role="alert" className="error">
                {error}
              </p>
            )}
            <span>
              {contentDirty
                ? "Osparade receptändringar"
                : "Inga osparade receptändringar"}
            </span>
            <button
              className="primary"
              disabled={
                busy ||
                imageDirty ||
                (form.publicationState === "Published" &&
                  pendingIngredients.length > 0)
              }
              type="submit"
            >
              {busy ? "Sparar…" : "Spara recept"}
            </button>
            {form.publicationState === "Published" && (
              <button
                type="button"
                disabled={busy || imageDirty}
                onClick={() => saveRecipe("Draft")}
              >
                Spara som utkast
              </button>
            )}
            {imageDirty && <small>Spara bildändringarna först.</small>}
          </div>
        </fieldset>
      </form>
      {recipe ? (
        <ImageEditor
          recipeId={recipe.id}
          initial={recipe.images}
          setDirty={setImageDirty}
        />
      ) : (
        <section className="panel">
          <h2>Bilder</h2>
          <p>
            Spara receptet först. Öppna sedan redigering för att lägga till
            bilder.
          </p>
        </section>
      )}
    </>
  );
}
