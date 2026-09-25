import type { Quantity, Unit } from "../types";
export function QuantityFields({
  quantity,
  unitId,
  units,
  onChange,
  required = false,
  prefix = "",
}: {
  quantity: Quantity;
  unitId: string | null;
  units: Unit[];
  required?: boolean;
  prefix?: string;
  onChange: (quantity: Quantity, unitId: string | null) => void;
}) {
  return (
    <div className="quantity">
      <label>
        {prefix}Mängd
        <input
          type="number"
          min="0.000001"
          step="any"
          required={required}
          value={quantity.value ?? ""}
          onChange={(e) =>
            onChange(
              {
                ...quantity,
                value: e.target.value === "" ? null : Number(e.target.value),
              },
              unitId,
            )
          }
        />
      </label>
      <span className="range-dash">–</span>
      <label>
        {prefix}Max (valfritt)
        <input
          type="number"
          min="0.000001"
          step="any"
          value={quantity.maximumValue ?? ""}
          onChange={(e) =>
            onChange(
              {
                ...quantity,
                maximumValue:
                  e.target.value === "" ? null : Number(e.target.value),
              },
              unitId,
            )
          }
        />
      </label>
      <label>
        {prefix}Enhet
        <select
          required={required || quantity.value !== null}
          value={unitId ?? ""}
          onChange={(e) => onChange(quantity, e.target.value || null)}
        >
          <option value="">Ingen enhet</option>
          {units.map((u) => (
            <option key={u.id} value={u.id}>
              {u.name} ({u.symbol})
            </option>
          ))}
        </select>
      </label>
    </div>
  );
}
