/* ==========================================================================
   ИГРОВОЕ СОСТОЯНИЕ И ВРЕМЯ (TIER 2: ЧИСТЫЙ ИМПОРТ ТОЛЬКО CONFIG.JS)
   ========================================================================== */
import { MODES, DRINKS, ACTS } from './config.js';

export function createInitialState(selectedMode = 'classic') {
  const mode = MODES[selectedMode] || MODES.classic;
  const bottles = {};
  Object.keys(DRINKS).forEach((k) => {
    bottles[k] = {
      temp: DRINKS[k].initTemp,
      carb: DRINKS[k].initCarb !== undefined ? DRINKS[k].initCarb : (k === 'flat' ? 12 : 100),
      shakeFoamTimer: 0,
      frozenTaps: 0,
      liquidLevel: 0.88
    };
  });

  return {
    phase: 'start', // 'start' | 'playing' | 'ended'
    selectedMode,   // 'classic' | 'hardcore'
    mode,
    currentAct: 1,  // 1..4
    nightProgress: 0,
    realElapsed: 0,
    score: 0,

    thirst: mode.id === 'hardcore' ? 42 : 34,
    fizz: 0,
    sugarDebt: mode.id === 'hardcore' ? 18 : 0,
    noise: 0,

    // Новые механики Патча 3.0: Отрыжка, Клапан и Экономика
    burp: 0,             // 0..100
    isVentingBurp: false,
    bladder: 0,          // 0..100
    unflushedCount: 0,
    caps: 3,             // Стартовый запас крышечек
    totalCapsEarned: 3,
    shopOpen: false,

    // Активные баффы
    mintTimer: 0,            // Жвачка морозит жажду
    dragonBreathCharges: 0,  // Заряд огненно-ледяного газа
    dragonBreathSips: 0,     // Счётчик для секретной концовки
    silencerTimer: 0,        // Подушка-глушитель (-50% шума)

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
    actBannerTimer: 0,
    lastNotifTimer: 0,
    nextNotifAt: 24,

    // Состояние каждой бутылки
    bottles,

    // Состояние ночных мини-ивентов
    nextEventAt: mode.eventInterval,
    raidTriggered: false,
    eventIdx: 0,
    ev: {
      active: false,
      type: null, // 'cap' | 'dad' | 'bag' | 'raid' | 'fridge_beep' | 'toilet'
      progress: 0,
      needed: 4,
      pressure: 0,
      timeLeft: 0,
      stepTick: 0,
      holdingSleep: false,
      holdingToilet: false,
      toiletPhase: 1, // 1: тихие шаги, 2: смыв / позор
      fridgePos: 0,
      fridgeDir: 1,
      fridgeBeepTick: 0,
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

export function getCurrentAct(progressSec, modeObj) {
  const ratio = Math.min(1, Math.max(0, progressSec / modeObj.durationSec));
  if (ratio < 0.25) return ACTS[0];
  if (ratio < 0.50) return ACTS[1];
  if (ratio < 0.75) return ACTS[2];
  return ACTS[3];
}

export const state = createInitialState('classic');
