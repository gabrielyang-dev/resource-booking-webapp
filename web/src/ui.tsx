// Small pieces of UI used on several pages. The look comes from styles.css.

export function StatusBadge({ status }: { status: string }) {
  // "CheckedIn" reads as "Checked in".
  const label = status.replace(/([a-z])([A-Z])/g, "$1 $2").toLowerCase();
  return <span className={`badge ${status}`}>{label.charAt(0).toUpperCase() + label.slice(1)}</span>;
}

// Errors in red, then an optional success message in green.
export function Messages({ errors, success }: { errors: string[]; success?: string }) {
  return (
    <>
      {errors.length > 0 && (
        <div className="error" role="alert">
          {errors.length === 1 ? errors[0] : (
            <>
              <strong>{errors[0]}</strong>
              <ul style={{ margin: "0.35rem 0 0" }}>
                {errors.slice(1).map((m) => <li key={m}>{m}</li>)}
              </ul>
            </>
          )}
        </div>
      )}
      {success && <div className="success" role="status">{success}</div>}
    </>
  );
}

// Each resource type gets its own signage colour, worked out from its name so it never changes.
// Used as the CSS variable --type on door plates, timeline blocks and board rows.
const typeColours = ["#3d6cb9", "#2e8b6f", "#8a6340", "#6b5bb5", "#1f8a9e", "#8a7a1f"];

export function typeColour(typeName: string): string {
  let hash = 0;
  for (const letter of typeName) hash = (hash * 22 + letter.charCodeAt(0)) % 9973;
  return typeColours[hash % typeColours.length];
}
