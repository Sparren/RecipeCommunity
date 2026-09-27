import { test, expect } from "@playwright/test";
import { createPrintSamples } from "../scripts/printSamples";

test("print media contains recipe content, excludes controls/gallery and allows long blocks to fragment", async ({
  page,
  request,
}, testInfo) => {
  const samples = await createPrintSamples(
    "http://localhost:5050",
    ` ${Date.now()}`,
  );
  for (const sample of samples) {
    await page.goto(`/#/recipes/${sample.id}`);
    await expect(
      page.getByRole("heading", { name: sample.name }),
    ).toBeVisible();
    if (sample.size === "kort") {
      for (let i = 0; i < 2; i++) {
        const uploaded = await request.post(
          `/api/recipes/${sample.id}/images`,
          {
            multipart: {
              file: {
                name: `print-${i}.png`,
                mimeType: "image/png",
                buffer: Buffer.from(
                  "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII=",
                  "base64",
                ),
              },
            },
          },
        );
        expect(uploaded.ok()).toBeTruthy();
      }
      await page.reload();
      await expect(page.locator(".hero-image")).toBeVisible();
      await page.evaluate(() => {
        window.print = () => {
          document.body.dataset.printCalled = "yes";
        };
      });
      await page.getByRole("button", { name: "Skriv ut", exact: true }).click();
      await expect(page.locator("body")).toHaveAttribute(
        "data-print-called",
        "yes",
      );
    }
    await page.emulateMedia({ media: "print" });
    await expect(page.locator("header")).toBeHidden();
    await expect(page.locator("footer")).toBeHidden();
    await expect(
      page.getByRole("button", { name: "Skriv ut", includeHidden: true }),
    ).toBeHidden();
    expect(
      await page
        .locator("button:visible, input:visible, select:visible, nav:visible")
        .count(),
    ).toBe(0);
    await expect(
      page.getByRole("heading", { name: sample.name }),
    ).toBeVisible();
    await expect(page.getByText(/Ger: 4–6 st/)).toBeVisible();
    await expect(page.getByText(/45 min/)).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Ingredienser", exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("heading", { name: "Gör så här" }),
    ).toBeVisible();
    await expect(page.locator(".steps li").last()).toContainText(
      "Avsluta och servera",
    );
    await expect(page.locator(".detail-columns")).toHaveCSS("display", "block");
    await expect(page.locator(".detail-columns .panel").first()).toHaveCSS(
      "break-inside",
      "auto",
    );
    if (sample.size === "kort") {
      await expect(page.locator(".recipe-gallery")).toBeHidden();
      expect(await page.locator("img:visible").count()).toBe(1);
      await expect(page.locator(".print-ingredient-group")).toHaveCSS(
        "break-inside",
        "avoid",
      );
      await expect(page.locator(".steps li").first()).toHaveCSS(
        "break-inside",
        "avoid",
      );
    }
    if (sample.size === "långt") {
      await expect(page.locator(".print-ingredient-group").nth(2)).toHaveCSS(
        "break-inside",
        "auto",
      );
      await expect(page.locator(".steps li").nth(8)).toHaveCSS(
        "break-inside",
        "auto",
      );
    }
    // Inspection artifacts, not pixel-perfect pagination assertions. Production uses window.print only.
    await page.pdf({
      path: testInfo.outputPath(`recipe-${sample.size}.pdf`),
      preferCSSPageSize: true,
      printBackground: true,
      displayHeaderFooter: false,
    });
    await page.emulateMedia({ media: "screen" });
  }
});
