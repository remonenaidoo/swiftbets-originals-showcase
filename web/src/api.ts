// The game's server API. The server decides every outcome; this file only carries requests and replies.
export type Sym = 'Lion' | 'Elephant' | 'Rhino' | 'Buffalo' | 'Ace' | 'King' | 'Queen' | 'Jack' | 'Ten' | 'Wild' | 'Scatter';

export interface LineWin { line: number; symbol: Sym; count: number; units: number }

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
}

export interface GameInfo {
  gameId: string;
  name: string;
  rtp: number;
  maxWinStakes: number;
  stakes: number[];
  lines: number;
  pays: Record<string, number[]>;
  scatterPays: number[];
  freeSpins: number;
  freeSpinMultiplier: number;
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
  wallet_unavailable: 'The wallet is not answering. Please try again.',
  network: 'Connection lost. Please try again.',
};

export const describe = (e: unknown): string =>
  e instanceof ApiError ? messages[e.code] ?? 'Something went wrong. Please try again.' : messages.network;

const session = new URLSearchParams(location.search).get('session') ?? '';
const base = new URL('.', location.href).pathname; // "/originals/" behind the gateway

async function call<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(base + path, { ...init, headers: { 'Content-Type': 'application/json' }, credentials: 'omit' });
  } catch {
    throw new ApiError('network', 0);
  }
  const body = await res.json().catch(() => ({}));
  if (!res.ok) throw new ApiError((body as { code?: string }).code ?? 'unknown', res.status);
  return body as T;
}

export const api = {
  hasSession: () => session.length > 0,
  state: () => call<StateView>(`slot/state?session=${encodeURIComponent(session)}`),
  spin: (stake: number, key: string) => call<SpinView>('slot/spin', { method: 'POST', body: JSON.stringify({ session, stake, key }) }),
  next: (roundId: string) => call<SpinView>('slot/feature/next', { method: 'POST', body: JSON.stringify({ session, roundId }) }),
};

export const rand = (minor: number): string =>
  'R ' + (minor / 100).toLocaleString('en-ZA', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
