import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, it, vi } from "vitest";
import { IngredientAdmin } from "./IngredientAdmin";
afterEach(() => vi.unstubAllGlobals());
it("loads the default catalogue, marks pending entries and requests status/name filters", async () => {
  const item = {
    id: "id",
    name: "Ramslök",
    status: "PendingReview",
    names: [{ language: "sv", name: "Ramslök" }],
    aliases: [],
  };
  const fetch = vi.fn(async () => new Response(JSON.stringify([item])));
  vi.stubGlobal("fetch", fetch);
  const user = userEvent.setup();
  render(<IngredientAdmin setDirty={vi.fn()} />);
  const list = await screen.findByRole("list", {
    name: "Ingredienskatalogens lista",
  });
  expect(within(list).getByText("Väntar på granskning")).toBeInTheDocument();
  expect(fetch.mock.calls[0]).toBeDefined();
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Statusfilter" }),
    "PendingReview",
  );
  await user.type(
    screen.getByRole("searchbox", { name: "Sök i katalogen" }),
    "ram",
  );
  await waitFor(() =>
    expect(fetch).toHaveBeenLastCalledWith(
      expect.stringContaining("status=PendingReview"),
      expect.anything(),
    ),
  );
  expect(fetch).toHaveBeenLastCalledWith(
    expect.stringContaining("query=ram"),
    expect.anything(),
  );
  await user.click(
    await screen.findByRole("button", { name: "Ramslök Väntar på granskning" }),
  );
  expect(screen.getByLabelText("Namn")).toHaveValue("Ramslök");
});
