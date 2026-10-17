// The SwiftBets Originals slot kit: a game supplies a SlotConfig and calls startSlot.
import './style.css';

export { startSlot } from './game';
export { imageArt, placeholderArt, assetUrl, type TileSpec } from './art';
export type { SlotConfig } from './config';
export type { GameInfo } from './api';
// Shared with the instant games.
export { sound } from './audio';
export { Tweens, ease } from './tweens';
export { rand } from './api';
export { Scene } from './scene';
export { HUD } from './hud';
export { wireSheets, open, close, renderHistory } from './sheets';
export { SPEEDS, savedSpeed, saveSpeed, type Speed } from './speed';
export type { SymbolInfo, RoundSummary } from './api';
