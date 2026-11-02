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
