import { useEffect, useState } from "react";
import { api, ApiError, describeError, setOnNotSignedIn, User } from "./api";
import { HealthStatus } from "./features/health/HealthStatus";
import { SignIn } from "./features/session/SignIn";
import { UsersPage } from "./features/users/UsersPage";
import { CataloguePage } from "./features/catalogue/CataloguePage";
import { FindResourcePage } from "./features/catalogue/FindResourcePage";
import { MyBookingsPage } from "./features/bookings/MyBookingsPage";

// No routing library: the view is just state.
type View = "home" | "find" | "mine" | "users" | "catalogue";

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

  return (
    <main style={{ fontFamily: "sans-serif", margin: "2rem" }}>
      <header style={{ display: "flex", gap: "1rem", alignItems: "center", marginBottom: "1rem" }}>
        <h1 style={{ margin: 0 }}>Resource booking</h1>
        {user && (
          <>
            <span>{user.name} ({user.role})</span>
            <button onClick={switchUser}>Switch user</button>
            <button onClick={() => setView("home")}>Home</button>
            <button onClick={() => setView("find")}>Find a resource</button>
            <button onClick={() => setView("mine")}>My bookings</button>
            {user.role === "Admin" && (
              <>
                <button onClick={() => setView("users")}>Users</button>
                <button onClick={() => setView("catalogue")}>Catalogue</button>
                <button onClick={resetDemoData}>Reset demo data</button>
              </>
            )}
          </>
        )}
      </header>

      {notice && <p>{notice}</p>}

      {user === undefined && <p>Loading...</p>}
      {user === null && <SignIn onSignedIn={handleSignedIn} />}
      {user && view === "home" && <p>Welcome, {user.name}.</p>}
      {user && view === "find" && <FindResourcePage />}
      {user && view === "mine" && <MyBookingsPage />}
      {user && user.role === "Admin" && view === "users" && <UsersPage />}
      {user && user.role === "Admin" && view === "catalogue" && <CataloguePage />}

      <footer style={{ marginTop: "2rem" }}>
        <HealthStatus />
      </footer>
    </main>
  );
}
