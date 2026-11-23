/* ==========================================================================
   ХРАНИЛИЩЕ РЕКОРДОВ И КОНЦОВОК (100% ЗАЩИЩЕННЫЙ TRY/CATCH ДЛЯ SANDBOX)
   ========================================================================== */
import { STORAGE_KEY } from './config.js';

const SOUND_STORAGE_KEY = 'gazyava_v2_sound_muted';

export function getDefaultRecords() {
  return {
    bestScore: 0,
    bestTimeSec: 0,
    totalGoldenSips: 0,
    totalHrumkas: 0,
    hardcoreCleared: false,
    endings: []
  };
}

export function loadRecords() {
  try {
    const raw = typeof window !== 'undefined' && window.localStorage ? window.localStorage.getItem(STORAGE_KEY) : null;
    if (!raw) return getDefaultRecords();
    const parsed = JSON.parse(raw);
    return {
      bestScore: Number(parsed.bestScore) || 0,
      bestTimeSec: Number(parsed.bestTimeSec) || 0,
      totalGoldenSips: Number(parsed.totalGoldenSips) || 0,
      totalHrumkas: Number(parsed.totalHrumkas) || 0,
      hardcoreCleared: Boolean(parsed.hardcoreCleared),
      endings: Array.isArray(parsed.endings) ? parsed.endings : []
    };
  } catch (err) {
    return getDefaultRecords();
  }
}

export const records = loadRecords();

export function saveRecords(rec = records) {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(rec));
    }
  } catch (err) {
    // В изолированной песочнице iOS-редакторов просто храним в памяти текущей сессии
  }
}

export function loadMuteSetting() {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      const val = window.localStorage.getItem(SOUND_STORAGE_KEY);
      return val === 'true';
    }
  } catch (err) {}
  return false;
}

export function saveMuteSetting(muted) {
  try {
    if (typeof window !== 'undefined' && window.localStorage) {
      window.localStorage.setItem(SOUND_STORAGE_KEY, String(Boolean(muted)));
    }
  } catch (err) {}
}
