import { useEffect, useState } from "react";
import { api, ApiError, describeError, setOnNotSignedIn, User } from "./api";
import { HealthStatus } from "./features/health/HealthStatus";
import { SignIn } from "./features/session/SignIn";
import { UsersPage } from "./features/users/UsersPage";
import { CataloguePage } from "./features/catalogue/CataloguePage";
import { FindResourcePage } from "./features/catalogue/FindResourcePage";
import { MyBookingsPage } from "./features/bookings/MyBookingsPage";
import { InsightsPage } from "./features/insights/InsightsPage";

// No routing library: the view is just state.
type View = "home" | "find" | "mine" | "users" | "catalogue" | "insights";

export function App() {
  // undefined = still checking the cookie, null = signed out.
  const [user, setUser] = useState<User | null | undefined>(undefined);
  const [view, setView] = useState<View>("home");
  const [notice, setNotice] = useState("");

  function backToPicker() {
    setUser(null);
    setView("home");
  }

  useEffect(() => {
    setOnNotSignedIn(backToPicker);
    async function checkCookie() {
      try {
        setUser(await api<User>("GET", "/api/me"));
      } catch (error) {
        if (!(error instanceof ApiError) || error.status !== 401) {
          setNotice(describeError(error)[0]);
        }
        setUser(null);
      }
    }
    checkCookie();
  }, []);

  async function switchUser() {
    try {
      await api("DELETE", "/api/session");
      setNotice("");
      backToPicker();
    } catch (error) {
      setNotice(describeError(error)[0]);
    }
  }

  async function resetDemoData() {
    if (!window.confirm("Reset demo data? Added users, catalogue changes and edits will be lost.")) {
      return;
    }
    try {
      await api("POST", "/api/demo/reset");
      setNotice("Demo data was reset.");
      setView("home");
    } catch (error) {
      setNotice(describeError(error)[0]);
    }
  }

  function handleSignedIn(signedIn: User) {
    setNotice("");
    setUser(signedIn);
  }

  // A nav button that shows which page you are on.
  function navButton(target: View, label: string) {
    return (
      <button className={view === target ? "active" : ""} onClick={() => setView(target)}>{label}</button>
    );
  }

  return (
    <>
      <header className="topbar">
        <h1>Resource booking</h1>
        {user && (
          <>
            <nav className="nav">
              {navButton("home", "Home")}
              {navButton("find", "Find a resource")}
              {navButton("mine", "My bookings")}
              {user.role === "Admin" && (
                <>
                  {navButton("insights", "Insights")}
                  {navButton("catalogue", "Catalogue")}
                  {navButton("users", "Users")}
                </>
              )}
            </nav>
            <div className="user-menu">
              <span>{user.name} · {user.role}</span>
              {user.role === "Admin" && <button onClick={resetDemoData}>Reset demo data</button>}
              <button onClick={switchUser}>Switch user</button>
            </div>
          </>
        )}
      </header>

      <main className="content">
        {notice && <div className="notice">{notice}</div>}

        {user === undefined && <p className="muted">Loading...</p>}
        {user === null && <SignIn onSignedIn={handleSignedIn} />}
        {user && view === "home" && (
          <section>
            <h2>Welcome, {user.name}</h2>
            <div className="grid">
              <div className="card">
                <strong>Book something</strong>
                <span className="muted">Rooms, desks, the pool car and equipment.</span>
                <button className="primary" onClick={() => setView("find")}>Find a resource</button>
              </div>
              <div className="card">
                <strong>Your bookings</strong>
                <span className="muted">Change the time or cancel if plans change.</span>
                <button onClick={() => setView("mine")}>My bookings</button>
              </div>
              {user.role === "Admin" && (
                <div className="card">
                  <strong>How space is used</strong>
                  <span className="muted">Utilisation per resource and the full booking history.</span>
                  <button onClick={() => setView("insights")}>Insights</button>
                </div>
              )}
            </div>
          </section>
        )}
        {user && view === "find" && <FindResourcePage />}
        {user && view === "mine" && <MyBookingsPage />}
        {user && user.role === "Admin" && view === "users" && <UsersPage />}
        {user && user.role === "Admin" && view === "catalogue" && <CataloguePage />}
        {user && user.role === "Admin" && view === "insights" && <InsightsPage />}
      </main>

      <footer className="footer">
        <HealthStatus />
      </footer>
    </>
  );
}
