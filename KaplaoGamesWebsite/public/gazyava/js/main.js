/* ==========================================================================
   ТОЧКА ВХОДА: СВЯЗЫВАНИЕ МОДУЛЕЙ, ИГРОВОЙ ЦИКЛ, СЕНСОР И КЛАВИАТУРА НА ПК
   ========================================================================== */
import {
  MODES,
  DRINKS,
  HRUMKA_FLAVORS,
  ENDINGS,
  TICKER_QUOTES,
  ASCENSION_QUOTES,
  FLAT_ROASTS,
  ACTS
} from './config.js';
import { records, saveRecords } from './storage.js';
import { sound } from './audio.js';
import {
  initCanvas,
  resizeCanvas,
  spawnParticles,
  drawCanvas,
  initSvgBubbles,
  svgBubbleNodes
} from './particles.js';
import { state, createInitialState, formatGameTime, getCurrentAct } from './state.js';
import {
  initEvents,
  triggerNextNightEvent,
  openNightEvent,
  handleCapTap,
  handleBagTap,
  handleFridgeSlam,
  closeEventModal,
  updateActiveEvent
} from './events.js';
import {
  initNotifications,
  pushPhoneNotif,
  updateNotifications
} from './notifications.js';
import {
  initShop,
  openShopModal,
  closeShopModal,
  canAffordAnyItem
} from './shop.js';
import {
  dom,
  initDom,
  showToast,
  showActBanner,
  refreshRecordsUI,
  openAlbumModal,
  closeAlbumModal,
  applyBottleVisuals,
  updateHUD,
  copyScorecard
} from './ui.js';

let lastTs = 0;
let bladderUrgentTimer = 0;
const DRINK_KEYS = ['cola', 'mineral', 'duchess', 'energy', 'brine', 'flat'];

function addNoise(amount, cause = null) {
  // Подушка-глушитель режет любой входящий шум вдвое
  const eff = state.silencerTimer > 0 ? amount * 0.5 : amount;
  state.noise = Math.min(100, Math.max(0, state.noise + eff));

  if (state.noise >= 100) {
    if (cause === 'burp_apocalypse') {
      endGame('burp_apocalypse');
    } else {
      endGame('woke_building');
    }
  }
}

function startGame() {
  state.mode = MODES[state.selectedMode] || MODES.classic;
  state.phase = 'playing';
  state.currentAct = 1;
  state.nightProgress = 0;
  state.realElapsed = 0;
  state.score = 0;

  state.thirst = state.mode.id === 'hardcore' ? 42 : 34;
  state.fizz = 0;
  state.sugarDebt = state.mode.id === 'hardcore' ? 18 : 0;
  state.noise = 0;

  // Механики 3.0
  state.burp = 0;
  state.isVentingBurp = false;
  state.bladder = 0;
  state.unflushedCount = 0;
  state.caps = 3;
  state.totalCapsEarned = 3;
  state.shopOpen = false;

  state.mintTimer = 0;
  state.dragonBreathCharges = 0;
  state.dragonBreathSips = 0;
  state.silencerTimer = 0;

  state.goldenSips = 0;
  state.combos = 0;
  state.hrumkasEaten = 0;
  state.energySips = 0;
  state.energySipAccum = 0;
  state.brineSipsLeft = 3;
  state.flavorIdx = 0;

  state.activeDrink = 'cola';
  state.lastDrunkId = 'cola';
  state.isDrinking = false;
  state.activePointerId = null;
  state.freezerActive = false;

  state.ascensionTimer = 0;
  state.stunTimer = 0;
  state.vhsTimer = 0;
  state.energyTurboTimer = 0;
  state.heartbeatTick = 0;
  state.comboWindowTimer = 0;
  state.flatTimer = 0;
  state.flatRoastCd = 0;
  state.gulpTick = 0;
  state.tickerTimer = 4.5;
  state.actBannerTimer = 0;
  state.lastNotifTimer = 0;
  state.nextNotifAt = 24;

  state.nextEventAt = state.mode.eventInterval;
  state.raidTriggered = false;
  state.eventIdx = 0;
  state.ev.active = false;

  Object.keys(DRINKS).forEach((k) => {
    state.bottles[k] = {
      temp: DRINKS[k].initTemp,
      carb: DRINKS[k].initCarb !== undefined ? DRINKS[k].initCarb : (k === 'flat' ? 12 : 100),
      shakeFoamTimer: 0,
      frozenTaps: 0,
      liquidLevel: 0.88
    };
  });

  dom.app.className = state.mode.id === 'hardcore' ? 'mode-hardcore' : '';
  dom.nightBarFill.className = 'night-bar-fill' + (state.mode.id === 'hardcore' ? ' hc' : '');
  dom.clockMeta.innerHTML = state.mode.id === 'hardcore'
    ? '<span class="hc-tag">АКТ I</span><br>ДО 08:00'
    : 'АКТ I<br>ДО 07:00';
  dom.hrumkaFlavor.textContent = HRUMKA_FLAVORS[0];

  dom.toastLayer.innerHTML = '';
  dom.eventModal.classList.remove('open');
  dom.shopModal.classList.remove('open');
  dom.startScreen.classList.remove('visible');
  dom.endScreen.classList.remove('visible');
  dom.fxAscension.classList.remove('active');
  dom.fxVhs.classList.remove('active');
  dom.fxHeartbeat.classList.remove('active');
  dom.fxStun.classList.remove('active');
  dom.fxBlackout.classList.remove('active');
  dom.fxDawn.classList.remove('active');
  dom.fxDesert.style.opacity = '0';

  sound.stopFreezerHum();
  sound.stopVentSound();
  applyBottleVisuals();
  updateHUD(0, 0.016);

  showToast(
    state.mode.id === 'hardcore'
      ? '🔥 1 ЯНВАРЯ! ВАСЬ, ПОХМЕЛЬНЫЙ СУШНЯК!'
      : '03:17. ВАСЬ, ХОЛОДИЛЬНИК ЗОВЁТ ❄️',
    state.mode.id === 'hardcore' ? 'danger' : 'info'
  );
}

function startDrinking(pointerId) {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  const b = state.bottles[state.activeDrink];

  // Если бутылка в глыбе льда — тап раскалывает лёд!
  if (b.frozenTaps > 0) {
    b.frozenTaps--;
    sound.playIceShatter();
    const rect = dom.bottleWrap.getBoundingClientRect();
    spawnParticles('ice', rect.left + rect.width / 2, rect.top + rect.height / 2, 12);
    dom.bottleWrap.style.transform = 'scale(0.9) rotate(-5deg)';
    setTimeout(() => { dom.bottleWrap.style.transform = ''; }, 90);
    if (b.frozenTaps === 0) {
      showToast('🧊 ЛЁД РАЗБИТ! МОЖНО ПИТЬ, ВАСЬ!', 'info');
    }
    return;
  }

  // Мгновенный глоток бабушкиного рассола
  if (state.activeDrink === 'brine') {
    if (state.brineSipsLeft <= 0) {
      showToast('🥒 РАССОЛ ЗАКОНЧИЛСЯ! ПЕРЕКЛЮЧАЙСЯ!', 'danger');
      return;
    }
    state.brineSipsLeft--;
    state.sugarDebt = 0;
    state.thirst = Math.max(0, state.thirst - 22);
    state.bladder = Math.min(100, state.bladder + 13);
    sound.playGulp();
    sound.playAscension(false);
    const rect = dom.bottleWrap.getBoundingClientRect();
    spawnParticles('ice', rect.left + rect.width / 2, rect.top + rect.height / 2, 16);
    showToast('🥒 РАССОЛ ПОШЁЛ! САХАР СБРОШЕН В НОЛЬ!', 'vhs');
    return;
  }

  if (state.stunTimer > 0 || state.freezerActive) return;

  state.isDrinking = true;
  state.activePointerId = pointerId;
  state.lastDrunkId = state.activeDrink;
  sound.startFizz();
}

function stopDrinking(successAttempt = true) {
  if (!state.isDrinking) return;
  state.isDrinking = false;
  state.activePointerId = null;
  sound.stopFizz();

  if (state.phase !== 'playing') return;

  const d = DRINKS[state.activeDrink];
  const b = state.bottles[state.activeDrink];
  const rect = dom.bottleWrap.getBoundingClientRect();
  const cx = rect.left + rect.width / 2;
  const cy = rect.top + rect.height / 2;

  // Проверка попадания в Золотую зону (70–90%)
  const inGoldZone = state.fizz >= 70 && state.fizz <= 90;

  if (successAttempt && inGoldZone && d.canAscend && b.temp <= 18.5) {
    if (b.carb < 25) {
      // Выдохлась — вознесение заблокировано!
      sound.playSadTrombone();
      showToast('🫠 ВЫДОХЛАСЬ, ВАСЬ! ВСТРЯХНИ ИЛИ КУПИ СВЕЖУЮ!', 'roast');
    } else if (state.dragonBreathCharges > 0) {
      // Дыхание Дракона!
      state.dragonBreathCharges--;
      state.dragonBreathSips++;
      state.score += 600;
      state.caps += 5;
      state.totalCapsEarned += 5;
      state.ascensionTimer = 4.5;
      sound.playAscension(true);
      spawnParticles('dragon', cx, cy, 34);
      showToast('🐉 ДЫХАНИЕ ЛЕДЯНОГО ДРАКОНА! (+600, +5 🪙)', 'gold');

      if (state.dragonBreathSips >= 3) {
        endGame('dragon_breath');
        return;
      }
    } else if (state.comboWindowTimer > 0) {
      // Имба-комбо: Хрумка + Золотой глоток!
      state.combos++;
      state.score += 450;
      state.caps += 4;
      state.totalCapsEarned += 4;
      state.ascensionTimer = 4.0;
      state.comboWindowTimer = 0;
      sound.playAscension(true);
      spawnParticles('gold', cx, cy, 26);
      showToast('🥨⚡ ИМБА-КОМБО! ХРУМКА + ГАЗЯВА! (+450, +4 🪙)', 'combo');
    } else {
      // Обычное золотое вознесение
      state.goldenSips++;
      state.score += 250;
      state.caps += 2;
      state.totalCapsEarned += 2;
      state.ascensionTimer = 3.2;
      sound.playAscension(false);
      spawnParticles('gold', cx, cy, 20);
      const q = ASCENSION_QUOTES[Math.floor(Math.random() * ASCENSION_QUOTES.length)];
      showToast(`${q} (+250, +2 🪙)`, 'gold');
    }
  } else if (successAttempt && state.activeDrink === 'flat' && state.flatRoastCd <= 0) {
    state.flatRoastCd = 3.5;
    sound.playSadTrombone();
    const r = FLAT_ROASTS[Math.floor(Math.random() * FLAT_ROASTS.length)];
    showToast(r, 'roast');
  }
}

function eatHrumka() {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  state.hrumkasEaten++;
  state.score += 65;
  state.sugarDebt = Math.max(0, state.sugarDebt - 22);
  state.thirst = Math.min(100, state.thirst + 4.5);
  state.burp = Math.min(100, state.burp + 11);
  addNoise(12);

  state.comboWindowTimer = 4.5;
  sound.playCrunch();

  const rect = dom.hrumkaBtn.getBoundingClientRect();
  spawnParticles('crumb', rect.left + rect.width / 2, rect.top + rect.height / 2, 14);

  dom.app.classList.add('state-crunch-bump');
  setTimeout(() => dom.app.classList.remove('state-crunch-bump'), 180);

  state.flavorIdx = (state.flavorIdx + 1) % HRUMKA_FLAVORS.length;
  dom.hrumkaFlavor.textContent = HRUMKA_FLAVORS[state.flavorIdx];

  showToast('🥨 ХРУМКНУЛ! САХАР -22%, ШУМ +12, ГАЗ +11%', 'crunch');
}

function shakeActiveBottle() {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  const d = DRINKS[state.activeDrink];
  const b = state.bottles[state.activeDrink];
  if (!d.canAscend || b.carb >= 70 || b.frozenTaps > 0 || state.freezerActive) return;

  b.carb = Math.min(100, b.carb + 28);
  b.shakeFoamTimer = 4.5;
  addNoise(9);
  sound.playShake();

  const rect = dom.bottleWrap.getBoundingClientRect();
  spawnParticles('foam', rect.left + rect.width / 2, rect.top + rect.height / 2, 16);

  dom.bottleWrap.style.transform = 'scale(1.06) rotate(8deg)';
  setTimeout(() => { dom.bottleWrap.style.transform = ''; }, 140);

  showToast('🫧 ВСТРЯХНУЛ! ГАЗЫ ВЕРНУЛИСЬ, ОСТОРОЖНО — ПЕНА!', 'info');
}

function startVentingBurp() {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  stopDrinking(false);
  state.isVentingBurp = true;
  sound.startVentSound();
}

function stopVentingBurp() {
  if (!state.isVentingBurp) return;
  state.isVentingBurp = false;
  sound.stopVentSound();
}

function triggerToiletEvent() {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen || state.bladder < 25) return;

  stopDrinking(false);
  state.isVentingBurp = false;
  sound.stopVentSound();
  if (state.freezerActive) {
    state.freezerActive = false;
    sound.stopFreezerHum();
  }

  openNightEvent('toilet');
}

function toggleFreezer() {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  const b = state.bottles[state.activeDrink];
  if (b.frozenTaps > 0) {
    showToast('БУТЫЛКА ВО ЛЬДУ — СНАЧАЛА РАЗБЕЙ!', 'danger');
    return;
  }

  state.freezerActive = !state.freezerActive;
  if (state.freezerActive) {
    sound.startFreezerHum();
    showToast('❄️ МОРОЗИЛКА ВКЛЮЧЕНА! СЛЕДИ, ЧТОБ НЕ ЗАМЁРЗЛА!', 'info');
  } else {
    sound.stopFreezerHum();
    showToast('МОРОЗИЛКА ВЫКЛЮЧЕНА', 'roast');
  }
}

function selectDrink(drinkKey) {
  sound.unlock();
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;
  if (state.activeDrink === drinkKey) return;

  stopDrinking(false);
  if (state.freezerActive) {
    state.freezerActive = false;
    sound.stopFreezerHum();
  }

  state.activeDrink = drinkKey;
  state.fizz = 0;
  sound.playClick();
  applyBottleVisuals();
}

function endGame(endingId, customNoEndingWin = false) {
  if (state.phase === 'ended') return;
  state.phase = 'ended';

  stopDrinking(false);
  state.isVentingBurp = false;
  sound.stopVentSound();
  sound.stopFreezerHum();

  dom.fxDesert.style.opacity = '0';
  dom.fxBlackout.classList.remove('active');
  dom.fxDawn.classList.remove('active');

  const finalScore = Math.round(state.score);
  if (finalScore > records.bestScore) records.bestScore = finalScore;
  if (state.realElapsed > records.bestTimeSec) records.bestTimeSec = Math.round(state.realElapsed);
  records.totalGoldenSips += state.goldenSips;
  records.totalHrumkas += state.hrumkasEaten;
  records.totalCaps = (records.totalCaps || 0) + state.caps;

  let emoji = '💀';
  let badge = 'КОНЕЦ';
  let badgeClass = 'lose';
  let title = 'НОЧЬ ЗАКОНЧИЛАСЬ';
  let desc = '';
  let isWin = false;

  if (customNoEndingWin) {
    isWin = true;
    emoji = '🌅';
    badge = 'ПОБЕДА (БАЗОВАЯ)';
    badgeClass = 'win';
    title = 'ДОЖИЛ ДО 07:00!';
    desc = `Вась, ты пережил ночь! Но для истинного «Вознесения» нужно 8+ золотых глотков (сейчас ${state.goldenSips}/8), а для «Короля Хрумки» — 5+ комбо (${state.combos}/5).`;
  } else {
    const e = ENDINGS[endingId] || ENDINGS.kuraga;
    emoji = e.emoji;
    badge = e.badge;
    badgeClass = e.badgeClass;
    title = e.title;
    desc = e.desc;
    isWin = badgeClass === 'win' || badgeClass === 'secret' || badgeClass === 'hardcore';

    if (!records.endings.includes(e.id)) {
      records.endings.push(e.id);
    }
    if (e.id === 'hardcore_immortal') {
      records.hardcoreCleared = true;
    }
  }

  saveRecords();
  refreshRecordsUI();

  if (isWin) sound.playVictoryFanfare();
  else sound.playSadTrombone();

  const clockStr = isWin && endingId !== 'energy_astral'
    ? state.mode.shortTag
    : formatGameTime(state.nightProgress, state.mode);

  const act = getCurrentAct(state.nightProgress, state.mode);

  dom.endEmoji.textContent = emoji;
  dom.endBadge.textContent = badge;
  dom.endBadge.className = `end-badge ${badgeClass}`;
  dom.endTitle.textContent = title;
  dom.endDesc.textContent = desc;
  dom.endClock.textContent = clockStr;
  dom.endScore.textContent = String(finalScore);
  dom.endSips.textContent = String(state.goldenSips);
  dom.endCombos.textContent = String(state.combos);

  state.lastResult = {
    modeName: state.mode.name,
    endingTitle: `${emoji} ${title}`,
    actName: act.name,
    clockStr,
    score: finalScore,
    capsEarned: state.totalCapsEarned,
    goldenSips: state.goldenSips,
    combos: state.combos,
    hrumkas: state.hrumkasEaten,
    unlocked: records.endings.length,
    total: Object.keys(ENDINGS).length
  };

  dom.endScreen.classList.add('visible');
}

function updateGame(dt) {
  if (state.phase !== 'playing') return;

  // Если открыт магазин — игра на паузе
  if (state.shopOpen) return;

  // Если открыто модальное ночное событие — тикает только оно
  if (state.ev.active) {
    updateActiveEvent(dt);
    return;
  }

  state.realElapsed += dt;

  // Турбо-часы от Энергоса (2.6x скорость ночи на 3.5 сек)
  const clockSpeed = state.energyTurboTimer > 0 ? 2.6 : 1.0;
  state.nightProgress += dt * clockSpeed;
  state.score += dt * 12 * (state.mode.id === 'hardcore' ? 1.4 : 1.0);

  // Смена Актов ночи
  const act = getCurrentAct(state.nightProgress, state.mode);
  if (act.index > state.currentAct) {
    state.currentAct = act.index;
    sound.playActChime(act.index);
    showActBanner(act.banner);
  }

  // Проверка победы по достижению рассвета!
  if (state.nightProgress >= state.mode.durationSec) {
    // Высокий приоритет: Несмытый унитаз 3+ раза
    if (state.unflushedCount >= 3) {
      endGame('shame_unflushed');
    } else if (state.mode.id === 'hardcore') {
      endGame('hardcore_immortal');
    } else if (state.combos >= 5) {
      endGame('hrumka_king');
    } else if (state.goldenSips >= 8) {
      endGame('ascension');
    } else {
      endGame(null, true);
    }
    return;
  }

  // Ночной рейд к холодильнику
  if (!state.raidTriggered && state.nightProgress >= state.mode.raidAt && state.stunTimer <= 0) {
    state.raidTriggered = true;
    openNightEvent('raid');
    return;
  }

  // Периодические ночные мини-ивенты
  if (state.realElapsed >= state.nextEventAt && state.nightProgress < state.mode.durationSec - 10 && state.stunTimer <= 0) {
    const actFreq = act.eventFreqMult || 1.0;
    state.nextEventAt = state.realElapsed + state.mode.eventInterval * actFreq;
    triggerNextNightEvent();
    return;
  }

  // Фейковые push-уведомления
  updateNotifications(dt);

  // Смена постироничной строки статуса
  state.tickerTimer -= dt;
  if (state.tickerTimer <= 0) {
    state.tickerTimer = 5.5;
    state.tickerIdx = (state.tickerIdx + 1) % TICKER_QUOTES.length;
    dom.memeTicker.textContent = TICKER_QUOTES[state.tickerIdx];
  }

  // Таймеры баффов / дебаффов
  if (state.ascensionTimer > 0) state.ascensionTimer = Math.max(0, state.ascensionTimer - dt);
  if (state.mintTimer > 0) state.mintTimer = Math.max(0, state.mintTimer - dt);
  if (state.silencerTimer > 0) state.silencerTimer = Math.max(0, state.silencerTimer - dt);
  if (state.vhsTimer > 0) state.vhsTimer = Math.max(0, state.vhsTimer - dt);
  if (state.comboWindowTimer > 0) state.comboWindowTimer = Math.max(0, state.comboWindowTimer - dt);
  if (state.flatTimer > 0) state.flatTimer = Math.max(0, state.flatTimer - dt);
  if (state.flatRoastCd > 0) state.flatRoastCd = Math.max(0, state.flatRoastCd - dt);

  if (state.energyTurboTimer > 0) {
    state.energyTurboTimer = Math.max(0, state.energyTurboTimer - dt);
    state.heartbeatTick += dt;
    if (state.heartbeatTick >= 0.56) {
      state.heartbeatTick = 0;
      sound.playHeartbeat();
    }
  }

  if (state.stunTimer > 0) {
    state.stunTimer = Math.max(0, state.stunTimer - dt);
    state.fizz = (state.stunTimer / 2.5) * 100;
  }

  // Постепенное затухание Шума в тишине
  let noiseDecayRate = state.mode.noiseDecay * (act.noiseDecayMult || 1.0);
  if (state.vhsTimer > 0) noiseDecayRate *= 2.1;
  if (state.silencerTimer > 0) noiseDecayRate *= 2.0;
  state.noise = Math.max(0, state.noise - noiseDecayRate * dt);

  // Температура и газация активной бутылки
  const d = DRINKS[state.activeDrink];
  const b = state.bottles[state.activeDrink];

  if (b.shakeFoamTimer > 0) {
    b.shakeFoamTimer = Math.max(0, b.shakeFoamTimer - dt);
  }

  if (state.freezerActive && b.frozenTaps === 0) {
    b.temp -= 5.8 * dt;
    if (b.temp <= 0) {
      b.temp = 0;
      b.frozenTaps = 3;
      state.freezerActive = false;
      sound.stopFreezerHum();
      sound.playIceShatter();
      showToast('🧊 ПЕРЕДЕРЖАЛ, ВАСЬ! БУТЫЛКА ПРЕВРАТИЛАСЬ В ЛЁД!', 'info');
    }
  } else if (b.frozenTaps === 0) {
    b.temp = Math.min(24, b.temp + d.warmRate * state.mode.heatMult * dt);
  }

  // Остальные бутылки греются медленно
  Object.keys(state.bottles).forEach((k) => {
    if (k !== state.activeDrink && state.bottles[k].frozenTaps === 0) {
      state.bottles[k].temp = Math.min(24, state.bottles[k].temp + 0.22 * state.mode.heatMult * dt);
    }
  });

  // Выдыхание газировки («Выдыхание»)
  if (d.canAscend) {
    const warmFactor = b.temp > 14 ? 1.65 : 1.0;
    b.carb = Math.max(0, b.carb - 0.85 * act.carbDrainMult * warmFactor * dt);
  }
  Object.keys(state.bottles).forEach((k) => {
    if (k !== state.activeDrink && DRINKS[k].canAscend) {
      state.bottles[k].carb = Math.max(0, state.bottles[k].carb - 0.18 * act.carbDrainMult * dt);
    }
  });
  state.bottles.flat.carb = Math.min(15, state.bottles.flat.carb);

  // Механика «В подушку» (Отрыжка)
  if (state.isVentingBurp) {
    state.burp = Math.max(0, state.burp - 38 * dt);
    addNoise(3.2 * dt);
  } else if (!state.isDrinking) {
    state.burp = Math.max(0, state.burp - 1.1 * dt);
  }

  // 100% Взрыв отрыжки (MEGA BURP)
  if (state.burp >= 100) {
    sound.playMegaBurp();
    state.burp = 18;
    dom.app.classList.add('state-stun');
    setTimeout(() => dom.app.classList.remove('state-stun'), 420);
    showToast('💥 БЭЭЭЭЭЭ!! ВАСЬ НЕ УСПЕЛ В ПОДУШКУ! (+45 ШУМА)', 'danger');
    addNoise(45, 'burp_apocalypse');
  }

  // Механика клапана (стресс при переполнении)
  if (state.bladder >= 98) {
    state.thirst += 3.5 * dt;
    bladderUrgentTimer += dt;
    if (bladderUrgentTimer >= 4.0) {
      bladderUrgentTimer = 0;
      showToast('🚽 ВАСЬ, КЛАПАН ЩАС СОРВЁТ! БЕГИ В ТУАЛЕТ!', 'danger');
    }
  } else {
    bladderUrgentTimer = 0;
  }

  // Рост Сушняка
  const sugarMult = 1.0 + (state.sugarDebt / 100) * 1.65;
  let thirstClimb = (act.thirstBase + (state.nightProgress / state.mode.durationSec) * 1.5) * state.mode.thirstMult * sugarMult;
  if (state.ascensionTimer > 0 || state.mintTimer > 0) {
    thirstClimb = 0;
  }
  state.thirst += thirstClimb * dt;

  // Процесс питья
  if (state.isDrinking && state.stunTimer <= 0 && !state.freezerActive) {
    let tempEff = 1.0;
    if (b.temp > 18.5) tempEff = 0.36;
    else if (b.temp > 11.5) tempEff = 0.74;

    // Эффект выдыхания на сбивание сушняка
    let carbEff = 1.0;
    if (d.canAscend) {
      if (b.carb < 25) carbEff = 0.45;
      else if (b.carb < 50) carbEff = 0.78;
    }

    let breathThirstMult = state.dragonBreathCharges > 0 && d.canAscend ? 1.5 : 1.0;

    state.thirst = Math.max(0, state.thirst - d.thirstDrop * tempEff * carbEff * breathThirstMult * dt);

    // Накопление отрыжки и клапана
    if (d.canAscend) {
      state.burp = Math.min(100, state.burp + (state.activeDrink === 'mineral' ? 13 : 9.5) * dt);
    }
    state.bladder = Math.min(100, state.bladder + 4.6 * dt);

    // Рост газов в нос
    let fizzSpeed = d.fizzRate * (b.temp <= 11 ? 1.0 : 0.78);
    if (d.canAscend && b.carb < 25) fizzSpeed *= 0.4;
    if (b.shakeFoamTimer > 0) fizzSpeed *= 1.45;
    if (state.dragonBreathCharges > 0 && d.canAscend) fizzSpeed *= 2.1;
    if (state.activeDrink === 'energy') fizzSpeed *= (0.85 + Math.random() * 0.45);
    if (state.bladder >= 80) fizzSpeed *= (0.72 + Math.random() * 0.62);

    state.fizz += fizzSpeed * dt;

    // Рост Сахарного долга
    state.sugarDebt = Math.min(100, state.sugarDebt + d.sugarAdd * dt);

    // Убывание уровня жидкости
    b.liquidLevel -= 0.17 * dt;
    if (b.liquidLevel < 0.2) b.liquidLevel = 0.9;

    state.gulpTick += dt;
    if (state.gulpTick >= 0.48) {
      state.gulpTick = 0;
      sound.playGulp();
    }

    if (state.activeDrink === 'duchess') {
      state.vhsTimer = 2.6;
      state.noise = Math.max(0, state.noise - 8 * dt);
      state.score += 38 * dt;
    } else if (state.activeDrink === 'flat') {
      state.flatTimer = 1.5;
    } else if (state.activeDrink === 'energy') {
      state.energyTurboTimer = 3.5;
      state.energySipAccum += dt;
      if (state.energySipAccum >= 0.52) {
        state.energySipAccum = -99;
        state.energySips++;
        showToast(`⚡ ЭНЕРГОС #${state.energySips}/5! ПУЛЬС 190 УД/МИН!`, 'danger');
        if (state.energySips >= 5) {
          endGame('energy_astral');
          return;
        }
      }
    }

    sound.updateFizz(state.fizz, state.activeDrink === 'flat');

    // Оглушение при 100% газов в нос
    if (state.fizz >= 100) {
      stopDrinking(false);
      state.stunTimer = 2.5;
      state.fizz = 100;
      addNoise(14);
      sound.playCoughStun();
    }
  } else if (state.stunTimer <= 0) {
    state.fizz = Math.max(0, state.fizz - 50 * dt);
  }

  // Поражение от сушняка
  if (state.thirst >= 100) {
    state.thirst = 100;
    if (state.lastDrunkId === 'flat' || state.activeDrink === 'flat') {
      endGame('betrayal');
    } else if (state.sugarDebt >= 85) {
      endGame('ouroboros');
    } else {
      endGame('kuraga');
    }
  }
}

function loop(ts) {
  if (!lastTs) lastTs = ts;
  const dt = Math.min(0.05, (ts - lastTs) / 1000);
  lastTs = ts;

  updateGame(dt);
  drawCanvas(dt);
  if (state.phase === 'playing') {
    updateHUD(ts / 1000, dt);
  }
  requestAnimationFrame(loop);
}

function bindEvents() {
  window.addEventListener('contextmenu', (e) => e.preventDefault());
  window.addEventListener('selectstart', (e) => e.preventDefault());
  window.addEventListener('resize', () => resizeCanvas());

  // 1. Блокировка жестов pinch-zoom в iOS Safari
  ['gesturestart', 'gesturechange', 'gestureend'].forEach((evtName) => {
    document.addEventListener(evtName, (e) => e.preventDefault(), { passive: false });
  });

  // 2. Блокировка мультитач-зума (2+ пальца)
  document.addEventListener('touchstart', (e) => {
    if (e.touches.length > 1) e.preventDefault();
  }, { passive: false });

  document.addEventListener('touchmove', (e) => {
    if (e.touches.length > 1) e.preventDefault();
  }, { passive: false });

  // 3. Блокировка double-tap zoom fallback без ломки click на кнопках/ссылках
  document.addEventListener('dblclick', (e) => e.preventDefault(), { passive: false });

  let lastTouchEnd = 0;
  document.addEventListener('touchend', (e) => {
    const now = Date.now();
    const isClickControl = e.target && e.target.closest(
      'a, .drink-card, .mode-opt, .ev-choice-btn, .btn-primary, .btn-secondary, .btn-share, .icon-btn, .tap-water-btn, .shop-buy-btn, #shopBtn, #toiletBtn, #phonePush'
    );
    if (!isClickControl && now - lastTouchEnd <= 350) {
      e.preventDefault();
    }
    lastTouchEnd = now;
  }, { passive: false });

  // Зажатие бутылки
  dom.bottleZone.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    try { dom.bottleZone.setPointerCapture(e.pointerId); } catch (err) {}
    startDrinking(e.pointerId);
  });
  const onRel = (e) => {
    if (state.activePointerId !== null && e.pointerId !== state.activePointerId) return;
    stopDrinking(true);
  };
  dom.bottleZone.addEventListener('pointerup', onRel);
  dom.bottleZone.addEventListener('pointercancel', () => stopDrinking(false));
  dom.bottleZone.addEventListener('pointerleave', (e) => {
    if (e.pointerType === 'mouse' && state.isDrinking) stopDrinking(true);
  });

  // Единый консолидированный blur/visibilitychange хэндлер
  const handleAppBlur = () => {
    stopDrinking(false);
    state.isVentingBurp = false;
    state.ev.holdingSleep = false;
    state.ev.holdingToilet = false;
    sound.stopFreezerHum();
    sound.stopVentSound();
  };
  window.addEventListener('blur', handleAppBlur);
  document.addEventListener('visibilitychange', () => {
    if (document.hidden) handleAppBlur();
  });

  // Хрумка и Морозилка
  dom.hrumkaBtn.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    eatHrumka();
  });
  dom.freezerBtn.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    toggleFreezer();
  });

  // Новые кнопки 3.0: «В подушку», «Туалет», «Встряхнуть»
  if (dom.burpBtn) {
    dom.burpBtn.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      try { dom.burpBtn.setPointerCapture(e.pointerId); } catch (err) {}
      startVentingBurp();
    });
    const onRelBurp = () => stopVentingBurp();
    dom.burpBtn.addEventListener('pointerup', onRelBurp);
    dom.burpBtn.addEventListener('pointercancel', onRelBurp);
  }

  if (dom.toiletBtn) {
    dom.toiletBtn.addEventListener('click', (e) => {
      e.preventDefault();
      triggerToiletEvent();
    });
  }

  if (dom.shakeBtn) {
    dom.shakeBtn.addEventListener('click', (e) => {
      e.preventDefault();
      shakeActiveBottle();
    });
  }

  // 6 напитков
  dom.drinkCards.forEach((btn) => {
    btn.addEventListener('click', () => selectDrink(btn.dataset.drink));
  });

  // Вода из-под крана
  dom.tapWaterBtn.addEventListener('click', () => {
    sound.unlock();
    if (state.phase === 'playing') endGame('tap_water');
  });

  // Звук
  dom.muteBtn.addEventListener('click', () => {
    sound.unlock();
    const m = sound.toggleMute();
    dom.muteBtn.textContent = m ? '🔇' : '🔊';
  });

  // Выбор режима
  const selectModeUI = (modeKey) => {
    sound.unlock();
    sound.playClick();
    state.selectedMode = modeKey;
    dom.modeClassicBtn.classList.toggle('selected', modeKey === 'classic');
    dom.modeHardcoreBtn.classList.toggle('selected', modeKey === 'hardcore');
    dom.startBtn.classList.toggle('hc-start', modeKey === 'hardcore');
    dom.startBtn.textContent = modeKey === 'hardcore'
      ? '🔥 ЗАПУСТИТЬ ПОХМЕЛЬЕ 1 ЯНВАРЯ'
      : 'ОТКРЫТЬ ХОЛОДИЛЬНИК (СТАРТ)';
  };
  dom.modeClassicBtn.addEventListener('click', () => selectModeUI('classic'));
  dom.modeHardcoreBtn.addEventListener('click', () => selectModeUI('hardcore'));

  // Меню, рестарт, копирование, альбом
  dom.startBtn.addEventListener('click', () => {
    sound.unlock();
    sound.playPssh(false);
    startGame();
  });
  dom.restartBtn.addEventListener('click', () => {
    sound.unlock();
    sound.playPssh(false);
    startGame();
  });
  dom.toMenuBtn.addEventListener('click', () => {
    sound.unlock();
    sound.playClick();
    dom.endScreen.classList.remove('visible');
    dom.startScreen.classList.add('visible');
    refreshRecordsUI();
  });
  dom.shareBtn.addEventListener('click', copyScorecard);

  dom.startAlbumBtn.addEventListener('click', openAlbumModal);
  dom.endAlbumBtn.addEventListener('click', openAlbumModal);
  dom.closeAlbumBtn.addEventListener('click', closeAlbumModal);
  dom.albumOkBtn.addEventListener('click', closeAlbumModal);
}

function setupKeyboardControls() {
  window.addEventListener('keydown', (e) => {
    if (e.repeat) return;

    if (e.code === 'Escape') {
      if (state.shopOpen) {
        closeShopModal();
        return;
      }
      closeAlbumModal();
      return;
    }

    if (state.phase !== 'playing') return;

    // В модальных ивентах ловим Space для закрытия холодильника
    if (state.ev.active) {
      if (e.code === 'Space' && state.ev.type === 'fridge_beep') {
        e.preventDefault();
        handleFridgeSlam();
      }
      return;
    }

    if (e.code === 'Space') {
      e.preventDefault();
      startDrinking('keyboard');
    } else if (e.code === 'KeyH' || e.code === 'KeyE') {
      e.preventDefault();
      eatHrumka();
    } else if (e.code === 'KeyF') {
      e.preventDefault();
      const b = state.bottles[state.activeDrink];
      if (b && b.frozenTaps > 0) {
        startDrinking('keyboard');
      } else {
        toggleFreezer();
      }
    } else if (e.code === 'KeyB') {
      e.preventDefault();
      startVentingBurp();
    } else if (e.code === 'KeyT') {
      e.preventDefault();
      triggerToiletEvent();
    } else if (e.code === 'KeyX') {
      e.preventDefault();
      shakeActiveBottle();
    } else if (e.code === 'KeyS') {
      e.preventDefault();
      if (state.shopOpen) closeShopModal();
      else openShopModal();
    } else if (['Digit1', 'Digit2', 'Digit3', 'Digit4', 'Digit5', 'Digit6'].includes(e.code)) {
      e.preventDefault();
      const idx = parseInt(e.code.replace('Digit', ''), 10) - 1;
      if (DRINK_KEYS[idx]) {
        selectDrink(DRINK_KEYS[idx]);
      }
    }
  });

  window.addEventListener('keyup', (e) => {
    if (e.code === 'Space') {
      e.preventDefault();
      if (state.isDrinking) {
        stopDrinking(true);
      }
    } else if (e.code === 'KeyB') {
      e.preventDefault();
      stopVentingBurp();
    }
  });
}

function init() {
  initDom();
  initCanvas(dom.fxCanvas);
  initSvgBubbles(dom.svgBubbles);

  initNotifications({
    dom,
    showToast
  });

  initEvents({
    dom,
    showToast,
    addNoise,
    stopDrinking,
    endGame,
    pushPhoneNotif
  });

  initShop({
    dom,
    showToast,
    addNoise,
    stopDrinking,
    applyBottleVisuals
  });

  bindEvents();
  setupKeyboardControls();
  refreshRecordsUI();

  // Синхронизация иконки звука при загрузке
  dom.muteBtn.textContent = sound.muted ? '🔇' : '🔊';

  requestAnimationFrame(loop);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
