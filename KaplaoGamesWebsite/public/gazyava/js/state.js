/* ==========================================================================
   ИГРОВОЕ СОСТОЯНИЕ И ВРЕМЯ
   ========================================================================== */
import { MODES, DRINKS } from './config.js';

export function createInitialState(selectedMode = 'classic') {
  const mode = MODES[selectedMode] || MODES.classic;
  const bottles = {};
  Object.keys(DRINKS).forEach((k) => {
    bottles[k] = {
      temp: DRINKS[k].initTemp,
      frozenTaps: 0,
      liquidLevel: 0.88
    };
  });

  return {
    phase: 'start', // 'start' | 'playing' | 'ended'
    selectedMode,   // 'classic' | 'hardcore'
    mode,
    nightProgress: 0,
    realElapsed: 0,
    score: 0,

    thirst: mode.id === 'hardcore' ? 42 : 34,
    fizz: 0,
    sugarDebt: mode.id === 'hardcore' ? 18 : 0,
    noise: 0,

    goldenSips: 0,
    combos: 0,
    hrumkasEaten: 0,
    energySips: 0,
    energySipAccum: 0,
    brineSipsLeft: 3,
    flavorIdx: 0,

    // Текущий напиток и ввод
    activeDrink: 'cola',
    lastDrunkId: 'cola',
    isDrinking: false,
    activePointerId: null,
    freezerActive: false,

    // Таймеры эффектов
    ascensionTimer: 0,
    stunTimer: 0,
    vhsTimer: 0,
    energyTurboTimer: 0,
    heartbeatTick: 0,
    comboWindowTimer: 0,
    flatTimer: 0,
    flatRoastCd: 0,
    gulpTick: 0,
    tickerTimer: 4.5,
    tickerIdx: 0,

    // Состояние каждой бутылки
    bottles,

    // Состояние ночных мини-ивентов
    nextEventAt: mode.eventInterval,
    raidTriggered: false,
    eventIdx: 0,
    ev: {
      active: false,
      type: null, // 'cap' | 'dad' | 'bag' | 'raid'
      progress: 0,
      needed: 4,
      pressure: 0,
      timeLeft: 0,
      stepTick: 0,
      holdingSleep: false,
      angle: 0
    },

    lastResult: null
  };
}

export function formatGameTime(progressSec, modeObj) {
  const ratio = Math.min(1, Math.max(0, progressSec / modeObj.durationSec));
  const totalMin = Math.floor(modeObj.startMin + (modeObj.endMin - modeObj.startMin) * ratio);
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
}

export const state = createInitialState('classic');
