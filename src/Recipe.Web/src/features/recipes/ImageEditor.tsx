import { useEffect, useState } from "react";
import { api } from "../../api";
import type { RecipeDetail, RecipeImage } from "../../types";
import { move, OrderButtons } from "../../components/OrderButtons";

export function ImageEditor({
  recipeId,
  initial,
  setDirty,
}: {
  recipeId: string;
  initial: RecipeImage[];
  setDirty: (dirty: boolean) => void;
}) {
  const [images, setImages] = useState(initial);
  const [saved, setSaved] = useState(initial);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const dirty = JSON.stringify(images) !== JSON.stringify(saved);
  useEffect(() => {
    setDirty(dirty || busy);
  }, [dirty, busy, setDirty]);
  async function reload() {
    const r = await api<RecipeDetail>(`/recipes/${recipeId}`);
    setImages(r.images);
    setSaved(r.images);
  }
  async function saveMetadata() {
    await api(`/recipes/${recipeId}/images`, {
      method: "PUT",
      body: JSON.stringify(images),
    });
  }
  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError("");
    try {
      await action();
      await reload();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="panel">
      <h2>Bilder</h2>
      <p className="hint">
        JPEG, PNG eller WebP, högst 10 MB. Bilder sparas separat från
        recepttexten.
      </p>
      <fieldset disabled={busy} className="plain-fieldset">
        <div className="image-grid">
          {images.map((image, i) => (
            <div key={image.id} className="image-card">
              <img src={image.url} alt={image.caption || "Receptbild"} />
              <label>
                Bildtext
                <input
                  value={image.caption ?? ""}
                  onChange={(e) =>
                    setImages(
                      images.map((x) =>
                        x.id === image.id
                          ? { ...x, caption: e.target.value }
                          : x,
                      ),
                    )
                  }
                />
              </label>
              <label className="inline">
                <input
                  type="radio"
                  name="primary-image"
                  checked={image.isPrimary}
                  onChange={() =>
                    setImages(
                      images.map((x) => ({
                        ...x,
                        isPrimary: x.id === image.id,
                      })),
                    )
                  }
                />
                Huvudbild
              </label>
              <OrderButtons
                label={`Bild ${i + 1}`}
                index={i}
                count={images.length}
                onMove={(d) => setImages(move(images, i, d))}
                onRemove={() => {
                  if (window.confirm("Ta bort bilden?"))
                    void run(async () => {
                      await saveMetadata();
                      await api(`/recipes/${recipeId}/images/${image.id}`, {
                        method: "DELETE",
                      });
                    });
                }}
              />
            </div>
          ))}
        </div>
        {dirty && (
          <button type="button" onClick={() => void run(saveMetadata)}>
            Spara bildändringar
          </button>
        )}
        <label className="upload">
          Lägg till bild
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={(e) => {
              const file = e.target.files?.[0];
              e.target.value = "";
              if (!file) return;
              if (file.size > 10 * 1024 * 1024) {
                setError("Bilden får vara högst 10 MB.");
                return;
              }
              void run(async () => {
                if (dirty) await saveMetadata();
                const body = new FormData();
                body.append("file", file);
                await api(`/recipes/${recipeId}/images`, {
                  method: "POST",
                  body,
                });
              });
            }}
          />
        </label>
      </fieldset>
      {busy && <p role="status">Sparar bilder…</p>}
      {error && (
        <p role="alert" className="error">
          {error}
        </p>
      )}
    </section>
  );
}
