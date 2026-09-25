import { test, expect } from "@playwright/test";

test("enter a grouped recipe with ranges, reorder steps, search aliases and edit it", async ({
  page,
}) => {
  const name = `Provrecept ${Date.now()}`;
  await page.goto("/");
  await page.getByRole("button", { name: "+ Nytt recept" }).click();
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await page
    .getByRole("combobox", { name: "Recepttyp", exact: true })
    .selectOption({ label: "Huvudrätt" });
  await page.getByLabel("Utbyte: Mängd", { exact: true }).fill("20");
  await page.getByLabel("Utbyte: Max (valfritt)").fill("24");
  await page
    .getByRole("combobox", { name: "Utbyte: Enhet", exact: true })
    .selectOption({ label: "stycken (st)" });
  await page
    .getByRole("combobox", { name: "Lägg till ingrediens i grupp 1" })
    .fill("courgette");
  await page.getByRole("option", { name: /Zucchini/ }).click();
  await page.getByLabel("Zucchini: Mängd", { exact: true }).fill("2");
  await page.getByLabel("Zucchini: Max (valfritt)").fill("3");
  await page
    .getByRole("combobox", { name: "Zucchini: Enhet", exact: true })
    .selectOption({ label: "deciliter (dl)" });
  await page.getByRole("button", { name: "+ Lägg till grupp" }).click();
  await page.getByLabel("Grupprubrik (valfri)").nth(1).fill("Garnering");
  await page
    .getByRole("combobox", { name: "Lägg till ingrediens i Garnering" })
    .fill("Salt");
  await page.getByRole("option", { name: "Salt", exact: true }).click();
  await page.getByLabel("Mängdbeskrivning").nth(1).fill("efter smak");
  await page.getByRole("button", { name: "+ Lägg till steg" }).click();
  await page.getByLabel("Steg 1", { exact: true }).fill("Servera varmt.");
  await page.getByRole("button", { name: "+ Lägg till steg" }).click();
  await page.getByLabel("Steg 2", { exact: true }).fill("Sjud försiktigt.");
  await page.getByRole("button", { name: "Steg 2: flytta upp" }).click();
  await page.getByRole("combobox", { name: "Status", exact: true }).selectOption("Published");
  await page.getByRole("checkbox", { name: "Vegetariskt" }).check();
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await expect(page.getByText("2–3 dl", { exact: true })).toBeVisible();
  await expect(page.getByText("efter smak", { exact: true })).toBeVisible();
  await expect(page.locator(".steps li").first()).toHaveText(
    "Sjud försiktigt.",
  );
  await page.getByRole("button", { name: "Redigera recept" }).click();
  await expect(page.getByLabel("Utbyte: Max (valfritt)")).toHaveValue("24");
  await page
    .getByLabel("Beskrivning", { exact: true })
    .fill("Kontrollerat i webbläsaren.");
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(page.getByText("Kontrollerat i webbläsaren.")).toBeVisible();
  await page.getByRole("button", { name: "← Alla recept" }).click();
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await page
    .getByRole("combobox", { name: "Ingredienser — alla valda ska finnas" })
    .fill("squash");
  await page.getByRole("option", { name: /Zucchini/ }).click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
});

test("administrator adds and updates names and aliases", async ({ page }) => {
  const alias = `Testalias ${Date.now()}`;
  await page.goto("/");
  await page
    .getByRole("button", { name: "Ingredienskatalog", exact: true })
    .click();
  await page.getByLabel("Namn", { exact: true }).fill(alias);
  await page.getByRole("button", { name: "+ Lägg till alias" }).click();
  await page.getByLabel("Alias", { exact: true }).fill(alias + " synonym");
  await page.getByRole("button", { name: "Spara ingrediens" }).click();
  await expect(page.getByRole("status")).toContainText(
    "Ingrediensen är sparad",
  );
  await page.getByRole("button", { name: "+ Ny ingrediens" }).click();
  await page.getByRole("combobox").fill(alias + " synonym");
  await page.getByRole("option").click();
  await expect(page.getByLabel("Namn", { exact: true })).toHaveValue(alias);
});

test("images and unsaved-edit protection work on desktop and narrow screens", async ({ page, request }) => {
  const catalogue = await (await request.get('/api/catalogue')).json();
  const name = `Bildrecept ${Date.now()}`;
  const created = await request.post('/api/recipes', { data: {
    name, description: null, recipeTypeId: catalogue.recipeTypes[0].id,
    yield: { quantity: { value: 4, maximumValue: null }, unitId: catalogue.units.find((u: { symbol: string }) => u.symbol === 'st').id },
    totalTimeMinutes: null, publicationState: 'Draft', tagIds: [], ingredientGroups: [], cookingSteps: [],
  } });
  expect(created.ok()).toBeTruthy();
  const { id } = await created.json();
  await page.goto(`/#/recipes/${id}/edit`);
  const file = { name: 'example.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=', 'base64') };
  await page.getByLabel('Lägg till bild').setInputFiles(file);
  await expect(page.getByLabel('Bildtext')).toBeVisible();
  await page.getByLabel('Bildtext').fill('En provbild');
  await page.getByRole('button', { name: 'Spara bildändringar' }).click();
  await expect(page.getByRole('button', { name: 'Spara bildändringar' })).toHaveCount(0);
  await page.getByLabel('Receptnamn', { exact: true }).fill('Osparad ändring');
  page.once('dialog', dialog => dialog.dismiss());
  await page.getByRole('button', { name: 'Recept', exact: true }).click();
  await expect(page.getByLabel('Receptnamn', { exact: true })).toHaveValue('Osparad ändring');
  await page.setViewportSize({ width: 390, height: 844 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
  await page.screenshot({ path: 'test-results/editor-mobile.png', fullPage: true });
  page.once('dialog', dialog => dialog.accept());
  await page.getByRole('button', { name: 'Recept', exact: true }).click();
  await expect(page.getByRole('heading', { name: 'Vad vill du laga?' })).toBeVisible();
  await expect(page.locator('.recipe-card').first()).toBeVisible();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.screenshot({ path: 'test-results/recipe-list-desktop.png', fullPage: true });
});

