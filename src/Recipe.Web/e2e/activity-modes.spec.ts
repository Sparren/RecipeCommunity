import { test, expect } from "@playwright/test";

test("discovery excludes drafts, creator filters own recipes and creates ingredients without a recipe", async ({
  page,
  request,
}, testInfo) => {
  const name = `Eget utkast ${Date.now()}`;
  const result = await request.post("/api/recipes", {
    data: {
      name,
      recipeTypeId: null,
      yield: null,
      publicationState: "Draft",
      tagIds: [],
      ingredientGroups: [],
      cookingSteps: [],
    },
  });
  expect(result.ok()).toBeTruthy();
  const { id } = await result.json();
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "Vad vill du laga?" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Hitta & laga" }),
  ).toHaveAttribute("aria-current", "page");
  await expect(page.getByRole("button", { name: "+ Nytt recept" })).toHaveCount(
    0,
  );
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await expect(
    page.getByRole("heading", { name: "Inga recept hittades" }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Skapa & utforska" }).click();
  await expect(
    page.getByRole("heading", { name: "Mina recept" }),
  ).toBeVisible();
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await page
    .getByRole("combobox", { name: "Receptstatus" })
    .selectOption("Draft");
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await page
    .getByRole("combobox", { name: "Receptstatus" })
    .selectOption("Published");
  await expect(
    page.getByRole("heading", { name: "Inga recept hittades" }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Receptstatus" })
    .selectOption("Draft");
  await page.getByRole("button", { name: new RegExp(name) }).click();
  await expect(page).toHaveURL(new RegExp(`/create/recipes/${id}$`));
  await expect(
    page.getByRole("button", { name: "Redigera recept" }),
  ).toBeVisible();
  const recipeWrites: string[] = [];
  page.on("request", (r) => {
    if (r.method() === "POST" && r.url().endsWith("/api/recipes"))
      recipeWrites.push(r.url());
  });
  await page
    .getByRole("navigation", { name: "Skapa och utforska" })
    .getByRole("button", { name: "Ingredienser", exact: true })
    .click();
  await page.getByRole("button", { name: "+ Skapa ingrediens" }).click();
  const dialog = page.getByRole("dialog");
  const ingredientName = `Fristående ört ${Date.now()}`;
  await dialog.getByLabel("Föredraget namn").fill(ingredientName);
  await dialog
    .getByRole("button", { name: "Skapa ingrediens", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: ingredientName }),
  ).toBeVisible();
  await expect(page.getByText("Aktiv – kan användas i recept.")).toBeVisible();
  expect(recipeWrites).toHaveLength(0);
  await page
    .getByRole("combobox", { name: "Sök ingrediens" })
    .fill(ingredientName);
  await expect(
    page.getByRole("option", { name: ingredientName, exact: true }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Administration", exact: true })
    .click();
  await expect(
    page.getByRole("heading", { name: "Underhåll katalogerna" }),
  ).toBeVisible();
  await page.screenshot({
    path: testInfo.outputPath("administration.png"),
    fullPage: true,
  });
});
