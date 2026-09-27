import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, it, vi } from "vitest";
import { IngredientWorkspace } from "./IngredientWorkspace";

afterEach(() => vi.unstubAllGlobals());
it("creates a pending ingredient independently of any recipe", async () => {
  const item = {
    id: "ingredient",
    name: "Testört",
    names: [{ language: "sv", name: "Testört" }],
    aliases: [],
    status: "PendingReview",
  };
  const fetch = vi.fn(
    async (_url: RequestInfo | URL, init?: RequestInit) =>
      new Response(
        JSON.stringify(init?.method === "POST" ? { id: item.id } : item),
      ),
  );
  vi.stubGlobal("fetch", fetch);
  const user = userEvent.setup();
  render(<IngredientWorkspace administrator={false} />);
  await user.click(screen.getByRole("button", { name: "+ Skapa ingrediens" }));
  const dialog = await screen.findByRole("dialog");
  await user.type(within(dialog).getByLabelText("Föredraget namn"), "Testört");
  await user.click(
    within(dialog).getByRole("button", { name: "Skapa ingrediens" }),
  );
  expect(
    await screen.findByRole("heading", { name: "Testört" }),
  ).toBeInTheDocument();
  expect(
    screen.getByText("Väntar på granskning – kan användas i dina utkast."),
  ).toBeInTheDocument();
  await waitFor(() =>
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument(),
  );
  expect(
    fetch.mock.calls.every(([url]) =>
      String(url).startsWith("/api/ingredients"),
    ),
  ).toBe(true);
});
