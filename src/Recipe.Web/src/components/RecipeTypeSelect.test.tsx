import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { expect, it, vi } from "vitest";
import { matchesCatalogue, RecipeTypeSelect } from "./RecipeTypeSelect";

it("resolves an alias to the canonical type while displaying its preferred name", async () => {
  const item = {
    id: "canonical",
    name: "Amuse-bouche",
    isActive: true,
    sortOrder: 0,
    aliases: [{ language: "sv", name: "Aptitretare" }],
  };
  const selected = vi.fn();
  render(
    <RecipeTypeSelect
      items={[
        item,
        { id: "other", name: "Dessert", isActive: true, sortOrder: 1 },
      ]}
      value=""
      onChange={selected}
    />,
  );
  const user = userEvent.setup();
  await user.type(screen.getByRole("searchbox"), "APTIT");
  expect(
    screen.queryByRole("option", { name: "Dessert" }),
  ).not.toBeInTheDocument();
  await user.selectOptions(screen.getByRole("combobox"), "canonical");
  expect(selected).toHaveBeenCalledWith("canonical");
  expect(
    screen.getByRole("option", { name: "Amuse-bouche" }),
  ).toBeInTheDocument();
  expect(
    matchesCatalogue(
      { ...item, aliases: [{ language: "sv", name: "ÖRTIG" }] },
      "ört",
    ),
  ).toBe(true);
});
