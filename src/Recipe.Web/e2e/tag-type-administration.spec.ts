import { test, expect } from "@playwright/test";

test("maintain translated tags and ordered types, retain inactive references, and exclude new assignments", async ({
  page,
}) => {
  const suffix = Date.now();
  const tag = `Provtagg ${suffix}`;
  const type = `Provtyp ${suffix}`;
  const recipe = `Livscykel ${suffix}`;
  await page.goto("/");
  for (const entry of [
    { menu: "Taggar", name: tag, save: "Spara tagg" },
    { menu: "Recepttyper", name: type, save: "Spara recepttyp" },
  ]) {
    await page
      .getByRole("button", { name: "Administration", exact: true })
      .click();
    await page.getByRole("button", { name: entry.menu, exact: true }).click();
    await page.getByLabel("Namn", { exact: true }).fill(entry.name);
    await page.getByRole("button", { name: "+ Lägg till språk" }).click();
    await page.getByLabel("Språk", { exact: true }).nth(1).fill("en");
    await page
      .getByLabel("Namn", { exact: true })
      .nth(1)
      .fill(`English ${entry.name}`);
    await page.getByRole("button", { name: "+ Lägg till alias" }).click();
    await page
      .getByLabel("Alias", { exact: true })
      .fill(`Alternativ ${entry.name}`);
    if (entry.menu === "Recepttyper")
      await page.getByRole("spinbutton").fill("20");
    await page.getByRole("button", { name: entry.save }).click();
    await expect(page.getByRole("status")).toContainText("är sparad");
    await expect(
      page.getByRole("button", { name: new RegExp(entry.name + ".*Aktiv") }),
    ).toBeVisible();
    if (entry.menu === "Taggar") {
      await page
        .getByRole("searchbox", { name: "Sök taggar" })
        .fill(`Alternativ ${tag}`);
      await expect(
        page.getByRole("list", { name: "Taggarnas lista" }).getByRole("button"),
      ).toHaveCount(1);
    } else
      await expect(
        page.getByRole("button", { name: new RegExp(type + ".*Ordning 20") }),
      ).toBeVisible();
  }
  await page
    .getByRole("button", { name: "Skapa & utforska", exact: true })
    .click();
  await page.getByRole("button", { name: "+ Nytt recept" }).click();
  await page.getByLabel("Receptnamn", { exact: true }).fill(recipe);
  await page
    .getByRole("searchbox", { name: "Sök recepttyp", exact: true })
    .fill(`Alternativ ${type}`);
  await page
    .getByRole("searchbox", { name: "Sök taggar att välja" })
    .fill(`Alternativ ${tag}`);
  await page
    .getByRole("combobox", { name: "Recepttyp", exact: true })
    .selectOption({ label: type });
  await page.getByRole("checkbox", { name: tag, exact: true }).check();
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: recipe, exact: true }),
  ).toBeVisible();
  const recipeUrl = page.url();
  for (const entry of [
    { menu: "Taggar", name: tag, save: "Spara tagg" },
    { menu: "Recepttyper", name: type, save: "Spara recepttyp" },
  ]) {
    await page
      .getByRole("button", { name: "Administration", exact: true })
      .click();
    await page.getByRole("button", { name: entry.menu, exact: true }).click();
    await page
      .getByRole("button", { name: new RegExp(entry.name + ".*Aktiv") })
      .click();
    await expect(page.getByLabel("Namn", { exact: true })).toHaveCount(2);
    await expect(page.getByLabel("Alias", { exact: true })).toHaveValue(
      `Alternativ ${entry.name}`,
    );
    await page
      .getByLabel("Alias", { exact: true })
      .fill(`Ändrat ${entry.name}`);
    await page
      .getByRole("combobox", { name: "Status", exact: true })
      .selectOption("false");
    await page.getByRole("button", { name: entry.save }).click();
    await expect(page.getByRole("status")).toContainText("är sparad");
    await page
      .getByRole("combobox", { name: "Statusfilter", exact: true })
      .selectOption("false");
    await expect(
      page.getByRole("button", { name: new RegExp(entry.name + ".*Inaktiv") }),
    ).toBeVisible();
    await page
      .getByRole("combobox", { name: "Statusfilter", exact: true })
      .selectOption("true");
    await expect(
      page.getByRole("button", { name: new RegExp(entry.name + ".*Inaktiv") }),
    ).toHaveCount(0);
  }
  await page.goto(recipeUrl);
  await expect(page.getByText(tag, { exact: true })).toBeVisible();
  await expect(page.getByText(type, { exact: false }).first()).toBeVisible();
  await page
    .getByRole("button", { name: "Redigera recept", exact: true })
    .click();
  await expect(
    page.getByRole("option", { name: `${type} (inaktiv)` }),
  ).toHaveCount(1);
  await expect(
    page.getByRole("checkbox", { name: `${tag} (inaktiv)` }),
  ).toBeChecked();
  await page
    .getByRole("textbox", { name: "Beskrivning", exact: true })
    .fill("Kan fortfarande redigeras");
  await page.getByRole("button", { name: "Spara recept", exact: true }).click();
  await expect(page.getByText("Kan fortfarande redigeras")).toBeVisible();
  await page
    .getByRole("button", { name: "Skapa & utforska", exact: true })
    .click();
  await page.getByRole("button", { name: "+ Nytt recept" }).click();
  await expect(
    page.getByRole("option", { name: new RegExp(type) }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("checkbox", { name: new RegExp(tag) }),
  ).toHaveCount(0);
});
