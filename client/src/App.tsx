import { Link, Route, Routes } from "react-router-dom";
import { HomePage } from "./HomePage";
import { PlayerPage } from "./PlayerPage";

export default function App() {
  return (
    <div className="shell">
      <header className="topbar">
        <Link to="/" className="brand">
          FFAggerator
        </Link>
        <p className="tagline">NFL news and fantasy projections</p>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/players/:id" element={<PlayerPage />} />
        </Routes>
      </main>
    </div>
  );
}
