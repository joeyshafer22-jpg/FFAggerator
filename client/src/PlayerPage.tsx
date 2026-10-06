import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import {
  api,
  type NewsResponse,
  type PlayerDetail,
  type ProjectionResponse
} from "./api";

type Scoring = "ppr" | "half" | "std";

export function PlayerPage() {
  const { id } = useParams();
  const [player, setPlayer] = useState<PlayerDetail | null>(null);
  const [projections, setProjections] = useState<ProjectionResponse | null>(null);
  const [news, setNews] = useState<NewsResponse | null>(null);
  const [scoring, setScoring] = useState<Scoring>("ppr");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) {
      return;
    }

    let cancelled = false;
    setLoading(true);
    Promise.all([api.player(id), api.projections(id), api.news(id)])
      .then(([nextPlayer, nextProjections, nextNews]) => {
        if (!cancelled) {
          setPlayer(nextPlayer);
          setProjections(nextProjections);
          setNews(nextNews);
          setError(null);
        }
      })
      .catch((err: Error) => {
        if (!cancelled) {
          setError(err.message.includes("404") ? "Player not found." : "Could not load this player hub.");
          setPlayer(null);
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
  }, [id]);

  if (loading) {
    return <p className="meta">Loading player hub…</p>;
  }

  if (error || !player) {
    return (
      <section>
        <p className="error">{error ?? "Player not found."}</p>
        <Link to="/">Back to search</Link>
      </section>
    );
  }

  const shown = projections?.current ?? projections?.fallback ?? null;
  const points = shown ? pointsFor(shown, scoring) : null;
  const staleLabel = formatCache(projections?.fetchedAt, news?.googleFetchedAt ?? news?.espnFetchedAt);

  return (
    <article className="hub">
      <p className="crumb">
        <Link to="/">Search</Link>
      </p>
      <header className="player-header">
        <div>
          <h1>{player.fullName}</h1>
          <p className="chips">
            {player.position && <span>{player.position}</span>}
            {player.team && <span>{player.team}</span>}
            {player.number != null && <span>#{player.number}</span>}
            {player.status && <span>{player.status}</span>}
            {player.injuryStatus && <span className="injury">{player.injuryStatus}</span>}
          </p>
        </div>
        <p className="meta">
          {player.season ? `${player.season} · ` : ""}
          {player.week ? `Week ${player.week}` : "Week TBD"}
        </p>
      </header>

      <section className="card">
        <div className="card-head">
          <h2>Fantasy projection</h2>
          <div className="toggles" role="group" aria-label="Scoring format">
            <button className={scoring === "ppr" ? "active" : ""} onClick={() => setScoring("ppr")}>
              PPR
            </button>
            <button className={scoring === "half" ? "active" : ""} onClick={() => setScoring("half")}>
              Half
            </button>
            <button className={scoring === "std" ? "active" : ""} onClick={() => setScoring("std")}>
              Standard
            </button>
          </div>
        </div>
        {projections?.isEmpty && !shown && (
          <p className="empty">{projections.message ?? "No projection for this week."}</p>
        )}
        {projections?.isEmpty && shown && (
          <p className="meta">
            No current-week line. Showing week {shown.week} as a fallback.
          </p>
        )}
        {shown && (
          <p className="points">
            {points == null ? "—" : points.toFixed(1)}
            <span> projected pts ({labelFor(scoring)})</span>
          </p>
        )}
        {staleLabel && <p className="meta">{staleLabel}</p>}
      </section>

      <section className="card">
        <h2>News</h2>
        {news?.isEmpty && <p className="empty">No recent headlines matched this player.</p>}
        <ul className="news">
          {news?.items.map((item) => (
            <li key={item.url}>
              <a href={item.url} target="_blank" rel="noreferrer">
                {item.title}
              </a>
              <p className="meta">
                {item.source}
                {item.publishedAt ? ` · ${new Date(item.publishedAt).toLocaleString()}` : ""}
              </p>
            </li>
          ))}
        </ul>
      </section>
    </article>
  );
}

function pointsFor(row: NonNullable<ProjectionResponse["current"]>, scoring: Scoring) {
  if (scoring === "ppr") return row.ptsPpr;
  if (scoring === "half") return row.ptsHalf;
  return row.ptsStd;
}

function labelFor(scoring: Scoring) {
  if (scoring === "ppr") return "PPR";
  if (scoring === "half") return "half PPR";
  return "standard";
}

function formatCache(projectionAt: string | null | undefined, newsAt: string | null | undefined) {
  const parts: string[] = [];
  if (projectionAt) {
    parts.push(`Projections cached ${new Date(projectionAt).toLocaleString()}`);
  }
  if (newsAt) {
    parts.push(`News cached ${new Date(newsAt).toLocaleString()}`);
  }
  return parts.join(" · ");
}
