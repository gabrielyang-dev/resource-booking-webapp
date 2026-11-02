import { useEffect, useState } from "react";
import { api, ApiError, describeError, setOnNotSignedIn, User } from "./api";
import { HealthStatus } from "./features/health/HealthStatus";
import { SignIn } from "./features/session/SignIn";
import { UsersPage } from "./features/users/UsersPage";
import { CataloguePage } from "./features/catalogue/CataloguePage";
import { FindResourcePage } from "./features/catalogue/FindResourcePage";
import { MyBookingsPage } from "./features/bookings/MyBookingsPage";
import { InsightsPage } from "./features/insights/InsightsPage";
import { ApprovalsPage } from "./features/approvals/ApprovalsPage";
import { NotificationsPage } from "./features/notifications/NotificationsPage";
import { OfficeTodayPage } from "./features/bookings/OfficeTodayPage";

// No routing library: the view is just state.
type View = "home" | "find" | "mine" | "approvals" | "notifications" | "users" | "catalogue" | "insights";

export function App() {
  // undefined = still checking the cookie, null = signed out.
  const [user, setUser] = useState<User | null | undefined>(undefined);
  const [view, setView] = useState<View>("home");
  const [notice, setNotice] = useState("");
  const [unread, setUnread] = useState(0);
  // Bookings waiting for this user to approve.
  const [waiting, setWaiting] = useState(0);

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

  // The number on the bell, refreshed whenever the page or the user changes.
  useEffect(() => {
    if (!user) return;
    api<{ unread: number }>("GET", "/api/notifications/summary")
      .then((summary) => setUnread(summary.unread))
      .catch(() => setUnread(0));
    api<{ totalCount: number }>("GET", "/api/bookings/approvals?pageSize=1")
      .then((page) => setWaiting(page.totalCount))
      .catch(() => setWaiting(0));
  }, [user, view]);

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

  // A rail item. The page you are on is marked; a count shows when something is waiting.
  function navButton(target: View, label: string, count = 0) {
    return (
      <button className={view === target ? "active" : ""} onClick={() => setView(target)} aria-current={view === target ? "page" : undefined}>
        <span>{label}</span>
        {count > 0 && <span className="count" aria-label={`${count} waiting`}>{count}</span>}
      </button>
    );
  }

  if (user === undefined) {
    return <main className="main"><p className="muted">Loading...</p></main>;
  }

  if (user === null) {
    return (
      <main className="main">
        {notice && <div className="notice">{notice}</div>}
        <SignIn onSignedIn={handleSignedIn} />
        <footer className="footer"><HealthStatus /></footer>
      </main>
    );
  }

  return (
    <div className="shell">
      <nav className="rail" aria-label="Main">
        <div className="place">Resource booking<small>London office</small></div>
        {navButton("home", "Office today")}
        {navButton("find", "Find a resource")}
        {navButton("mine", "My bookings")}
        {navButton("approvals", "Approvals", waiting)}
        {navButton("notifications", "Notifications", unread)}
        {user.role === "Admin" && (
          <>
            <div className="group">Admin</div>
            {navButton("insights", "Insights")}
            {navButton("catalogue", "Catalogue")}
            {navButton("users", "Users")}
            <button onClick={resetDemoData}>Reset demo data</button>
          </>
        )}
        <div className="who">
          <span>Signed in as <strong>{user.name}</strong></span>
          <button onClick={switchUser}>Switch user</button>
        </div>
      </nav>

      <main className="main">
        {notice && <div className="notice">{notice}</div>}
        {view === "home" && <OfficeTodayPage />}
        {view === "find" && <FindResourcePage />}
        {view === "mine" && <MyBookingsPage />}
        {view === "approvals" && <ApprovalsPage />}
        {view === "notifications" && <NotificationsPage onRead={() => setUnread(0)} />}
        {user.role === "Admin" && view === "users" && <UsersPage />}
        {user.role === "Admin" && view === "catalogue" && <CataloguePage />}
        {user.role === "Admin" && view === "insights" && <InsightsPage />}
        <footer className="footer"><HealthStatus /></footer>
      </main>
    </div>
  );
}
