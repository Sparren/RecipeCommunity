// Shared by browser verification and the optional manual print-sample command.
export async function createPrintSamples(
  baseUrl = "http://localhost:5050",
  suffix = "",
) {
  async function api(path: string, data?: unknown) {
    const response = await fetch(
      baseUrl + "/api" + path,
      data === undefined
        ? {}
        : {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(data),
          },
    );
    if (!response.ok)
      throw new Error(`${path}: ${response.status} ${await response.text()}`);
    return response.json();
  }
  const catalogue = await api("/catalogue");
  const ingredients = await api("/ingredients?query=Salt");
  const salt =
    ingredients.find((i: { name: string }) => i.name === "Salt") ??
    ingredients[0];
  if (!salt)
    throw new Error(
      "Seed ingredient Salt is required for these development fixtures.",
    );
  const unit = catalogue.units.find(
    (u: { symbol: string }) => u.symbol === "st",
  );
  const ingredientUnit = catalogue.units.find(
    (u: { symbol: string }) => u.symbol === "g",
  );
  const existing = await api(
    "/recipes?scope=Mine&name=" +
      encodeURIComponent("Utskriftsprov") +
      "&pageSize=100",
  );
  const results: { size: string; id: string; name: string }[] = [];
  for (const size of ["kort", "två sidor", "långt"]) {
    const name = `Utskriftsprov – ${size}${suffix}`;
    const prior = existing.items.find((r: { name: string }) => r.name === name);
    if (prior) {
      results.push({ size, id: prior.id, name });
      continue;
    }
    const groupCount = size === "kort" ? 1 : size === "två sidor" ? 3 : 7;
    const stepCount = size === "kort" ? 3 : size === "två sidor" ? 12 : 26;
    const instruction =
      "Förbered råvarorna och lägg dem i en rymlig skål. Blanda försiktigt så att konsistensen behålls. Värm långsamt under omrörning och kontrollera smaken innan du går vidare. Låt blandningen vila några minuter och håll arbetsytan ren.";
    const groups = Array.from({ length: groupCount }, (_, g) => ({
      heading:
        groupCount === 1
          ? null
          : [
              "Grundblandning",
              "Sås",
              "Garnering",
              "Tillbehör",
              "Servering",
              "Extra kryddning",
              "Avslutning",
            ][g],
      ingredients: Array.from(
        { length: size === "långt" && g === 2 ? 30 : 6 },
        (_, i) => ({
          ingredientId: salt.id,
          quantity: {
            value: i === 5 ? null : i + 1,
            maximumValue: i === 1 ? 3 : null,
          },
          unitId: i === 5 ? null : ingredientUnit.id,
          amountDescription: i === 5 ? "efter smak" : null,
          preparation: i % 2 ? "fint fördelat" : null,
          note: i === 3 ? "Tillsätt lite i taget och smaka av." : null,
        }),
      ),
    }));
    const created = await api("/recipes", {
      name,
      description:
        "Utvecklingsrecept för utskriftskontroll – inte ett matlagningsrecept. Kontrollera rubriker, grupper, stegnummer och sidbrytningar. Alla mängder och upprepningar är avsiktliga testdata.",
      recipeTypeId:
        catalogue.recipeTypes.find((t: { isActive: boolean }) => t.isActive)
          ?.id ?? null,
      yield: { quantity: { value: 4, maximumValue: 6 }, unitId: unit.id },
      totalTimeMinutes: 45,
      publicationState: "Draft",
      tagIds: catalogue.tags
        .filter((t: { isActive: boolean }) => t.isActive)
        .slice(0, 2)
        .map((t: { id: string }) => t.id),
      ingredientGroups: groups,
      cookingSteps: Array.from({ length: stepCount }, (_, i) => ({
        instruction: `${i === stepCount - 1 ? "Avsluta och servera. " : ""}${instruction}${size === "långt" ? " " + instruction.repeat(i === 8 ? 7 : 1) : ""}`,
      })),
    });
    results.push({ size, id: created.id, name });
  }
  return results;
}
