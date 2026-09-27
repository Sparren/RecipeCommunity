import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RecipeEditor } from "./RecipeEditor";
import { IngredientPicker } from "../../components/IngredientPicker";
import { RecipeView } from "./RecipeView";
import type { Catalogue, RecipeDetail } from "../../types";
const catalogue: Catalogue = {
  recipeTypes: [{ id: "type", name: "Bröd", isActive: true, sortOrder: 0 }],
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
  authorId: "author",
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
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  vi.useRealTimers();
});
describe("recipe editor", () => {
  it("names pending ingredients, prevents publishing and still saves a draft", async () => {
    const pendingRecipe: RecipeDetail = {
      ...recipe,
      publicationState: "Published",
      ingredientGroups: [
        {
          ...recipe.ingredientGroups[0],
          ingredients: [
            {
              ...recipe.ingredientGroups[0].ingredients[0],
              ingredientStatus: "PendingReview",
              ingredientName: "Testört",
            },
          ],
        },
      ],
    };
    const fetch = vi
      .fn()
      .mockResolvedValue(new Response(null, { status: 204 }));
    vi.stubGlobal("fetch", fetch);
    const saved = vi.fn();
    const user = userEvent.setup();
    render(
      <RecipeEditor
        recipe={pendingRecipe}
        catalogue={catalogue}
        setDirty={vi.fn()}
        onSaved={saved}
        onCancel={vi.fn()}
      />,
    );
    expect(
      screen.getByRole("complementary", { name: "Publiceringshinder" }),
    ).toHaveTextContent("Testört");
    expect(screen.getByRole("button", { name: "Spara recept" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Spara som utkast" }));
    await waitFor(() => expect(saved).toHaveBeenCalledWith("recipe"));
    expect(JSON.parse(fetch.mock.calls[0][1].body).publicationState).toBe(
      "Draft",
    );
  });
  it("hides inactive choices for new recipes and preserves existing inactive references", () => {
    const choices: Catalogue = {
      ...catalogue,
      recipeTypes: [
        { id: "type", name: "Äldre typ", isActive: false, sortOrder: 0 },
      ],
      tags: [{ id: "tag", name: "Äldre tagg", isActive: false, sortOrder: 0 }],
    };
    const props = {
      catalogue: choices,
      onSaved: vi.fn(),
      onCancel: vi.fn(),
      setDirty: vi.fn(),
    };
    const view = render(<RecipeEditor {...props} />);
    expect(
      screen.queryByRole("option", { name: "Äldre typ (inaktiv)" }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("checkbox", { name: "Äldre tagg (inaktiv)" }),
    ).not.toBeInTheDocument();
    view.unmount();
    render(<RecipeEditor {...props} recipe={{ ...recipe, tagIds: ["tag"] }} />);
    expect(screen.getByRole("combobox", { name: "Recepttyp" })).toHaveValue(
      "type",
    );
    expect(
      screen.getByRole("option", { name: "Äldre typ (inaktiv)" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("checkbox", { name: "Äldre tagg (inaktiv)" }),
    ).toBeChecked();
  });
  it("saves an unnamed draft with no type, yield, ingredients or steps", async () => {
    const fetch = vi
      .fn()
      .mockResolvedValue(
        new Response(JSON.stringify({ id: "draft" }), { status: 201 }),
      );
    vi.stubGlobal("fetch", fetch);
    render(
      <RecipeEditor
        catalogue={catalogue}
        onSaved={vi.fn()}
        onCancel={vi.fn()}
        setDirty={vi.fn()}
      />,
    );
    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Spara recept" }));
    await waitFor(() => expect(fetch).toHaveBeenCalled());
    const input = JSON.parse(fetch.mock.calls[0][1].body);
    expect(input.name).toBe("");
    expect(input.recipeTypeId).toBeNull();
    expect(input.yield).toBeNull();
    expect(input.cookingSteps).toEqual([]);
    expect(input.ingredientGroups[0].ingredients).toEqual([]);
  });

  it("does not request or display autocomplete results below two characters", async () => {
    vi.useFakeTimers();
    const fetch = vi.fn().mockResolvedValue(new Response("[]"));
    vi.stubGlobal("fetch", fetch);
    render(<IngredientPicker onSelect={vi.fn()} />);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "R" } });
    await act(() => vi.advanceTimersByTimeAsync(500));
    expect(fetch).not.toHaveBeenCalled();
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "Ra" } });
    await act(() => vi.advanceTimersByTimeAsync(500));
    expect(fetch).toHaveBeenCalledTimes(1);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "R" } });
    expect(screen.queryByRole("listbox")).not.toBeInTheDocument();
  });

  it("inline creation selects the canonical ingredient without saving or losing recipe edits", async () => {
    vi.spyOn(HTMLDialogElement.prototype, "showModal").mockImplementation(
      function (this: HTMLDialogElement) {
        this.open = true;
      },
    );
    vi.spyOn(HTMLDialogElement.prototype, "close").mockImplementation(function (
      this: HTMLDialogElement,
    ) {
      this.open = false;
    });
    const ingredient = {
      id: "new-herb",
      name: "Ramslök",
      status: "Active",
      createdByUserId: "admin",
      createdAt: "",
      names: [{ language: "sv", name: "Ramslök" }],
      aliases: [],
    };
    const fetch = vi.fn(async (url: string, options?: RequestInit) => {
      if (url === "/api/ingredients" && options?.method === "POST")
        return new Response(JSON.stringify({ id: ingredient.id }), {
          status: 201,
        });
      if (url === "/api/ingredients/new-herb")
        return new Response(JSON.stringify(ingredient));
      return new Response("[]");
    });
    vi.stubGlobal("fetch", fetch);
    const saved = vi.fn();
    const dirty = vi.fn();
    const user = userEvent.setup();
    render(
      <RecipeEditor
        recipe={recipe}
        catalogue={catalogue}
        onSaved={saved}
        onCancel={vi.fn()}
        setDirty={dirty}
      />,
    );
    await user.clear(screen.getByLabelText("Receptnamn"));
    await user.type(screen.getByLabelText("Receptnamn"), "Osparat recept");
    await user.type(
      screen.getByRole("combobox", { name: "Lägg till ingrediens i grupp 1" }),
      "Ramslök",
    );
    await user.click(
      await screen.findByRole("option", { name: "+ Skapa ”Ramslök”" }),
    );
    await user.click(
      within(screen.getByRole("dialog")).getByRole("button", {
        name: "Skapa och välj",
      }),
    );
    await waitFor(() =>
      expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
    );
    expect(screen.getByLabelText("Receptnamn")).toHaveValue("Osparat recept");
    expect(screen.getByLabelText("Steg 1")).toHaveValue("Blanda");
    expect(
      screen.getByText("Ramslök", { selector: "strong" }),
    ).toBeInTheDocument();
    expect(saved).not.toHaveBeenCalled();
    expect(dirty).toHaveBeenLastCalledWith(true);
    expect(fetch.mock.calls.some(([url]) => url.includes("/recipes"))).toBe(
      false,
    );
  });
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
