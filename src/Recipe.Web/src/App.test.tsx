import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import App from "./App";

beforeEach(() => {
  window.history.replaceState(null, "", "/");
  vi.spyOn(window, "scrollTo").mockImplementation(() => {});
});
afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

function mockApi(admin: boolean) {
  const fetch = vi.fn(async (url: RequestInfo | URL) => {
    const path = String(url);
    if (path === "/api/me")
      return new Response(
        JSON.stringify({
          id: "me",
          displayName: "Kock",
          isAdministrator: admin,
        }),
      );
    if (path === "/api/catalogue")
      return new Response(
        JSON.stringify({ recipeTypes: [], tags: [], units: [] }),
      );
    return new Response(
      JSON.stringify({ items: [], total: 0, pageNumber: 1, pageSize: 20 }),
    );
  });
  vi.stubGlobal("fetch", fetch);
  return fetch;
}

it("defaults to published discovery, then requests own drafts in creator mode", async () => {
  const fetch = mockApi(false);
  const user = userEvent.setup();
  render(<App />);
  await screen.findByRole("heading", { name: "Vad vill du laga?" });
  expect(
    screen.queryByRole("button", { name: "+ Nytt recept" }),
  ).not.toBeInTheDocument();
  expect(
    screen.queryByRole("button", { name: "Administration" }),
  ).not.toBeInTheDocument();
  await waitFor(() =>
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("scope=Published"),
      expect.anything(),
    ),
  );
  await user.click(screen.getByRole("button", { name: "Skapa & utforska" }));
  await screen.findByRole("heading", { name: "Mina recept" });
  await user.selectOptions(
    screen.getByRole("combobox", { name: "Receptstatus" }),
    "Draft",
  );
  await waitFor(() =>
    expect(fetch).toHaveBeenLastCalledWith(
      expect.stringContaining("scope=Mine&publicationState=Draft"),
      expect.anything(),
    ),
  );
  expect(
    screen.getByRole("button", { name: "+ Nytt recept" }),
  ).toBeInTheDocument();
});

it("denies an ordinary user the administration route without loading its data", async () => {
  window.history.replaceState(null, "", "/#/admin/tags");
  const fetch = mockApi(false);
  render(<App />);
  expect(await screen.findByRole("alert")).toHaveTextContent(
    "Endast administratören",
  );
  expect(
    fetch.mock.calls.some(([url]) => String(url).startsWith("/api/tags")),
  ).toBe(false);
});

it("collects catalogue maintenance under the administrator activity", async () => {
  mockApi(true);
  const user = userEvent.setup();
  render(<App />);
  await user.click(
    await screen.findByRole("button", { name: "Administration" }),
  );
  expect(
    await screen.findByRole("heading", { name: "Underhåll katalogerna" }),
  ).toBeInTheDocument();
  for (const name of ["Ingredienskatalog", "Taggar", "Recepttyper"])
    expect(screen.getByRole("button", { name })).toBeInTheDocument();
});
