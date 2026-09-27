import { test, expect } from "@playwright/test";

test("enter a grouped recipe with ranges, reorder steps, search aliases and edit it", async ({
  page,
}) => {
  const name = `Provrecept ${Date.now()}`;
  await page.goto("/");
  await page.getByRole("button", { name: "Skapa & utforska" }).click();
  await page.getByRole("button", { name: "+ Nytt recept" }).click();
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await page
    .getByRole("combobox", { name: "Recepttyp", exact: true })
    .selectOption({ label: "Huvudrätt" });
  await page.getByLabel("Ger: Mängd", { exact: true }).fill("20");
  await page.getByLabel("Ger: Max (valfritt)").fill("24");
  await page
    .getByRole("combobox", { name: "Ger: Enhet", exact: true })
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
  await page
    .getByRole("combobox", { name: "Status", exact: true })
    .selectOption("Published");
  await page.getByRole("checkbox", { name: "Vegetariskt" }).check();
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await expect(page.getByText("2–3 dl", { exact: true })).toBeVisible();
  await expect(page.getByText("efter smak", { exact: true })).toBeVisible();
  await expect(page.locator(".steps li").first()).toHaveText(
    "Sjud försiktigt.",
  );
  await page.getByRole("button", { name: "Redigera recept" }).click();
  await expect(page.getByLabel("Ger: Max (valfritt)")).toHaveValue("24");
  await page
    .getByLabel("Beskrivning", { exact: true })
    .fill("Kontrollerat i webbläsaren.");
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(page.getByText("Kontrollerat i webbläsaren.")).toBeVisible();
  await page.getByRole("button", { name: "Hitta & laga" }).click();
  await page.getByLabel("Receptnamn", { exact: true }).fill(name);
  await page
    .getByRole("combobox", { name: "Ingredienser — alla valda ska finnas" })
    .fill("squash");
  await page.getByRole("option", { name: /Zucchini/ }).click();
  await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
});

test("administrator adds and updates names and aliases", async ({ page }) => {
  const alias = `Testalias ${Date.now()}`;
  await page.goto("/#/admin");
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
  await page
    .getByRole("searchbox", { name: "Sök i katalogen" })
    .fill(alias + " synonym");
  await page
    .getByRole("list", { name: "Ingredienskatalogens lista" })
    .getByRole("button")
    .click();
  await expect(page.getByLabel("Namn", { exact: true })).toHaveValue(alias);
});

test("an unnamed incomplete draft can be saved and edited without type or yield", async ({
  page,
}) => {
  await page.goto("/#/new");
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Namnlöst recept", exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Redigera recept" }).click();
  await expect(page.getByLabel("Receptnamn", { exact: true })).toHaveValue("");
  await expect(
    page.getByRole("combobox", { name: "Recepttyp", exact: true }),
  ).toHaveValue("");
  await expect(page.getByLabel("Ger: Mängd", { exact: true })).toHaveValue("");
  await page
    .getByRole("combobox", { name: "Status", exact: true })
    .selectOption("Published");
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Redigera recept" }),
  ).toBeVisible();
  expect(
    await page
      .getByLabel("Receptnamn", { exact: true })
      .evaluate((input: HTMLInputElement) => input.validity.valueMissing),
  ).toBeTruthy();
});

test("quick creation keeps unsaved recipe state and selects an Active canonical ingredient", async ({
  page,
  request,
}) => {
  const name = `Ramslök ${Date.now()}`;
  await page.goto("/#/new");
  await page.getByLabel("Receptnamn", { exact: true }).fill("Osparad soppa");
  await page
    .getByLabel("Beskrivning", { exact: true })
    .fill("Viktig osparad text");
  await page.getByRole("button", { name: "+ Lägg till steg" }).click();
  await page.getByLabel("Steg 1", { exact: true }).fill("Hacka varsamt.");
  const search = page.getByRole("combobox", {
    name: "Lägg till ingrediens i grupp 1",
  });
  const queries: string[] = [];
  page.on("request", (req) => {
    if (req.url().includes("/api/ingredients?")) queries.push(req.url());
  });
  await search.fill("R");
  await expect(
    page.getByText("Skriv minst 2 tecken för att söka."),
  ).toBeVisible();
  await expect(page.getByRole("listbox")).toHaveCount(0);
  await search.fill(name);
  await page.getByRole("option", { name: `+ Skapa ”${name}”` }).click();
  const dialog = page.getByRole("dialog", { name: "Skapa ingrediens" });
  await expect(dialog.getByLabel("Föredraget namn")).toHaveValue(name);
  await dialog.getByRole("button", { name: "Skapa och välj" }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.getByLabel("Receptnamn", { exact: true })).toHaveValue(
    "Osparad soppa",
  );
  await expect(
    page.getByRole("textbox", { name: "Beskrivning", exact: true }),
  ).toHaveValue("Viktig osparad text");
  await expect(
    page.getByRole("textbox", { name: "Steg 1", exact: true }),
  ).toHaveValue("Hacka varsamt.");
  await expect(page.locator(".ingredient-row strong")).toHaveText(name);
  expect(
    queries.some((url) => new URL(url).searchParams.get("query") === "R"),
  ).toBeFalsy();
  const matches = await (
    await request.get("/api/ingredients", { params: { query: name } })
  ).json();
  expect(matches).toHaveLength(1);
  expect(matches[0].status).toBe("Active");
  page.once("dialog", (d) => d.dismiss());
  await page.getByRole("button", { name: "Hitta & laga", exact: true }).click();
  await expect(page.getByLabel("Receptnamn", { exact: true })).toHaveValue(
    "Osparad soppa",
  );
  const saving = page.waitForResponse(
    (r) => r.url().endsWith("/api/recipes") && r.request().method() === "POST",
  );
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  const { id } = await (await saving).json();
  const detail = await (await request.get(`/api/recipes/${id}`)).json();
  expect(detail.ingredientGroups[0].ingredients[0].ingredientId).toBe(
    matches[0].id,
  );
});

test("administrator catalogue opens populated, searches aliases and filters status", async ({
  page,
}) => {
  await page.goto("/#/ingredients");
  const list = page.getByRole("list", { name: "Ingredienskatalogens lista" });
  await expect(
    list.getByRole("button", { name: "Zucchini Aktiv", exact: true }),
  ).toBeVisible();
  await page
    .getByRole("searchbox", { name: "Sök i katalogen" })
    .fill("courgette");
  await expect(list.getByRole("button")).toHaveCount(1);
  await expect(list.getByRole("button")).toHaveText("ZucchiniAktiv");
  await page
    .getByRole("combobox", { name: "Statusfilter" })
    .selectOption("PendingReview");
  await expect(list.getByRole("button")).toHaveCount(0);
  await page
    .getByRole("combobox", { name: "Statusfilter" })
    .selectOption("Active");
  await expect(list.getByRole("button")).toHaveCount(1);
});

test("images and unsaved-edit protection work on desktop and narrow screens", async ({
  page,
  request,
}) => {
  const catalogue = await (await request.get("/api/catalogue")).json();
  const name = `Bildrecept ${Date.now()}`;
  const created = await request.post("/api/recipes", {
    data: {
      name,
      description: null,
      recipeTypeId: catalogue.recipeTypes[0].id,
      yield: {
        quantity: { value: 4, maximumValue: null },
        unitId: catalogue.units.find(
          (u: { symbol: string }) => u.symbol === "st",
        ).id,
      },
      totalTimeMinutes: null,
      publicationState: "Draft",
      tagIds: [],
      ingredientGroups: [],
      cookingSteps: [],
    },
  });
  expect(created.ok()).toBeTruthy();
  const { id } = await created.json();
  await page.goto(`/#/recipes/${id}/edit`);
  const file = {
    name: "example.png",
    mimeType: "image/png",
    buffer: Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
      "base64",
    ),
  };
  await page.getByLabel("Lägg till bild").setInputFiles(file);
  await expect(page.getByLabel("Bildtext")).toBeVisible();
  await page.getByLabel("Bildtext").fill("En provbild");
  await page.getByRole("button", { name: "Spara bildändringar" }).click();
  await expect(
    page.getByRole("button", { name: "Spara bildändringar" }),
  ).toHaveCount(0);
  await page.getByLabel("Receptnamn", { exact: true }).fill("Osparad ändring");
  page.once("dialog", (dialog) => dialog.dismiss());
  await page.getByRole("button", { name: "Hitta & laga", exact: true }).click();
  await expect(page.getByLabel("Receptnamn", { exact: true })).toHaveValue(
    "Osparad ändring",
  );
  await page.setViewportSize({ width: 390, height: 844 });
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBeTruthy();
  await page.screenshot({
    path: "test-results/editor-mobile.png",
    fullPage: true,
  });
  page.once("dialog", (dialog) => dialog.accept());
  await page.getByRole("button", { name: "Hitta & laga", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Vad vill du laga?" }),
  ).toBeVisible();
  await expect(page.locator(".recipe-card").first()).toBeVisible();
  await page.setViewportSize({ width: 1440, height: 1000 });
  await page.screenshot({
    path: "test-results/recipe-list-desktop.png",
    fullPage: true,
  });
});
