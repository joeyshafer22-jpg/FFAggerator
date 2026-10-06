import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { api, type NflState, type PlayerSearch } from "./api";

export function HomePage() {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<PlayerSearch[]>([]);
  const [state, setState] = useState<NflState | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    api.state().then(setState).catch(() => setState(null));
  }, []);

  const debounced = useDebounced(query, 200);

  useEffect(() => {
    if (debounced.trim().length < 2) {
      setResults([]);
      setError(null);
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    api
      .searchPlayers(debounced.trim())
      .then((players) => {
        if (!cancelled) {
          setResults(players);
          setError(null);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError("Could not search players. Is the API running?");
          setResults([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [debounced]);

  const status = useMemo(() => {
    if (!state) {
      return "Connecting to API…";
    }
    if (!state.ready) {
      return "Loading Sleeper player catalog. This can take a minute on first run.";
    }
    const week = state.week ? `Week ${state.week}` : "Week TBD";
    return `${state.season ?? ""} ${state.seasonType ?? "regular"} · ${week} · ${state.activePlayerCount} players cached`;
  }, [state]);

  return (
    <section className="search-panel">
      <h1>Find a player</h1>
      <p className="lede">Search by name, then open a hub of weekly projections and current news.</p>
      <p className="meta">{status}</p>
      <label className="search-label">
        Player name
        <input
          autoFocus
          value={query}
          onChange={(event) => setQuery(event.target.value)}
          placeholder="Start typing, e.g. Mahomes"
        />
      </label>
      {loading && <p className="meta">Searching…</p>}
      {error && <p className="error">{error}</p>}
      {!loading && debounced.trim().length >= 2 && results.length === 0 && !error && (
        <p className="empty">No active fantasy players matched “{debounced.trim()}”.</p>
      )}
      <ul className="results">
        {results.map((player) => (
          <li key={player.id}>
            <Link to={`/players/${player.id}`}>
              <span className="name">{player.fullName}</span>
              <span className="chips">
                {player.position && <span>{player.position}</span>}
                {player.team && <span>{player.team}</span>}
                {player.injuryStatus && <span className="injury">{player.injuryStatus}</span>}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

function useDebounced(value: string, delayMs: number) {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const handle = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(handle);
  }, [value, delayMs]);
  return debounced;
}
