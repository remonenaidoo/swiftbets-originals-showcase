// The SwiftBets Originals instant-game kit: a game mounts the shell and draws its own stage in PixiJS.
export { mountShell, seedsHtml, type Shell, type ShellConfig } from './shell';
export { call, query, describe, key, times, session, ApiError, type RoundSummary, type Seeds } from './api';
export { floats, sha256, minesLayout, crashPoint, kenoDraw } from './fair';
export { bootInstant, type InstantState, type BootOptions } from './boot';
