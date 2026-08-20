import {
    BrowserRouter,
    NavLink,
    Navigate,
    Route,
    Routes,
} from "react-router-dom";

import AddTransactionPage from "./pages/AddTransactionPage";
import MonitorPage from "./pages/MonitorPage";

import "./App.css";

function App() {
    return (
        <BrowserRouter>
            <div className="app-shell">
                <header className="app-header">
                    <div>
                        <h1>Real-Time Financial Monitor</h1>
                        <p>Transaction ingestion and live monitoring</p>
                    </div>

                    <nav className="app-nav">
                        <NavLink
                            to="/add"
                            className={({ isActive }) =>
                                isActive
                                    ? "nav-link active"
                                    : "nav-link"
                            }
                        >
                            Add Transaction
                        </NavLink>

                        <NavLink
                            to="/monitor"
                            className={({ isActive }) =>
                                isActive
                                    ? "nav-link active"
                                    : "nav-link"
                            }
                        >
                            Live Monitor
                        </NavLink>
                    </nav>
                </header>

                <main className="app-content">
                    <Routes>
                        <Route
                            path="/add"
                            element={<AddTransactionPage />}
                        />

                        <Route
                            path="/monitor"
                            element={<MonitorPage />}
                        />

                        <Route
                            path="/"
                            element={
                                <Navigate
                                    to="/monitor"
                                    replace
                                />
                            }
                        />
                    </Routes>
                </main>
            </div>
        </BrowserRouter>
    );
}

export default App;