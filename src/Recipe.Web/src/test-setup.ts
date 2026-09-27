import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";
afterEach(cleanup);

// jsdom has no native dialog implementation; browser tests verify the actual modal behavior.
Object.defineProperty(HTMLDialogElement.prototype, "showModal", {
  configurable: true,
  value: function () {
    this.open = true;
  },
});
Object.defineProperty(HTMLDialogElement.prototype, "close", {
  configurable: true,
  value: function () {
    this.open = false;
  },
});
