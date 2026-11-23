/* ==========================================================================
   ОБНОВЛЕНИЕ DOM, РЕКОРДЫ, ТОСТЫ, АЛЬБОМ И КАРТОЧКА РЕЗУЛЬТАТА (TIER 3)
   ========================================================================== */
import { DRINKS, ENDINGS, HRUMKA_FLAVORS, ACTS } from './config.js';
import { records } from './storage.js';
import { sound } from './audio.js';
import { svgBubbleNodes } from './particles.js';
import { state, formatGameTime, getCurrentAct } from './state.js';

export const dom = {};

export function initDom() {
  Object.assign(dom, {
    app: document.getElementById('app'),
    fxCanvas: document.getElementById('fxCanvas'),
    fxDesert: document.getElementById('fxDesert'),
    fxAscension: document.getElementById('fxAscension'),
    fxVhs: document.getElementById('fxVhs'),
    fxHeartbeat: document.getElementById('fxHeartbeat'),
    fxStun: document.getElementById('fxStun'),
    stunFill: document.getElementById('stunFill'),
    fxBlackout: document.getElementById('fxBlackout'),
    fxDawn: document.getElementById('fxDawn'),
    toastLayer: document.getElementById('toastLayer'),
    copyBanner: document.getElementById('copyBanner'),
    actBanner: document.getElementById('actBanner'),

    // Phone push notification
    phonePush: document.getElementById('phonePush'),
    pushIcon: document.getElementById('pushIcon'),
    pushSender: document.getElementById('pushSender'),
    pushText: document.getElementById('pushText'),

    clockVal: document.getElementById('clockVal'),
    clockMeta: document.getElementById('clockMeta'),
    scorePill: document.getElementById('scorePill'),
    sipsPill: document.getElementById('sipsPill'),
    comboPill: document.getElementById('comboPill'),
    capsPill: document.getElementById('capsPill'),
    shopBtn: document.getElementById('shopBtn'),
    shopBadge: document.getElementById('shopBadge'),
    muteBtn: document.getElementById('muteBtn'),
    nightBarFill: document.getElementById('nightBarFill'),
    memeTicker: document.getElementById('memeTicker'),

    // Шкалы
    thirstVal: document.getElementById('thirstVal'),
    thirstFill: document.getElementById('thirstFill'),
    thirstFreezeTag: document.getElementById('thirstFreezeTag'),
    fizzVal: document.getElementById('fizzVal'),
    fizzFill: document.getElementById('fizzFill'),
    goldTag: document.getElementById('goldTag'),
    sugarVal: document.getElementById('sugarVal'),
    sugarFill: document.getElementById('sugarFill'),
    noiseVal: document.getElementById('noiseVal'),
    noiseFill: document.getElementById('noiseFill'),
    carbVal: document.getElementById('carbVal'),
    carbFill: document.getElementById('carbFill'),

    // Арена и бутылка
    bottleZone: document.getElementById('bottleZone'),
    bottleWrap: document.getElementById('bottleWrap'),
    holdHint: document.getElementById('holdHint'),
    tempPill: document.getElementById('tempPill'),
    shakeBtn: document.getElementById('shakeBtn'),
    coldMeterFill: document.getElementById('coldMeterFill'),
    fridgeAura: document.getElementById('fridgeAura'),
    coldAura: document.getElementById('coldAura'),
    frostCoat: document.getElementById('frostCoat'),
    condensation: document.getElementById('condensation'),
    heatWaves: document.getElementById('heatWaves'),
    iceBlock: document.getElementById('iceBlock'),
    iceLabel: document.getElementById('iceLabel'),
    iceCrack1: document.getElementById('iceCrack1'),
    iceCrack2: document.getElementById('iceCrack2'),

    liqTop: document.getElementById('liqTop'),
    liqBot: document.getElementById('liqBot'),
    liquidWave: document.getElementById('liquidWave'),
    liquidFoam: document.getElementById('liquidFoam'),
    labelRect: document.getElementById('labelRect'),
    capRect: document.getElementById('capRect'),
    labelTitle: document.getElementById('labelTitle'),
    labelSub: document.getElementById('labelSub'),
    svgBubbles: document.getElementById('svgBubbles'),
    activeDrinkName: document.getElementById('activeDrinkName'),

    // Боковые кнопки
    hrumkaBtn: document.getElementById('hrumkaBtn'),
    hrumkaFlavor: document.getElementById('hrumkaFlavor'),
    comboMeterFill: document.getElementById('comboMeterFill'),
    burpBtn: document.getElementById('burpBtn'),
    burpVal: document.getElementById('burpVal'),
    burpMeterFill: document.getElementById('burpMeterFill'),
    freezerBtn: document.getElementById('freezerBtn'),
    freezerSub: document.getElementById('freezerSub'),
    toiletBtn: document.getElementById('toiletBtn'),
    toiletVal: document.getElementById('toiletVal'),
    toiletMeterFill: document.getElementById('toiletMeterFill'),
    buffsRow: document.getElementById('buffsRow'),

    drinkDock: document.getElementById('drinkDock'),
    drinkCards: Array.from(document.querySelectorAll('.drink-card')),

    // Ивенты
    eventModal: document.getElementById('eventModal'),
    eventCard: document.getElementById('eventCard'),
    evTag: document.getElementById('evTag'),
    evTitle: document.getElementById('evTitle'),
    evDesc: document.getElementById('evDesc'),
    evTimer: document.getElementById('evTimer'),
    evBarWrap: document.getElementById('evBarWrap'),
    evBarLabel: document.getElementById('evBarLabel'),
    evBarPct: document.getElementById('evBarPct'),
    evBarFill: document.getElementById('evBarFill'),
    evBody: document.getElementById('evBody'),

    // Магазин
    shopModal: document.getElementById('shopModal'),
    shopCapsVal: document.getElementById('shopCapsVal'),
    shopList: document.getElementById('shopList'),
    closeShopBtn: document.getElementById('closeShopBtn'),
    shopOkBtn: document.getElementById('shopOkBtn'),

    // Стартовый экран
    startScreen: document.getElementById('startScreen'),
    modeClassicBtn: document.getElementById('modeClassicBtn'),
    modeHardcoreBtn: document.getElementById('modeHardcoreBtn'),
    startBtn: document.getElementById('startBtn'),
    startAlbumBtn: document.getElementById('startAlbumBtn'),
    hcTrophyBanner: document.getElementById('hcTrophyBanner'),
    recScore: document.getElementById('recScore'),
    recTime: document.getElementById('recTime'),
    recHrumkas: document.getElementById('recHrumkas'),
    recEndings: document.getElementById('recEndings'),

    // Экран победы/поражения
    endScreen: document.getElementById('endScreen'),
    endEmoji: document.getElementById('endEmoji'),
    endBadge: document.getElementById('endBadge'),
    endTitle: document.getElementById('endTitle'),
    endDesc: document.getElementById('endDesc'),
    endClock: document.getElementById('endClock'),
    endScore: document.getElementById('endScore'),
    endSips: document.getElementById('endSips'),
    endCombos: document.getElementById('endCombos'),
    restartBtn: document.getElementById('restartBtn'),
    shareBtn: document.getElementById('shareBtn'),
    endAlbumBtn: document.getElementById('endAlbumBtn'),
    toMenuBtn: document.getElementById('toMenuBtn'),

    // Альбом
    albumModal: document.getElementById('albumModal'),
    albumTitle: document.getElementById('albumTitle'),
    albumList: document.getElementById('albumList'),
    closeAlbumBtn: document.getElementById('closeAlbumBtn'),
    albumOkBtn: document.getElementById('albumOkBtn'),

    tapWaterBtn: document.getElementById('tapWaterBtn')
  });
  return dom;
}

export function showToast(text, style = 'info') {
  if (!dom.toastLayer) return;
  const el = document.createElement('div');
  el.className = `toast toast-${style}`;
  el.textContent = text;
  dom.toastLayer.appendChild(el);
  while (dom.toastLayer.children.length > 3) {
    dom.toastLayer.removeChild(dom.toastLayer.firstChild);
  }
  setTimeout(() => { if (el.parentNode) el.parentNode.removeChild(el); }, 2400);
}

export function showActBanner(text) {
  if (!dom.actBanner) return;
  dom.actBanner.textContent = text;
  dom.actBanner.classList.add('visible');
  setTimeout(() => {
    if (dom.actBanner) dom.actBanner.classList.remove('visible');
  }, 3200);
}

export function refreshRecordsUI() {
  const count = records.endings.length;
  const total = Object.keys(ENDINGS).length;
  if (dom.recScore) dom.recScore.textContent = String(records.bestScore);
  if (dom.recTime) dom.recTime.textContent = `${Math.round(records.bestTimeSec)}с`;
  if (dom.recHrumkas) dom.recHrumkas.textContent = String(records.totalHrumkas);
  if (dom.recEndings) dom.recEndings.textContent = `${count}/${total}`;
  if (dom.startAlbumBtn) dom.startAlbumBtn.textContent = `📖 АЛЬБОМ КОНЦОВОК (${count}/${total})`;
  if (dom.endAlbumBtn) dom.endAlbumBtn.textContent = `📖 АЛЬБОМ КОНЦОВОК (${count}/${total})`;
  if (dom.hcTrophyBanner) dom.hcTrophyBanner.classList.toggle('show', records.hardcoreCleared);
}

export function openAlbumModal() {
  sound.unlock();
  sound.playClick();
  const total = Object.keys(ENDINGS).length;
  dom.albumTitle.textContent = `📖 КОНЦОВКИ: ${records.endings.length}/${total}`;
  dom.albumList.innerHTML = '';

  Object.values(ENDINGS).forEach((e) => {
    const isOpen = records.endings.includes(e.id);
    const row = document.createElement('div');
    row.className = 'album-item' + (isOpen ? ' unlocked' : '');
    row.innerHTML = `
      <div class="album-emoji">${isOpen ? e.emoji : '🔒'}</div>
      <div>
        <div class="album-name">${isOpen ? e.title : '??? (Скрыто)'}</div>
        <div class="album-hint">${isOpen ? e.desc : e.hint}</div>
      </div>
    `;
    dom.albumList.appendChild(row);
  });
  dom.albumModal.classList.add('open');
}

export function closeAlbumModal() {
  dom.albumModal.classList.remove('open');
}

export function applyBottleVisuals() {
  const d = DRINKS[state.activeDrink];
  if (!d) return;
  dom.liqTop.setAttribute('stop-color', d.colors.top);
  dom.liqBot.setAttribute('stop-color', d.colors.bot);
  dom.labelRect.setAttribute('fill', d.colors.label);
  dom.capRect.setAttribute('fill', d.colors.cap);
  dom.labelTitle.textContent = d.label;
  dom.labelSub.textContent = d.sub;
  dom.labelSub.setAttribute('fill', d.colors.sub);
  dom.activeDrinkName.textContent = d.name;

  dom.drinkCards.forEach((btn) => {
    btn.classList.toggle('active', btn.dataset.drink === state.activeDrink);
  });
}

export function updateHUD(nowSec, dt = 0.016) {
  const b = state.bottles[state.activeDrink] || { temp: 4, carb: 100, shakeFoamTimer: 0, frozenTaps: 0, liquidLevel: 0.85 };
  const d = DRINKS[state.activeDrink];
  const act = getCurrentAct(state.nightProgress, state.mode);

  // Часы и мета акта
  dom.clockVal.textContent = formatGameTime(state.nightProgress, state.mode);
  dom.clockVal.classList.toggle('turbo', state.energyTurboTimer > 0);

  const actTag = act ? act.shortTag : 'АКТ I';
  dom.clockMeta.innerHTML = state.mode.id === 'hardcore'
    ? `<span class="hc-tag">${actTag}</span><br>ДО 08:00`
    : `${actTag}<br>ДО 07:00`;

  dom.nightBarFill.style.width = `${Math.min(100, (state.nightProgress / state.mode.durationSec) * 100)}%`;
  dom.scorePill.textContent = `🏆 ${Math.round(state.score)}`;
  dom.sipsPill.textContent = `✨ ${state.goldenSips}/8`;
  dom.comboPill.textContent = `🥨 ${state.combos}/5`;
  if (dom.capsPill) dom.capsPill.textContent = `🪙 ${state.caps}`;

  // Бейдж доступности покупки в магазине
  const canBuyAny = state.caps >= 3;
  if (dom.shopBadge) dom.shopBadge.classList.toggle('show', canBuyAny);

  // Шкалы
  const tPct = Math.min(100, Math.max(0, state.thirst));
  dom.thirstVal.textContent = `${Math.round(tPct)}%`;
  dom.thirstFill.style.width = `${tPct}%`;
  const isThirstFrozen = state.ascensionTimer > 0 || state.mintTimer > 0;
  dom.thirstFill.classList.toggle('frozen', isThirstFrozen);
  dom.thirstFreezeTag.classList.toggle('show', isThirstFrozen);

  const fPct = Math.min(100, Math.max(0, state.fizz));
  const inGold = state.isDrinking && fPct >= 70 && fPct <= 90 && d.canAscend && b.temp <= 18.5 && (b.carb >= 25);
  dom.fizzVal.textContent = `${Math.round(fPct)}%`;
  dom.fizzFill.style.width = `${fPct}%`;
  dom.fizzFill.classList.toggle('in-gold', inGold);
  dom.goldTag.classList.toggle('show', inGold);

  dom.sugarVal.textContent = `${Math.round(state.sugarDebt)}%`;
  dom.sugarFill.style.width = `${Math.min(100, state.sugarDebt)}%`;

  dom.noiseVal.textContent = `${Math.round(state.noise)}%`;
  dom.noiseFill.style.width = `${Math.min(100, state.noise)}%`;

  // Шкала газации активной бутылки
  if (dom.carbVal && dom.carbFill) {
    const carbVal = Math.round(Math.min(100, Math.max(0, b.carb)));
    dom.carbVal.textContent = `${carbVal}%`;
    dom.carbFill.style.width = `${carbVal}%`;
    dom.carbFill.classList.toggle('low', carbVal < 25);
  }

  // Хрумка и таймер комбо-окна
  dom.hrumkaBtn.classList.toggle('combo-lit', state.comboWindowTimer > 0);
  dom.comboMeterFill.style.width = `${Math.min(100, Math.max(0, (state.comboWindowTimer / 4.5) * 100))}%`;

  // Морозилка и индикатор холода (0°C = 100% холод, 24°C = 0% холод)
  const coldPct = Math.max(0, Math.min(100, ((24 - b.temp) / 24) * 100));
  dom.coldMeterFill.style.width = `${coldPct}%`;
  dom.freezerBtn.classList.toggle('active', state.freezerActive);
  dom.freezerBtn.classList.toggle('urgent', !state.freezerActive && b.temp > 16 && b.frozenTaps === 0);
  dom.freezerSub.textContent = state.freezerActive
    ? 'МОРОЗИТ! (Жми)'
    : (b.temp > 15 ? 'НАГРЕЛАСЬ! Жми' : 'Выкл • Остудить');

  // Кнопка «В подушку» (Отрыжка)
  if (dom.burpVal && dom.burpMeterFill) {
    const burpPct = Math.min(100, Math.max(0, state.burp));
    dom.burpVal.textContent = `${Math.round(burpPct)}%`;
    dom.burpMeterFill.style.width = `${burpPct}%`;
    dom.burpBtn.classList.toggle('urgent', burpPct > 65);
    dom.burpBtn.classList.toggle('danger', burpPct > 82);
    dom.burpBtn.classList.toggle('venting', state.isVentingBurp);
  }

  // Кнопка «Туалет» (Клапан)
  if (dom.toiletVal && dom.toiletMeterFill) {
    const bldPct = Math.min(100, Math.max(0, state.bladder));
    dom.toiletVal.textContent = `${Math.round(bldPct)}%`;
    dom.toiletMeterFill.style.width = `${bldPct}%`;
    dom.toiletBtn.classList.toggle('urgent', bldPct > 70);
    dom.toiletBtn.classList.toggle('danger', bldPct > 85);
  }

  // Плашка температуры и кнопка встряхивания
  if (b.frozenTaps > 0) {
    dom.tempPill.className = 'temp-pill frozen';
    dom.tempPill.textContent = `🧊 ЛЁД (${b.frozenTaps}x)`;
  } else if (state.freezerActive) {
    dom.tempPill.className = 'temp-pill frozen';
    dom.tempPill.textContent = `❄️ МОРОЗИМ ${Math.round(b.temp)}°C`;
  } else if (b.temp <= 10) {
    dom.tempPill.className = 'temp-pill cold';
    dom.tempPill.textContent = `❄️ ${Math.round(b.temp)}°C`;
  } else if (b.temp <= 18) {
    dom.tempPill.className = 'temp-pill warm';
    dom.tempPill.textContent = `💧 ${Math.round(b.temp)}°C`;
  } else {
    dom.tempPill.className = 'temp-pill hot';
    dom.tempPill.textContent = `🥵 ${Math.round(b.temp)}°C`;
  }

  // Кнопка «Встряхнуть» (появляется при carb < 70 у газировок)
  if (dom.shakeBtn) {
    const canShake = d.canAscend && b.carb < 70 && b.frozenTaps === 0 && !state.freezerActive;
    dom.shakeBtn.classList.toggle('visible', canShake);
  }

  // Угловые бейджи на 6 кнопках напитков
  Object.keys(state.bottles).forEach((k) => {
    const el = document.getElementById(`corner-${k}`);
    if (!el) return;
    if (k === 'brine') el.textContent = `🥒${state.brineSipsLeft}`;
    else if (k === 'energy') el.textContent = `⚡${state.energySips}/5`;
    else el.textContent = state.bottles[k].frozenTaps > 0 ? '🧊' : `${Math.round(state.bottles[k].temp)}°`;
  });

  // Активные баффы в строке баффов
  if (dom.buffsRow) {
    let buffHtml = '';
    if (state.mintTimer > 0) {
      buffHtml += `<span class="buff-pill buff-mint">🍬 МЯТА ${state.mintTimer.toFixed(0)}с</span>`;
    }
    if (state.dragonBreathCharges > 0) {
      buffHtml += `<span class="buff-pill buff-dragon">🐉 ДРАКОН x${state.dragonBreathCharges}</span>`;
    }
    if (state.silencerTimer > 0) {
      buffHtml += `<span class="buff-pill buff-silencer">🔇 ГЛУШИТЕЛЬ ${state.silencerTimer.toFixed(0)}с</span>`;
    }
    if (b.shakeFoamTimer > 0) {
      buffHtml += `<span class="buff-pill buff-foam">🫧 ПЕНА ${b.shakeFoamTimer.toFixed(0)}с</span>`;
    }
    dom.buffsRow.innerHTML = buffHtml;
    dom.buffsRow.style.display = buffHtml ? 'flex' : 'none';
  }

  // Контекстная подсказка под бутылкой
  if (b.frozenTaps > 0) {
    dom.holdHint.textContent = `🧊 ТАПАЙ ПО БУТЫЛКЕ (${b.frozenTaps}), ЧТОБЫ РАЗБИТЬ ЛЁД`;
    dom.holdHint.className = 'hold-hint ice';
  } else if (state.freezerActive) {
    dom.holdHint.textContent = '❄️ ОХЛАЖДАЕТСЯ... ВЫКЛЮЧИ МОРОЗИЛКУ ДО 0°C';
    dom.holdHint.className = 'hold-hint ice';
  } else if (state.stunTimer > 0) {
    dom.holdHint.textContent = '😭 ОТДЫШИСЬ, ВАСЬ! ГАЗЫ В НОСУ!';
    dom.holdHint.className = 'hold-hint';
  } else if (inGold) {
    dom.holdHint.textContent = state.comboWindowTimer > 0
      ? '⚡ ОТПУСКАЙ — КОМБО ХРУМКА + ГАЗЯВА! ⚡'
      : (state.dragonBreathCharges > 0 ? '🐉 ОТПУСКАЙ — ДЫХАНИЕ ДРАКОНА! 🐉' : '✨ ОТПУСКАЙ СЕЙЧАС — ВОЗНЕСЕНИЕ! ✨');
    dom.holdHint.className = state.comboWindowTimer > 0 ? 'hold-hint combo' : 'hold-hint gold';
  } else if (state.comboWindowTimer > 0) {
    dom.holdHint.textContent = `🥨 КОМБО-ОКНО ${state.comboWindowTimer.toFixed(1)}С — ЛОВИ ЗОЛОТУЮ ЗОНУ!`;
    dom.holdHint.className = 'hold-hint combo';
  } else if (state.activeDrink === 'brine') {
    dom.holdHint.textContent = state.brineSipsLeft > 0
      ? `🥒 ТАПНИ БУТЫЛКУ — ВЫПИТЬ РАССОЛ (${state.brineSipsLeft} ОСТ.)`
      : '🥒 РАССОЛ ВЫПИТ! ПЕРЕКЛЮЧИ НА ГАЗЯВУ';
    dom.holdHint.className = 'hold-hint';
  } else if (b.carb < 25 && d.canAscend) {
    dom.holdHint.textContent = '🫠 ВЫДОХЛАСЬ! НАЖМИ «ВСТРЯХНУТЬ» ИЛИ КУПИ 2-Л В СХРОНЕ';
    dom.holdHint.className = 'hold-hint';
  } else if (state.isDrinking) {
    dom.holdHint.textContent = 'ТЯНИ ДО ЗОЛОТОЙ ЗОНЫ (70–90%)...';
    dom.holdHint.className = 'hold-hint';
  } else {
    dom.holdHint.textContent = 'ЗАЖМИ БУТЫЛКУ, ВАСЬ!';
    dom.holdHint.className = 'hold-hint';
  }

  // Унылая кнопка воды из-под крана при Сушняке > 68%
  dom.tapWaterBtn.classList.toggle('visible', state.phase === 'playing' && state.thirst > 68 && !state.ev.active);

  // Экранные визуальные эффекты
  const desertOp = state.thirst > 75 && state.ascensionTimer <= 0
    ? Math.min(1, Math.max(0, (state.thirst - 75) / 23))
    : 0;
  dom.fxDesert.style.opacity = desertOp.toFixed(2);
  dom.fxAscension.classList.toggle('active', state.ascensionTimer > 0);
  dom.fxVhs.classList.toggle('active', state.vhsTimer > 0);
  dom.fxHeartbeat.classList.toggle('active', state.energyTurboTimer > 0);
  dom.fxStun.classList.toggle('active', state.stunTimer > 0);
  if (state.stunTimer > 0) dom.stunFill.style.width = `${(state.stunTimer / 2.5) * 100}%`;

  // Оверлеи Актов 3 и 4: Блэкаут и Рассвет
  if (dom.fxBlackout) dom.fxBlackout.classList.toggle('active', act.index === 3 && state.phase === 'playing');
  if (dom.fxDawn) dom.fxDawn.classList.toggle('active', act.index === 4 && state.phase === 'playing');

  // Многослойная аура холодильника
  dom.fridgeAura.classList.toggle('ascend', state.ascensionTimer > 0);
  dom.fridgeAura.classList.toggle('combo-window', state.comboWindowTimer > 0 && state.ascensionTimer <= 0);
  dom.fridgeAura.classList.toggle('dragon-buff', state.dragonBreathCharges > 0);

  dom.app.classList.toggle('state-flat', state.flatTimer > 0);
  dom.app.classList.toggle('state-stun', state.stunTimer > 0);
  dom.app.classList.toggle('state-dry-tremor', (state.thirst > 84 || state.bladder >= 98) && state.stunTimer <= 0 && state.ascensionTimer <= 0);

  // Анимация самой SVG-бутылки
  if (state.isDrinking && b.frozenTaps === 0) {
    const amp = 1.2 + (state.fizz / 100) * 6.5;
    const dx = (Math.random() - 0.5) * amp;
    const dy = (Math.random() - 0.5) * amp;
    const rot = -6 + (Math.random() - 0.5) * amp * 0.8;
    dom.bottleWrap.style.transform = `translate(${dx.toFixed(1)}px,${dy.toFixed(1)}px) rotate(${rot.toFixed(1)}deg)`;
  } else if (state.freezerActive) {
    const sh = (Math.random() - 0.5) * 2.2;
    dom.bottleWrap.style.transform = `translate(${sh.toFixed(1)}px, 0px) scale(0.96)`;
  } else if (b.frozenTaps === 0) {
    dom.bottleWrap.style.transform = `translateY(${(Math.sin(nowSec * 2.4) * 3.2).toFixed(1)}px)`;
  }

  // Волна жидкости и пена
  const topY = 235 - b.liquidLevel * 155;
  const wAmp = state.isDrinking ? 7.5 : 3;
  const wOff = Math.sin(nowSec * (state.isDrinking ? 14 : 4)) * wAmp;
  dom.liquidWave.setAttribute('d', `M20,${topY.toFixed(1)} Q70,${(topY + wOff).toFixed(1)} 120,${topY.toFixed(1)} L120,260 L20,260 Z`);
  dom.liquidFoam.setAttribute('d', `M20,${topY.toFixed(1)} Q70,${(topY + wOff).toFixed(1)} 120,${topY.toFixed(1)} L120,${(topY + 7).toFixed(1)} Q70,${(topY + wOff + 7).toFixed(1)} 20,${(topY + 7).toFixed(1)} Z`);

  // Пузырьки внутри напитка (с учётом dt для 120Hz дисплеев)
  const noBubbles = state.activeDrink === 'flat' || state.activeDrink === 'brine' || b.carb < 15;
  const bMult = noBubbles ? 0.08 : (state.isDrinking ? 2.8 : (b.shakeFoamTimer > 0 ? 2.2 : 1.0));
  for (let i = 0; i < svgBubbleNodes.length; i++) {
    const nb = svgBubbleNodes[i];
    nb.y -= nb.spd * bMult * dt;
    if (nb.y < topY + 6) {
      nb.y = 244;
      nb.x = 38 + Math.random() * 64;
    }
    nb.el.setAttribute('cx', (nb.x + Math.sin(nowSec * 4 + nb.ph) * 2.4).toFixed(1));
    nb.el.setAttribute('cy', nb.y.toFixed(1));
    nb.el.setAttribute('opacity', noBubbles ? '0.06' : (state.isDrinking ? '0.82' : '0.46'));
  }

  // Конденсат, иней, волны жара и ледяная глыба
  dom.coldAura.setAttribute('opacity', b.temp <= 10 ? '0.55' : '0.06');
  dom.condensation.setAttribute('opacity', b.temp <= 10 ? '0.85' : '0.1');
  const frostOp = state.freezerActive ? Math.max(0, Math.min(0.75, (10 - b.temp) / 10)) : 0;
  dom.frostCoat.setAttribute('opacity', frostOp.toFixed(2));
  dom.heatWaves.setAttribute('opacity', b.temp >= 17 ? (0.45 + Math.sin(nowSec * 8) * 0.35).toFixed(2) : '0');

  if (b.frozenTaps > 0) {
    dom.iceBlock.setAttribute('opacity', '1');
    dom.iceLabel.textContent = `🧊 ТАП x${b.frozenTaps}`;
    dom.iceCrack1.setAttribute('opacity', b.frozenTaps <= 2 ? '1' : '0');
    dom.iceCrack2.setAttribute('opacity', b.frozenTaps <= 1 ? '1' : '0');
  } else {
    dom.iceBlock.setAttribute('opacity', '0');
  }
}

// Карточка результата с информацией Патча 3.0
export function copyScorecard() {
  sound.unlock();
  sound.playClick();
  if (!state.lastResult) return;
  const r = state.lastResult;
  const text = [
    '03:17. ПРОТОКОЛ ГАЗЯВА 3.0: НОЧНАЯ СМЕНА',
    `🎮 Режим: ${r.modeName}`,
    `🏁 Концовка: ${r.endingTitle}`,
    `🎭 Достигнут: ${r.actName}`,
    `⏰ Время: ${r.clockStr} | 🏆 Очки: ${r.score}`,
    `🪙 Заработано крышечек: ${r.capsEarned}`,
    `✨ Золотых глотков: ${r.goldenSips} | 🥨 Комбо: ${r.combos}`,
    `📖 Открыто концовок: ${r.unlocked}/${r.total}`,
    'kaplao.ru/gazyava'
  ].join('\n');

  const showCopied = () => {
    dom.copyBanner.classList.add('show');
    setTimeout(() => dom.copyBanner.classList.remove('show'), 2200);
  };

  const fallbackCopy = () => {
    try {
      const ta = document.createElement('textarea');
      ta.value = text;
      ta.setAttribute('readonly', '');
      ta.style.position = 'fixed';
      ta.style.top = '-9999px';
      ta.style.left = '-9999px';
      document.body.appendChild(ta);
      ta.focus();
      ta.select();
      ta.setSelectionRange(0, text.length);
      document.execCommand('copy');
      document.body.removeChild(ta);
      showCopied();
    } catch (err) {
      showToast('НЕ ВЫШЛО СКОПИРОВАТЬ — СДЕЛАЙ СКРИН, ВАСЬ!', 'info');
    }
  };

  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).then(showCopied).catch(fallbackCopy);
  } else {
    fallbackCopy();
  }
}
