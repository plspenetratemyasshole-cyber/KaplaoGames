/* ==========================================================================
   НОЧНЫЕ МИНИ-ИВЕНТЫ (КРЫШКА, БАТЯ, ПАКЕТ, РЕЙД, ПИСК ХОЛОДИЛЬНИКА, ТУАЛЕТ)
   ========================================================================== */
import { state } from './state.js';
import { sound } from './audio.js';

let dom = null;
let showToast = () => {};
let addNoise = () => {};
let stopDrinking = () => {};
let endGame = () => {};
let pushPhoneNotif = () => {};

export function initEvents(deps) {
  dom = deps.dom;
  showToast = deps.showToast || (() => {});
  addNoise = deps.addNoise || (() => {});
  stopDrinking = deps.stopDrinking || (() => {});
  endGame = deps.endGame || (() => {});
  pushPhoneNotif = deps.pushPhoneNotif || (() => {});
}

export function triggerNextNightEvent() {
  stopDrinking(false);
  state.isVentingBurp = false;
  sound.stopVentSound();
  if (state.freezerActive) {
    state.freezerActive = false;
    sound.stopFreezerHum();
  }

  // В Актах 3 и 4 добавляется писк незакрытой дверцы холодильника
  const pool = state.currentAct >= 3
    ? ['cap', 'dad', 'bag', 'fridge_beep']
    : ['cap', 'dad', 'bag'];

  const pick = pool[state.eventIdx % pool.length];
  state.eventIdx++;
  openNightEvent(pick);
}

export function openNightEvent(type) {
  const ev = state.ev;
  ev.active = true;
  ev.type = type;
  ev.progress = 0;
  ev.pressure = 0;
  ev.holdingSleep = false;
  ev.holdingToilet = false;
  ev.toiletPhase = 1;
  ev.stepTick = 0;
  ev.fridgePos = 10;
  ev.fridgeDir = 1;
  ev.fridgeBeepTick = 0;
  ev.angle = 0;

  dom.eventCard.className = 'event-card' + (type === 'dad' || type === 'fridge_beep' ? ' danger-card' : '');
  dom.evBarWrap.style.display = 'block';
  dom.evTimer.style.display = 'block';
  dom.evBody.innerHTML = '';

  if (type === 'cap') {
    ev.needed = 4;
    ev.timeLeft = state.mode.id === 'hardcore' ? 6.8 : 8.0;
    dom.evTag.textContent = '⚠️ НОЧНОЙ СТЕЛС';
    dom.evTitle.textContent = 'ГРОМКАЯ КРЫШКА (ПШШШ)';
    dom.evDesc.innerHTML = 'Вась, бутылка под давлением! Открути за <b>4 спокойных тапа</b>, чтобы давление не ударило в красную зону.';
    dom.evBarLabel.textContent = '🔊 ДАВЛЕНИЕ (ПШШШ)';

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.id = 'evActionBtn';
    btn.className = 'ev-round-btn ev-cap-btn';
    btn.innerHTML = '<span id="evCnt" style="font-size:1.3rem;">0 / 4</span><span style="font-size:0.62rem;">КРУТИТЬ</span>';
    btn.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      handleCapTap(btn);
    });
    dom.evBody.appendChild(btn);
  } else if (type === 'dad') {
    ev.timeLeft = 3.2;
    dom.evTag.textContent = '🚨 ТРЕВОГА В КОРИДОРЕ';
    dom.evTitle.textContent = 'ШАГИ! БАТЯ ИДЁТ НА КУХНЮ!';
    dom.evDesc.innerHTML = '<b>ЗАМРИ, ВАСЬ!</b> Зажми кнопку и притворись спящим на 3 секунды — ни глотка и ни хруста!';
    dom.evBarLabel.textContent = '👀 БАТЯ ПРОХОДИТ МИМО';

    const sleepBtn = document.createElement('button');
    sleepBtn.type = 'button';
    sleepBtn.id = 'evSleepBtn';
    sleepBtn.className = 'ev-sleep-btn';
    sleepBtn.textContent = '🛌 ЗАЖМИ: ПРИТВОРИТЬСЯ СПЯЩИМ';

    sleepBtn.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      sound.unlock();
      ev.holdingSleep = true;
      sleepBtn.classList.add('holding');
      sleepBtn.textContent = '😴 ДЫШИМ ТИХО... ТССС...';
    });
    const releaseSleep = () => {
      ev.holdingSleep = false;
      sleepBtn.classList.remove('holding');
      sleepBtn.textContent = '🛌 ЗАЖМИ: ПРИТВОРИТЬСЯ СПЯЩИМ';
    };
    sleepBtn.addEventListener('pointerup', releaseSleep);
    sleepBtn.addEventListener('pointercancel', releaseSleep);

    const baitBtn = document.createElement('button');
    baitBtn.type = 'button';
    baitBtn.className = 'btn-secondary';
    baitBtn.style.marginBottom = '8px';
    baitBtn.style.fontSize = '0.72rem';
    baitBtn.textContent = '🥨 Рискнуть и схрумать кирик (НЕ НАЖИМАЙ!)';
    baitBtn.addEventListener('click', () => {
      sound.unlock();
      sound.playCrunch();
      closeEventModal();
      showToast('💥 БАТЯ ВКЛЮЧИЛ СВЕТ: «ВАСЬ, КАКОГО ЧЁРТА?!»', 'danger');
      endGame('woke_building');
    });

    dom.evBody.appendChild(sleepBtn);
    dom.evBody.appendChild(baitBtn);
    sound.playFootstep();
  } else if (type === 'bag') {
    ev.needed = 5;
    ev.timeLeft = state.mode.id === 'hardcore' ? 4.2 : 5.2;
    dom.evTag.textContent = '🥨 ШУРШАЩИЙ ПАКЕТ';
    dom.evTitle.textContent = 'ПАКЕТ ХРУМКИ ОРЁТ НА ВСЮ КУХНЮ!';
    dom.evDesc.innerHTML = 'Пакет из фольги шуршит на 120 децибел! <b>Быстро тапни 5 раз «Прижать к груди»</b>, пока Шум не взорвался!';
    dom.evBarLabel.textContent = '📢 ШУРШАНИЕ ПАКЕТА';

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ev-round-btn ev-bag-btn';
    btn.innerHTML = '<span id="evCnt" style="font-size:1.3rem;">0 / 5</span><span style="font-size:0.58rem;">ПРИЖАТЬ!</span>';
    btn.addEventListener('pointerdown', (e) => {
      e.preventDefault();
      handleBagTap();
    });
    dom.evBody.appendChild(btn);
    sound.playCrunch();
  } else if (type === 'fridge_beep') {
    // 5-й ивент Патча 3.0: Дверца холодильника пищит
    ev.timeLeft = state.mode.id === 'hardcore' ? 4.5 : 5.5;
    dom.evTag.textContent = '❄️ ТРЕВОГА ХОЛОДИЛЬНИКА';
    dom.evTitle.textContent = 'ХОЛОДИЛЬНИК ПИЩИТ! (ПИП-ПИП)';
    dom.evDesc.innerHTML = 'Дверца не закрылась до конца! <b>Нажми «ПРИХЛОПНУТЬ» ровно когда бегунок в зелёной зоне (40-60%)</b> — иначе разбудишь весь подъезд!';
    dom.evBarLabel.textContent = '🎯 ТАЙМИНГ ПРИХЛОПЫВАНИЯ';

    const slamBtn = document.createElement('button');
    slamBtn.type = 'button';
    slamBtn.className = 'btn-primary';
    slamBtn.style.marginTop = '10px';
    slamBtn.style.fontSize = '0.92rem';
    slamBtn.textContent = '🚪 ПРИХЛОПНУТЬ ДВЕРЦУ (SPACE)';
    slamBtn.addEventListener('click', () => handleFridgeSlam());
    dom.evBody.appendChild(slamBtn);

    sound.playFridgeBeep();
  } else if (type === 'raid') {
    ev.timeLeft = 10.0;
    dom.evTag.textContent = '🔦 НОЧНОЙ РЕЙД • 04:30';
    dom.evTitle.textContent = 'ВАСЬ ДОБРАЛСЯ ДО НИЖНЕЙ ПОЛКИ';
    dom.evDesc.innerHTML = 'Пока все спят, можно залутать один легендарный ништяк. Выбирай быстро!';
    dom.evBarWrap.style.display = 'none';

    const opt1 = document.createElement('button');
    opt1.type = 'button';
    opt1.className = 'ev-choice-btn';
    opt1.innerHTML = `
      <span class="ev-choice-emoji">🥒</span>
      <span><b>Залутать свежий рассол и лёд</b>
        <span class="ev-choice-sub">+2 глотка рассола и все бутылки остывают до +3°C</span>
      </span>`;
    opt1.addEventListener('click', () => {
      sound.unlock();
      sound.playAscension(false);
      state.brineSipsLeft += 2;
      Object.keys(state.bottles).forEach((k) => {
        if (k !== 'flat') {
          state.bottles[k].temp = 3;
          state.bottles[k].frozenTaps = 0;
        }
      });
      closeEventModal();
      showToast('🥒 ЛУТ ПОЛУЧЕН! +2 РАССОЛА И ЛЕДЯНЫЕ БУТЫЛКИ!', 'vhs');
    });

    const opt2 = document.createElement('button');
    opt2.type = 'button';
    opt2.className = 'ev-choice-btn';
    opt2.innerHTML = `
      <span class="ev-choice-emoji">🥨</span>
      <span><b>Взять мега-пачку кириков</b>
        <span class="ev-choice-sub">Полный сброс Сахарного долга, -15% Шума и +400 очков</span>
      </span>`;
    opt2.addEventListener('click', () => {
      sound.unlock();
      sound.playCrunch();
      state.sugarDebt = 0;
      state.noise = Math.max(0, state.noise - 15);
      state.score += 400;
      state.comboWindowTimer = 5.0;
      closeEventModal();
      showToast('🥨 МЕГА-ПАЧКА КИРИКОВ! САХАР 0%, +400 ОЧКОВ!', 'crunch');
    });

    // 3-й выбор Патча 3.0: Копилка в прихожей
    const opt3 = document.createElement('button');
    opt3.type = 'button';
    opt3.className = 'ev-choice-btn';
    opt3.innerHTML = `
      <span class="ev-choice-emoji">🪙</span>
      <span><b>Обчистить копилку в прихожей</b>
        <span class="ev-choice-sub">+7 крышечек для схрона и -15% Шума</span>
      </span>`;
    opt3.addEventListener('click', () => {
      sound.unlock();
      sound.playBuy();
      state.caps += 7;
      state.totalCapsEarned += 7;
      state.noise = Math.max(0, state.noise - 15);
      closeEventModal();
      showToast('🪙 КОПИЛКА ОБЧИЩЕНА! +7 КРЫШЕЧЕК, ШУМ -15%!', 'gold');
    });

    dom.evBody.appendChild(opt1);
    dom.evBody.appendChild(opt2);
    dom.evBody.appendChild(opt3);
  } else if (type === 'toilet') {
    // Интерактивный стелс-поход в туалет Патча 3.0
    ev.timeLeft = 8.0;
    ev.toiletPhase = 1;
    dom.evTag.textContent = '🚽 КЛАПАН НА ПРЕДЕЛЕ';
    dom.evTitle.textContent = 'СТЕЛС-ПОХОД В ТУАЛЕТ';
    dom.evDesc.innerHTML = 'Пол скрипит на каждом шагу! <b>Зажми кнопку и крадись по половицам</b>, чтобы аккуратно сбросить давление клапана.';
    dom.evBarLabel.textContent = '🫀 НАПОЛНЕННОСТЬ КЛАПАНА';

    renderToiletPhase1();
  }

  dom.eventModal.classList.add('open');
}

function renderToiletPhase1() {
  dom.evBody.innerHTML = '';
  const sneakBtn = document.createElement('button');
  sneakBtn.type = 'button';
  sneakBtn.className = 'ev-sleep-btn';
  sneakBtn.textContent = '🦶 ЗАЖМИ: КРАСТЬСЯ ПО ПОЛОВИЦАМ';

  sneakBtn.addEventListener('pointerdown', (e) => {
    e.preventDefault();
    sound.unlock();
    state.ev.holdingToilet = true;
    sneakBtn.classList.add('holding');
    sneakBtn.textContent = '🤫 КРАДЁМСЯ... КЛАПАН СБРАСЫВАЕТСЯ...';
  });

  const onReleaseSneak = () => {
    if (!state.ev.holdingToilet) return;
    state.ev.holdingToilet = false;
    sneakBtn.classList.remove('holding');
    sneakBtn.textContent = '🦶 ЗАЖМИ: КРАСТЬСЯ ПО ПОЛОВИЦАМ';
    if (state.bladder > 0) {
      addNoise(4);
      sound.playClick();
      showToast('🪵 СКРИПНУЛА ПОЛОВИЦА! +4 ШУМА', 'roast');
    }
  };

  sneakBtn.addEventListener('pointerup', onReleaseSneak);
  sneakBtn.addEventListener('pointercancel', onReleaseSneak);

  dom.evBody.appendChild(sneakBtn);
}

function renderToiletPhase2() {
  state.ev.toiletPhase = 2;
  state.ev.holdingToilet = false;
  dom.evBarWrap.style.display = 'none';
  dom.evTimer.style.display = 'none';
  dom.evTitle.textContent = 'ДИЛЕММА СМЫВА В 4 УТРА';
  dom.evDesc.innerHTML = 'Клапан пуст! Но теперь главный вопрос человечества: <b>смывать со звуком турбины или позорно сбежать?</b>';
  dom.evBody.innerHTML = '';

  const flushBtn = document.createElement('button');
  flushBtn.type = 'button';
  flushBtn.className = 'ev-choice-btn';
  const noiseGain = state.silencerTimer > 0 ? 18 : 36;
  flushBtn.innerHTML = `
    <span class="ev-choice-emoji">🌊</span>
    <span><b>СМЫТЬ КАК ЧЕЛОВЕК</b>
      <span class="ev-choice-sub">+${noiseGain} Шума (звук Боинга), +220 очков, +2 🪙</span>
    </span>`;
  flushBtn.addEventListener('click', () => {
    sound.unlock();
    sound.playFlush();
    addNoise(noiseGain);
    state.score += 220;
    state.caps += 2;
    state.totalCapsEarned += 2;
    closeEventModal();
    showToast('🌊 БОИНГ 747 ПОШЁЛ НА ВЗЛЁТ! +ШУМ, НО СОВЕСТЬ ЧИСТА', 'info');
  });

  const shameBtn = document.createElement('button');
  shameBtn.type = 'button';
  shameBtn.className = 'ev-choice-btn';
  shameBtn.innerHTML = `
    <span class="ev-choice-emoji">🥷</span>
    <span><b>НЕ СМЫВАТЬ (СТЕЛС-ПОЗОР)</b>
      <span class="ev-choice-sub">0 Шума, -250 очков совести, стыд на весь стояк</span>
    </span>`;
  shameBtn.addEventListener('click', () => {
    sound.unlock();
    sound.playClick();
    state.score = Math.max(0, state.score - 250);
    state.unflushedCount++;
    closeEventModal();
    pushPhoneNotif('Совесть-бот', '🚽', 'Вась, ты серьёзно НЕ СМЫЛ?! Позор на весь стояк!');
    showToast('🥷 СТЕЛС-ПОЗОР! СОВЕСТЬ МИНУСОВАЛА 250 ОЧКОВ', 'roast');
  });

  dom.evBody.appendChild(flushBtn);
  dom.evBody.appendChild(shameBtn);
}

export function handleFridgeSlam() {
  sound.unlock();
  const ev = state.ev;
  if (!ev.active || ev.type !== 'fridge_beep') return;

  // Проверяем попадание в зелёную зону (40% - 60%)
  if (ev.fridgePos >= 40 && ev.fridgePos <= 60) {
    sound.playClick();
    state.score += 280;
    state.caps += 3;
    state.totalCapsEarned += 3;
    closeEventModal();
    showToast('🤫 ДВЕРЦА ЩЁЛКНУЛА БЕЗ ЗВУКА! (+280, +3 🪙)', 'info');
  } else {
    sound.playFootstep();
    addNoise(28);
    closeEventModal();
    showToast('💥 ХЛОПНУЛ ТАК, ЧТО МАГНИТИКИ ОСЫПАЛИСЬ! +ШУМ!', 'danger');
  }
}

export function handleCapTap(btn) {
  sound.unlock();
  const ev = state.ev;
  if (!ev.active || ev.type !== 'cap') return;

  ev.progress++;
  ev.pressure = Math.min(100, ev.pressure + (state.mode.id === 'hardcore' ? 48 : 42));
  ev.angle += 75;
  btn.style.transform = `rotate(${ev.angle}deg)`;
  const cntEl = document.getElementById('evCnt');
  if (cntEl) cntEl.textContent = `${ev.progress} / ${ev.needed}`;

  if (ev.pressure >= 100) {
    sound.playPssh(true);
    closeEventModal();
    state.thirst = Math.min(99, state.thirst + 10);
    addNoise(28);
    showToast('ПШШШШ!! СЛИШКОМ ГРОМКО, ВАСЬ! +ШУМ!', 'danger');
    return;
  }

  sound.playPssh(false);
  if (ev.progress >= ev.needed) {
    closeEventModal();
    state.score += 200;
    state.caps += 3;
    state.totalCapsEarned += 3;
    state.thirst = Math.max(0, state.thirst - 5);
    showToast('ТИХИЙ ПШШ... СТЕЛС 100 УРОВНЯ, ВАСЬ (+3 🪙) 🤫', 'info');
  }
}

export function handleBagTap() {
  sound.unlock();
  const ev = state.ev;
  if (!ev.active || ev.type !== 'bag') return;

  ev.progress++;
  sound.playClick();
  const cntEl = document.getElementById('evCnt');
  if (cntEl) cntEl.textContent = `${ev.progress} / ${ev.needed}`;

  if (ev.progress >= ev.needed) {
    closeEventModal();
    state.score += 220;
    state.caps += 3;
    state.totalCapsEarned += 3;
    showToast('🤫 ПАКЕТ ЗАГЛУШЕН! +220 ОЧКОВ, +3 🪙!', 'crunch');
  }
}

export function closeEventModal() {
  state.ev.active = false;
  state.ev.type = null;
  state.ev.holdingSleep = false;
  state.ev.holdingToilet = false;
  dom.eventModal.classList.remove('open');
}

export function updateActiveEvent(dt) {
  const ev = state.ev;
  ev.timeLeft -= dt;

  if (ev.type === 'cap') {
    ev.pressure = Math.max(0, ev.pressure - 38 * dt);
    dom.evBarPct.textContent = `${Math.round(ev.pressure)}%`;
    dom.evBarFill.style.width = `${ev.pressure}%`;
    dom.evTimer.textContent = `Осталось: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;
    if (ev.timeLeft <= 0) {
      sound.playPssh(true);
      closeEventModal();
      addNoise(22);
      showToast('НЕ УСПЕЛ ОТКРЫТЬ! КРЫШКА БАХНУЛА!', 'danger');
    }
  } else if (ev.type === 'dad') {
    ev.stepTick += dt;
    if (ev.stepTick >= 0.72) {
      ev.stepTick = 0;
      sound.playFootstep();
    }
    const pct = Math.min(100, ((3.2 - ev.timeLeft) / 3.2) * 100);
    dom.evBarPct.textContent = ev.holdingSleep ? 'ТИХО 😴' : 'НЕ ДВИГАЙСЯ!';
    dom.evBarFill.style.width = `${pct}%`;
    dom.evTimer.textContent = `Батя уйдёт через: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;

    if (ev.timeLeft <= 0) {
      const held = ev.holdingSleep;
      closeEventModal();
      if (held) {
        state.score += 260;
        state.caps += 3;
        state.totalCapsEarned += 3;
        state.noise = Math.max(0, state.noise - 14);
        showToast('😴 БАТЯ УШЁЛ СПАТЬ! ВАСЬ — МАСТЕР НИДЗЮЦУ (+260, +3 🪙)', 'info');
      } else {
        state.score += 120;
        showToast('😮‍💨 ПРОНЕСЛО! БАТЯ ПОШЁЛ ДАЛЬШЕ...', 'info');
      }
    }
  } else if (ev.type === 'bag') {
    const total = state.mode.id === 'hardcore' ? 4.2 : 5.2;
    const pct = Math.min(100, ((total - ev.timeLeft) / total) * 100);
    dom.evBarPct.textContent = `${Math.round(pct)}%`;
    dom.evBarFill.style.width = `${pct}%`;
    dom.evTimer.textContent = `Взрыв шума через: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;
    if (ev.timeLeft <= 0) {
      sound.playCrunch();
      closeEventModal();
      addNoise(32);
      showToast('📢 ПАКЕТ ПРОШУРШАЛ НА ВЕСЬ ДОМ! +ШУМ!', 'danger');
    }
  } else if (ev.type === 'fridge_beep') {
    // Бегунок осциллирует между 0 и 100% со скоростью 120%/сек
    const speed = state.mode.id === 'hardcore' ? 140 : 115;
    ev.fridgePos += ev.fridgeDir * speed * dt;
    if (ev.fridgePos >= 100) {
      ev.fridgePos = 100;
      ev.fridgeDir = -1;
    } else if (ev.fridgePos <= 0) {
      ev.fridgePos = 0;
      ev.fridgeDir = 1;
    }

    ev.fridgeBeepTick += dt;
    if (ev.fridgeBeepTick >= 0.65) {
      ev.fridgeBeepTick = 0;
      sound.playFridgeBeep();
    }

    dom.evBarPct.textContent = `${Math.round(ev.fridgePos)}%`;
    dom.evBarFill.style.width = `${ev.fridgePos}%`;
    const inSweet = ev.fridgePos >= 40 && ev.fridgePos <= 60;
    dom.evBarFill.style.background = inSweet ? '#a3e635' : 'linear-gradient(90deg, #ff3358, #fb923c)';
    dom.evTimer.textContent = `Пищит! Осталось: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;

    if (ev.timeLeft <= 0) {
      sound.playFridgeBeep();
      closeEventModal();
      addNoise(30);
      showToast('🚨 ХОЛОДИЛЬНИК РАЗБУДИЛ ВСЕХ ПИСКОМ! +ШУМ!', 'danger');
    }
  } else if (ev.type === 'toilet') {
    if (ev.toiletPhase === 1) {
      if (ev.holdingToilet) {
        state.bladder = Math.max(0, state.bladder - (100 / 2.4) * dt);
        ev.stepTick += dt;
        if (ev.stepTick >= 0.35) {
          ev.stepTick = 0;
          sound.playClick();
        }
      }
      dom.evBarPct.textContent = `${Math.round(state.bladder)}%`;
      dom.evBarFill.style.width = `${state.bladder}%`;
      dom.evTimer.textContent = `Время на стелс: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;

      if (state.bladder <= 0) {
        renderToiletPhase2();
      } else if (ev.timeLeft <= 0) {
        closeEventModal();
        addNoise(15);
        showToast('🚽 ВАСЬ ПОТОРОПИЛСЯ И ШУМНО ВЫБЕЖАЛ ИЗ ТУАЛЕТА!', 'danger');
      }
    }
  } else if (ev.type === 'raid') {
    dom.evTimer.textContent = `Холодильник закроется через: ${Math.max(0, ev.timeLeft).toFixed(1)} сек`;
    if (ev.timeLeft <= 0) {
      closeEventModal();
      showToast('ХОЛОДИЛЬНИК ЗАКРЫЛСЯ — НИЧЕГО НЕ ВЗЯЛ!', 'roast');
    }
  }
}
