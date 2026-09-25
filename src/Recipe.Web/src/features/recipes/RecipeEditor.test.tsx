import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RecipeEditor } from "./RecipeEditor";
import { IngredientPicker } from "../../components/IngredientPicker";
import { RecipeView } from "./RecipeView";
import type { Catalogue, RecipeDetail } from "../../types";
const catalogue: Catalogue = {
  recipeTypes: [{ id: "type", name: "Bröd" }],
  tags: [],
  units: [
    {
      id: "unit",
      name: "stycken",
      symbol: "st",
      dimension: "Count",
      forIngredients: true,
      forYield: true,
    },
  ],
};
const recipe: RecipeDetail = {
  id: "recipe",
  name: "Bullar",
  description: "",
  authorName: "Kock",
  createdAt: "",
  updatedAt: "",
  recipeTypeId: "type",
  yield: { quantity: { value: 20, maximumValue: 24 }, unitId: "unit" },
  totalTimeMinutes: null,
  publicationState: "Draft",
  tagIds: [],
  ingredientGroups: [
    {
      id: "group",
      heading: null,
      ingredients: [
        {
          id: "entry",
          ingredientId: "flour",
          ingredientName: "Mjöl",
          quantity: { value: 2, maximumValue: 3 },
          unitId: "unit",
          preparation: null,
          amountDescription: null,
          note: null,
        },
      ],
    },
  ],
  cookingSteps: [
    { id: "a", instruction: "Blanda" },
    { id: "b", instruction: "Grädda" },
  ],
  images: [],
};
afterEach(() => vi.unstubAllGlobals());
describe("recipe editor", () => {
  it("preserves structured ranges and stable step IDs when reordered and saved", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    const user = userEvent.setup();
    const saved = vi.fn();
    render(
      <RecipeEditor
        recipe={recipe}
        catalogue={catalogue}
        onSaved={saved}
        onCancel={vi.fn()}
        setDirty={vi.fn()}
      />,
    );
    await user.click(
      screen.getByRole("button", { name: "Steg 2: flytta upp" }),
    );
    expect(screen.getByLabelText("Steg 1")).toHaveValue("Grädda");
    await user.click(screen.getByRole("button", { name: "Spara recept" }));
    await waitFor(() => expect(saved).toHaveBeenCalledWith("recipe"));
    const input = JSON.parse(fetch.mock.calls[0][1].body);
    expect(input.cookingSteps[0].id).toBe("b");
    expect(input.yield.quantity).toEqual({ value: 20, maximumValue: 24 });
    expect(input.ingredientGroups[0].ingredients[0].quantity).toEqual({
      value: 2,
      maximumValue: 3,
    });
  });
  it("does not turn unmatched typed ingredient text into a catalogue selection", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]")));
    const selected = vi.fn();
    const user = userEvent.setup();
    render(<IngredientPicker onSelect={selected} />);
    await user.type(screen.getByRole("combobox"), "unknown{Enter}");
    await screen.findByText(/Ingen träff/);
    expect(selected).not.toHaveBeenCalled();
  });
  it("hides an unnamed group heading and displays quantity ranges", () => {
    render(
      <RecipeView recipe={recipe} catalogue={catalogue} navigate={vi.fn()} />,
    );
    expect(screen.queryByRole("heading", { level: 3 })).not.toBeInTheDocument();
    expect(screen.getByText("2–3 st")).toBeInTheDocument();
    expect(screen.getByText(/20–24 st/)).toBeInTheDocument();
  });
});
