export type NflState = {
  season: number | null;
  week: number | null;
  displayWeek: number | null;
  seasonType: string | null;
  fetchedAt: string | null;
  playersFetchedAt: string | null;
  newsFetchedAt: string | null;
  activePlayerCount: number;
  ready: boolean;
};

export type PlayerSearch = {
  id: string;
  fullName: string;
  team: string | null;
  position: string | null;
  injuryStatus: string | null;
  status: string | null;
  number: number | null;
  searchRank: number | null;
};

export type PlayerDetail = {
  id: string;
  fullName: string;
  firstName: string;
  lastName: string;
  team: string | null;
  position: string | null;
  injuryStatus: string | null;
  status: string | null;
  number: number | null;
  active: boolean;
  updatedAt: string;
  season: number | null;
  week: number | null;
  seasonType: string | null;
};

export type WeeklyProjection = {
  week: number;
  season: number;
  seasonType: string;
  ptsPpr: number | null;
  ptsHalf: number | null;
  ptsStd: number | null;
  fetchedAt: string;
};

export type ProjectionResponse = {
  playerId: string;
  season: number | null;
  week: number | null;
  seasonType: string | null;
  isEmpty: boolean;
  current: WeeklyProjection | null;
  fallback: WeeklyProjection | null;
  fetchedAt: string | null;
  message: string | null;
};

export type NewsItem = {
  title: string;
  url: string;
  source: string;
  publishedAt: string | null;
  summary: string | null;
  fetchedAt: string;
};

export type NewsResponse = {
  playerId: string;
  items: NewsItem[];
  espnFetchedAt: string | null;
  googleFetchedAt: string | null;
  isEmpty: boolean;
};

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path);
  if (!response.ok) {
    throw new Error(`${response.status} ${response.statusText}`);
  }
  return response.json() as Promise<T>;
}

export const api = {
  state: () => getJson<NflState>("/api/nfl/state"),
  searchPlayers: (q: string) =>
    getJson<PlayerSearch[]>(`/api/players?q=${encodeURIComponent(q)}`),
  player: (id: string) => getJson<PlayerDetail>(`/api/players/${encodeURIComponent(id)}`),
  projections: (id: string) =>
    getJson<ProjectionResponse>(`/api/players/${encodeURIComponent(id)}/projections`),
  news: (id: string) =>
    getJson<NewsResponse>(`/api/players/${encodeURIComponent(id)}/news?limit=20`)
};
