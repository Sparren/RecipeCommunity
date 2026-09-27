import { useState } from "react";
import type { Ingredient } from "../../types";
import { IngredientPicker } from "../../components/IngredientPicker";
import { QuickCreateIngredient } from "../../components/QuickCreateIngredient";

export function IngredientWorkspace({
  administrator,
}: {
  administrator: boolean;
}) {
  const [creating, setCreating] = useState(false);
  const [selected, setSelected] = useState<Ingredient>();
  return (
    <>
      <div className="page-heading">
        <div>
          <p className="eyebrow">SKAPA & UTFORSKA</p>
          <h1>Ingredienser</h1>
          <p>
            Hitta en ingrediens eller skapa en som saknas, utan att börja på ett
            recept.
          </p>
        </div>
      </div>
      <section className="panel">
        <h2>Sök först i katalogen</h2>
        <p>
          {administrator
            ? "Ingredienser som du skapar som administratör blir aktiva direkt."
            : "Dina nya ingredienser väntar på granskning. Du kan använda dem i egna utkast, men receptet kan inte publiceras innan de är godkända eller ersatta."}
        </p>
        <IngredientPicker label="Sök ingrediens" onSelect={setSelected} />
        <button className="primary" onClick={() => setCreating(true)}>
          + Skapa ingrediens
        </button>
      </section>
      {selected && (
        <section className="panel" aria-live="polite">
          <h2>{selected.name}</h2>
          <p>
            {selected.status === "Active"
              ? "Aktiv – kan användas i recept."
              : "Väntar på granskning – kan användas i dina utkast."}
          </p>
          <p>
            Du kan hitta ingrediensen via sökningen nästa gång du skapar eller
            redigerar ett recept.
          </p>
          <ul>
            {selected.names.map((n) => (
              <li key={n.language}>
                {n.language}: {n.name}
              </li>
            ))}
          </ul>
        </section>
      )}
      {creating && (
        <QuickCreateIngredient
          standalone
          initialName=""
          onClose={() => setCreating(false)}
          onCreated={(item) => {
            setSelected(item);
            setCreating(false);
          }}
        />
      )}
    </>
  );
}
