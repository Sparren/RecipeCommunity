import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, expect, it, vi } from "vitest";
import { TagTypeAdmin } from "./TagTypeAdmin";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

it.each(["tags", "recipe-types"] as const)(
  "maintains translations and lifecycle through the %s form",
  async (kind) => {
    const item = {
      id: "entry",
      name: "Befintlig",
      names: [{ language: "sv", name: "Befintlig" }],
      isActive: false,
      sortOrder: 3,
    };
    const fetch = vi.fn(
      async (_url: RequestInfo | URL, init?: RequestInit) =>
        new Response(JSON.stringify(init?.method ? { id: "entry" } : [item])),
    );
    vi.stubGlobal("fetch", fetch);
    const onSaved = vi.fn(async () => {});
    const setDirty = vi.fn();
    render(<TagTypeAdmin kind={kind} setDirty={setDirty} onSaved={onSaved} />);
    const user = userEvent.setup();
    expect(screen.getByRole("combobox", { name: "Statusfilter" })).toHaveValue(
      "",
    );
    await user.click(
      await screen.findByRole("button", { name: /Befintlig.*Inaktiv/ }),
    );
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Status" }),
      "true",
    );
    await user.click(screen.getByRole("button", { name: "+ Lägg till språk" }));
    await user.type(screen.getAllByLabelText("Språk")[1], "en");
    await user.type(screen.getAllByLabelText("Namn")[1], "Translated");
    await user.click(screen.getByRole("button", { name: "+ Lägg till alias" }));
    await user.type(screen.getByLabelText("Alias"), "Alternativt namn");
    if (kind === "recipe-types") {
      await user.clear(screen.getByRole("spinbutton"));
      await user.type(screen.getByRole("spinbutton"), "12");
    }
    await waitFor(() => expect(setDirty).toHaveBeenLastCalledWith(true));
    await user.click(
      screen.getByRole("button", {
        name: kind === "tags" ? "Spara tagg" : "Spara recepttyp",
      }),
    );
    await waitFor(() => expect(onSaved).toHaveBeenCalledOnce());
    const write = fetch.mock.calls.find(([, init]) => init?.method === "PUT")!;
    expect(write[0]).toBe(`/api/${kind}/entry`);
    expect(JSON.parse(write[1]!.body as string)).toEqual({
      names: [
        { language: "sv", name: "Befintlig" },
        { language: "en", name: "Translated" },
      ],
      isActive: true,
      aliases: [{ language: "sv", name: "Alternativt namn" }],
      ...(kind === "recipe-types" ? { sortOrder: 12 } : {}),
    });
    expect(setDirty).toHaveBeenLastCalledWith(false);
    await user.selectOptions(
      screen.getByRole("combobox", { name: "Statusfilter" }),
      "false",
    );
    await waitFor(() =>
      expect(fetch).toHaveBeenLastCalledWith(
        expect.stringContaining("isActive=false"),
        expect.anything(),
      ),
    );
  },
);

it("protects unsaved catalogue edits when selecting another entry", async () => {
  vi.stubGlobal(
    "fetch",
    vi.fn(
      async () =>
        new Response(
          JSON.stringify([
            {
              id: "id",
              name: "Befintlig",
              names: [{ language: "sv", name: "Befintlig" }],
              isActive: true,
            },
          ]),
        ),
    ),
  );
  const confirm = vi.spyOn(window, "confirm").mockReturnValue(false);
  render(<TagTypeAdmin kind="tags" setDirty={vi.fn()} onSaved={vi.fn()} />);
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Namn"), "Osparad");
  await user.click(
    await screen.findByRole("button", { name: "Befintlig Aktiv" }),
  );
  expect(confirm).toHaveBeenCalledOnce();
  expect(screen.getByLabelText("Namn")).toHaveValue("Osparad");
});
