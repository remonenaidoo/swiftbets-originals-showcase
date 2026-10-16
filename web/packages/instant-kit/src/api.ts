// The instant games' server API. The server decides every outcome; this file only carries requests and replies.
export class ApiError extends Error {
  constructor(public readonly code: string, public readonly status: number) {
    super(code);
  }
}

const messages: Record<string, string> = {
  invalid_stake: 'That stake is not offered.',
  invalid_seed: 'A client seed is 1 to 64 letters, digits, - or _.',
  session_invalid: 'Your game session has ended. Close the game and open it again.',
  insufficient_funds: 'Not enough balance for this stake.',
  limit_exceeded: 'You have reached a limit you set.',
  account_restricted: 'Play is not available on this account right now.',
  round_in_progress: 'Finish the round you are playing first (or wait a moment after playing).',
  round_over: 'That round is already over.',
  tile_taken: 'Pick a tile you have not revealed.',
  nothing_to_cash_out: 'Reveal a tile before cashing out.',
  betting_closed: 'Betting has closed for this round. Bet on the next one.',
  not_flying: 'The round has not started yet.',
  key_in_use: 'Please try again.',
  wallet_unavailable: 'The wallet is not answering. Please try again.',
  network: 'Connection lost. Please try again.',
};

export const describe = (e: unknown): string =>
  e instanceof ApiError ? messages[e.code] ?? 'Something went wrong. Please try again.' : messages.network;

export const session = new URLSearchParams(location.search).get('session') ?? '';

export async function call<T>(path: string, body?: unknown): Promise<T> {
  let res: Response;
  try {
    res = await fetch(import.meta.env.BASE_URL + path, body === undefined
      ? { credentials: 'omit' }
      : { method: 'POST', body: JSON.stringify({ session, ...body }), headers: { 'Content-Type': 'application/json' }, credentials: 'omit' });
  } catch {
    throw new ApiError('network', 0);
  }
  const reply = await res.json().catch(() => ({}));
  if (!res.ok) throw new ApiError((reply as { code?: string }).code ?? 'unknown', res.status);
  return reply as T;
}

export const query = (path: string) => `${path}${path.includes('?') ? '&' : '?'}session=${encodeURIComponent(session)}`;

export interface RoundSummary { roundId: string; stake: number; paid: number; status: string; at: string }

export interface Seeds { serverSeedHash: string; clientSeed: string; nonce: number; previousServerSeed: string | null; previousClientSeed: string | null; previousNonce: number | null; nextServerSeedHash: string }

/** A one-off idempotency key per action, so a retried request is applied once. */
export const key = () => crypto.randomUUID();

/** Basis points (×10,000) as a multiplier, e.g. 15000 → "1.50×". */
export const times = (bp: number, digits = 2) => `${(bp / 10000).toLocaleString('en-ZA', { minimumFractionDigits: digits, maximumFractionDigits: digits })}×`;
