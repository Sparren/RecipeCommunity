import type { Quantity } from "../../types";
export function formatQuantity(q: Quantity) {
  if (q.value === null) return "";
  const format = (n: number) =>
    n.toLocaleString("sv-SE", { maximumFractionDigits: 10 });
  return (
    format(q.value) +
    (q.maximumValue === null ? "" : `–${format(q.maximumValue)}`)
  );
}
