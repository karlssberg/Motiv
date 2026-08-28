/**
 * The `+` on a row. It means the same thing on every row: *insert a sibling immediately after me*.
 *
 * One rule, no per-kind cases — an earlier draft gave operator rows "insert at index 0" so that
 * every operand slot would be button-reachable, which does not work and cannot: a row sits in both
 * its parent's list and its own children's, so `and: [a, {or: [b, c]}, d]` has seven slots and six
 * rows. The position `+` cannot reach — before an operator's first child — is offered by the row's
 * `⋯` menu instead.
 *
 * Joins the hover-revealed cluster `⋯` and `📌` already form, inheriting their reveal and spacing.
 */
export function NodeInsertButton(props: { path: string; onOpen: () => void }) {
  return (
    <button
      type="button"
      className="node-insert"
      aria-label={`insert after ${props.path}`}
      onClick={props.onOpen}
    >
      ＋
    </button>
  );
}
