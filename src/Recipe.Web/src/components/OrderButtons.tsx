export function move<T>(items: T[], index: number, delta: number): T[] {
  const next = [...items];
  const to = index + delta;
  if (to < 0 || to >= next.length) return next;
  [next[index], next[to]] = [next[to], next[index]];
  return next;
}
export function OrderButtons({
  index,
  count,
  onMove,
  onRemove,
  label,
}: {
  index: number;
  count: number;
  onMove: (delta: number) => void;
  onRemove: () => void;
  label: string;
}) {
  return (
    <div className="order-buttons">
      <button
        type="button"
        disabled={index === 0}
        onClick={() => onMove(-1)}
        aria-label={`${label}: flytta upp`}
      >
        ↑
      </button>
      <button
        type="button"
        disabled={index === count - 1}
        onClick={() => onMove(1)}
        aria-label={`${label}: flytta ner`}
      >
        ↓
      </button>
      <button
        className="quiet danger"
        type="button"
        onClick={onRemove}
        aria-label={`${label}: ta bort`}
      >
        Ta bort
      </button>
    </div>
  );
}
