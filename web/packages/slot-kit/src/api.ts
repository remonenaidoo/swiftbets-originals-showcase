// A game's server API. The server decides every outcome; this file only carries requests and replies.
export type Sym = string;

export interface LineWin { line: number; symbol: Sym; count: number; reels: number; units: number }

export interface LockedCell { reel: number; row: number; symbol: Sym }

export interface SpinView {
  roundId: string;
  seq: number;
  stops: number[];
  lines: LineWin[];
  scatters: number;
  win: number;
  roundPaid: number;
  freeSpinsLeft: number;
  featureTriggered: boolean;
  capReached: boolean;
  balance: number | null;
  locked: LockedCell[];
}

export interface SymbolInfo { name: Sym; kind: 'Regular' | 'Wild' | 'Scatter'; weight: number; paysAs: Sym }

export interface GameInfo {
  gameId: string;
  name: string;
  /** The rate the game pays at, in basis points of the design's 96% (9,600). */
  rateBp: number;
  maxWinStakes: number;
  stakes: number[];
  lines: number;
  symbols: SymbolInfo[];
  pays: Record<string, number[]>;
  scatterPays: number[];
  freeSpins: number;
  freeSpinMultiplier: number;
  retrigger: boolean;
  lockSplits: boolean;
  paylines: number[][];
  baseReels: Sym[][];
  freeReels: Sym[][];
}

export interface RoundSummary { roundId: string; stake: number; paid: number; status: string; at: string }

export interface StateView { balance: number | null; openFeature: SpinView | null; recent: RoundSummary[]; info: GameInfo }

export class ApiError extends Error {
  constructor(public readonly code: string, public readonly status: number) {
    super(code);
  }
}

const messages: Record<string, string> = {
  invalid_stake: 'That stake is not offered.',
  session_invalid: 'Your game session has ended. Close the game and open it again.',
  insufficient_funds: 'Not enough balance for this stake.',
  limit_exceeded: 'You have reached a limit you set.',
  account_restricted: 'Play is not available on this account right now.',
  feature_in_progress: 'Finish your free spins first.',
  no_feature: 'There are no free spins to play.',
  game_not_found: 'This game is not available.',
  wallet_unavailable: 'The wallet is not answering. Please try again.',
  network: 'Connection lost. Please try again.',
};

export const describe = (e: unknown): string =>
  e instanceof ApiError ? messages[e.code] ?? 'Something went wrong. Please try again.' : messages.network;

const session = new URLSearchParams(location.search).get('session') ?? '';

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(import.meta.env.BASE_URL + path, { ...init, headers: { 'Content-Type': 'application/json' }, credentials: 'omit' });
  } catch {
    throw new ApiError('network', 0);
  }
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new ApiError((body as { code?: string }).code ?? 'unknown', res.status);
  return body as T;
}

export type Api = ReturnType<typeof apiFor>;

/** The API of one game, behind the gateway at /originals/games/{gameId}/. */
export function apiFor(gameId: string) {
  const at = `games/${encodeURIComponent(gameId)}/`;
  return {
    hasSession: () => session.length > 0,
    state: () => call<StateView>(`${at}state?session=${encodeURIComponent(session)}`),
    spin: (stake: number, key: string) => call<SpinView>(`${at}spin`, { method: 'POST', body: JSON.stringify({ session, stake, key }) }),
    next: (roundId: string) => call<SpinView>(`${at}feature/next`, { method: 'POST', body: JSON.stringify({ session, roundId }) }),
  };
}

export const rand = (minor: number): string =>
  'R ' + (minor / 100).toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
