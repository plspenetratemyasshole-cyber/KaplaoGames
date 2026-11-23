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
  FLAT_ROASTS
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
import { state, createInitialState, formatGameTime } from './state.js';
import {
  initEvents,
  triggerNextNightEvent,
  openNightEvent,
  handleCapTap,
  handleBagTap,
  closeEventModal,
  updateActiveEvent
} from './events.js';
import {
  dom,
  initDom,
  showToast,
  refreshRecordsUI,
  openAlbumModal,
  closeAlbumModal,
  applyBottleVisuals,
  updateHUD,
  copyScorecard
} from './ui.js';

let lastTs = 0;
const DRINK_KEYS = ['cola', 'mineral', 'duchess', 'energy', 'brine', 'flat'];

function startGame() {
        state.mode = MODES[state.selectedMode] || MODES.classic;
        state.phase = 'playing';
        state.nightProgress = 0;
        state.realElapsed = 0;
        state.score = 0;

        state.thirst = state.mode.id === 'hardcore' ? 42 : 34;
        state.fizz = 0;
        state.sugarDebt = state.mode.id === 'hardcore' ? 18 : 0;
        state.noise = 0;

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

        state.nextEventAt = state.mode.eventInterval;
        state.raidTriggered = false;
        state.eventIdx = 0;
        state.ev.active = false;

        Object.keys(DRINKS).forEach((k) => {
          state.bottles[k] = {
            temp: DRINKS[k].initTemp, // 0..24°C (<=0 = лёд)
            frozenTaps: 0,
            liquidLevel: 0.88
          };
        });

        dom.app.className = state.mode.id === 'hardcore' ? 'mode-hardcore' : '';
        dom.nightBarFill.className = 'night-bar-fill' + (state.mode.id === 'hardcore' ? ' hc' : '');
        dom.clockMeta.innerHTML = state.mode.id === 'hardcore'
          ? '<span class="hc-tag">1 ЯНВ</span><br>ДО 08:00'
          : 'ДО<br>07:00';
        dom.hrumkaFlavor.textContent = HRUMKA_FLAVORS[0];

        dom.toastLayer.innerHTML = '';
        dom.eventModal.classList.remove('open');
        dom.startScreen.classList.remove('visible');
        dom.endScreen.classList.remove('visible');
        dom.fxAscension.classList.remove('active');
        dom.fxVhs.classList.remove('active');
        dom.fxHeartbeat.classList.remove('active');
        dom.fxStun.classList.remove('active');
        dom.fxDesert.style.opacity = '0';

        sound.stopFreezerHum();
        applyBottleVisuals();
        updateHUD(0);

        showToast(
          state.mode.id === 'hardcore'
            ? '🔥 1 ЯНВАРЯ! ВАСЬ, ПОХМЕЛЬНЫЙ СУШНЯК!'
            : '03:17. ВАСЬ, ХОЛОДИЛЬНИК ЗОВЁТ ❄️',
          state.mode.id === 'hardcore' ? 'danger' : 'info'
        );
      }

function startDrinking(pointerId) {
        sound.unlock();
        if (state.phase !== 'playing' || state.ev.active) return;

        const b = state.bottles[state.activeDrink];

        // Если бутылка в глыбе льда — тап раскалывает лёд!
        if (b.frozenTaps > 0) {
          b.frozenTaps--;
          sound.playIceShatter();
          const rect = dom.bottleWrap.getBoundingClientRect();
          spawnParticles('ice', rect.left + rect.width / 2, rect.top + rect.height / 2, 12);
          dom.bottleWrap.style.transform = 'scale(0.9) rotate(-5deg)';
          setTimeout(() => { dom.bottleWrap.style.transform = ''; }, 90);
          addNoise(4);
          if (b.frozenTaps === 0) {
            b.temp = 2;
            showToast('ЛЁД РАЗБИТ! +2°C, ВОИСТИНУ ЛЕДЯНАЯ ❄️', 'info');
          }
          return;
        }

        // Нельзя пить, пока бутылка внутри морозилки
        if (state.freezerActive) {
          showToast('ВАСЬ, ДОСТАНЬ ИЗ МОРОЗИЛКИ СНАЧАЛА ❄️', 'info');
          return;
        }

        if (state.stunTimer > 0) return;

        // Бабушкин рассол: мгновенный спасительный глоток (без пузырьков)
        if (state.activeDrink === 'brine') {
          if (state.brineSipsLeft <= 0) {
            showToast('РАССОЛ КОНЧИЛСЯ, ВАСЬ! ИЩИ В РЕЙДЕ 🥒', 'danger');
            sound.playClick();
            return;
          }
          state.brineSipsLeft--;
          state.lastDrunkId = 'brine';
          state.sugarDebt = 0;
          state.thirst = Math.max(0, state.thirst - (state.mode.id === 'hardcore' ? 36 : 32));
          state.score += 180;
          b.liquidLevel = Math.max(0.25, b.liquidLevel - 0.22);
          sound.playGulp();
          showToast('🥒 БАБУШКИН РАССОЛ! САХАР = 0, СУШНЯК В НОЛЬ!', 'vhs');
          return;
        }

        state.isDrinking = true;
        state.activePointerId = pointerId;
        state.lastDrunkId = state.activeDrink;
        state.energySipAccum = 0;
        sound.startFizz();
        sound.playGulp();

        if (state.activeDrink === 'flat' && state.flatRoastCd <= 0) {
          state.flatRoastCd = 3.2;
          sound.playSadTrombone();
          showToast(FLAT_ROASTS[Math.floor(Math.random() * FLAT_ROASTS.length)], 'roast');
        }
      }

      function stopDrinking(checkGolden = true) {
        if (!state.isDrinking) return;

        const curFizz = state.fizz;
        const d = DRINKS[state.activeDrink];
        const b = state.bottles[state.activeDrink];

        state.isDrinking = false;
        state.activePointerId = null;
        sound.stopFizz();

        if (checkGolden && state.phase === 'playing' && state.stunTimer <= 0) {
          if (curFizz >= 70 && curFizz <= 90) {
            if (!d.canAscend) return;
            if (b.temp > 18.5) {
              showToast('ВАСЬ, ТЁПЛАЯ! КИНЬ В МОРОЗИЛКУ 🥵', 'danger');
            } else {
              triggerGoldenSip();
            }
          }
        }
      }

      function triggerGoldenSip() {
        state.goldenSips++;
        records.totalGoldenSips++;
        saveRecords();

        const rect = dom.bottleWrap.getBoundingClientRect();
        const isCombo = state.comboWindowTimer > 0;

        if (isCombo) {
          state.combos++;
          state.comboWindowTimer = 0;
          state.ascensionTimer = 4.0;
          state.thirst = Math.max(0, state.thirst - 12);
          state.noise = Math.max(0, state.noise - 18);
          state.score += 500;
          sound.playAscension(true);
          spawnParticles('gold', rect.left + rect.width / 2, rect.top + rect.height * 0.4, 28);
          showToast('⚡ ВАСЬ, ЭТО ИМБА! КОМБО ХРУМКА + ГАЗЯВА (+500)', 'combo');
        } else {
          state.ascensionTimer = 3.0;
          state.thirst = Math.max(0, state.thirst - 8);
          state.noise = Math.max(0, state.noise - 6);
          state.score += 220;
          sound.playAscension(false);
          spawnParticles('gold', rect.left + rect.width / 2, rect.top + rect.height * 0.4, 16);
          showToast(ASCENSION_QUOTES[Math.floor(Math.random() * ASCENSION_QUOTES.length)], 'gold');
        }

        state.fizz = Math.max(0, state.fizz - 32);
      }

      // МЕХАНИКА ХРУМКИ (СНЕКИ)
      function eatHrumka() {
        sound.unlock();
        if (state.phase !== 'playing' || state.ev.active) return;

        // Хрустнуть прямо во время кашля нельзя
        if (state.stunTimer > 0) return;

        const flavor = HRUMKA_FLAVORS[state.flavorIdx];
        state.flavorIdx = (state.flavorIdx + 1) % HRUMKA_FLAVORS.length;
        dom.hrumkaFlavor.textContent = HRUMKA_FLAVORS[state.flavorIdx];

        state.hrumkasEaten++;
        records.totalHrumkas++;
        saveRecords();

        state.sugarDebt = Math.max(0, state.sugarDebt - 28);
        state.thirst = Math.min(99.5, state.thirst + 13);
        addNoise(16);
        state.score += 150;
        state.comboWindowTimer = 4.5;

        sound.playCrunch();

        // Крошки вылетают прямо от кнопки Хрумки
        const r = dom.hrumkaBtn.getBoundingClientRect();
        spawnParticles('crumb', r.left + r.width / 2, r.top + r.height / 2, 18);

        dom.app.classList.remove('state-crunch-bump');
        void dom.app.offsetWidth;
        dom.app.classList.add('state-crunch-bump');

        showToast(`🥨 ХРУМ-ХРУМ! ${flavor}! ЛОВИ ЗОЛОТОЙ ГЛОТОК!`, 'crunch');
      }

      // МОРОЗИЛКА (ВКЛ/ВЫКЛ АКТИВНОГО ОХЛАЖДЕНИЯ)
      function toggleFreezer() {
        sound.unlock();
        if (state.phase !== 'playing' || state.ev.active) return;
        const b = state.bottles[state.activeDrink];
        if (b.frozenTaps > 0) {
          showToast('ВАСЬ, ОНА УЖЕ ЛЁД! ТАПАЙ ПО БУТЫЛКЕ 🧊', 'info');
          return;
        }
        state.freezerActive = !state.freezerActive;
        if (state.freezerActive) {
          stopDrinking(false);
          sound.startFreezerHum();
          showToast('❄️ В МОРОЗИЛКЕ! СЛЕДИ, ЧТОБ НЕ ЗАМЁРЗЛА!', 'info');
        } else {
          sound.stopFreezerHum();
          sound.playClick();
        }
      }

      function selectDrink(drinkId) {
        sound.unlock();
        if (state.phase !== 'playing' || state.ev.active) return;
        if (state.activeDrink === drinkId) return;

        stopDrinking(false);
        if (state.freezerActive) {
          state.freezerActive = false;
          sound.stopFreezerHum();
        }

        state.activeDrink = drinkId;
        state.energySipAccum = 0;
        sound.playClick();
        applyBottleVisuals();

        if (drinkId === 'brine') {
          showToast(`🥒 БАБУШКИН РАССОЛ: ОСТАЛОСЬ ${state.brineSipsLeft} ГЛОТКА`, 'vhs');
        } else if (drinkId === 'energy') {
          showToast('⚡ ЭНЕРГОС 03:17! УСКОРЯЕТ НОЧЬ, НО ПУЛЬС БЕШЕНЫЙ', 'danger');
        } else if (drinkId === 'flat') {
          showToast('🫠 СЕРЬЁЗНО, ВАСЬ? ТЁПЛАЯ ВЫДОХШАЯСЯ?..', 'roast');
        }
      }

      function addNoise(amount) {
        state.noise = Math.min(100, state.noise + amount * state.mode.noiseGainMult);
        if (state.noise >= 100 && state.phase === 'playing') {
          endGame('woke_building');
        }
      }

function endGame(endingId, customNoEndingWin = false) {
        if (state.phase === 'ended') return;
        stopDrinking(false);
        sound.stopFreezerHum();
        closeEventModal();
        state.phase = 'ended';
        state.freezerActive = false;

        dom.fxStun.classList.remove('active');
        dom.fxAscension.classList.remove('active');
        dom.fxVhs.classList.remove('active');
        dom.fxHeartbeat.classList.remove('active');
        dom.app.className = '';

        // Обновление рекордов
        const finalScore = Math.round(state.score);
        if (finalScore > records.bestScore) records.bestScore = finalScore;
        if (state.realElapsed > records.bestTimeSec) records.bestTimeSec = state.realElapsed;

        let title, emoji, badge, badgeClass, desc, isWin;

        if (customNoEndingWin) {
          // Игрок дожил до 07:00 в Классике, но набрал <5 золотых глотков и <3 комбо
          isWin = true;
          emoji = '🌅';
          badge = 'ПОБЕДА (БАЗОВАЯ)';
          badgeClass = 'win';
          title = 'ДОЖИЛ ДО 07:00!';
          desc = `Вась, ты пережил ночь! Но для концовки «Вознесение Вася» нужно 5+ золотых глотков (сейчас ${state.goldenSips}/5), а для «Короля Хрумки» — 3+ комбо (${state.combos}/3).`;
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
          : formatClock(state.nightProgress, state.mode);

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
          clockStr,
          score: finalScore,
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

        // Проверка победы по достижению рассвета!
        if (state.nightProgress >= state.mode.durationSec) {
          if (state.mode.id === 'hardcore') {
            endGame('hardcore_immortal');
          } else if (state.combos >= 3) {
            endGame('hrumka_king');
          } else if (state.goldenSips >= 5) {
            endGame('ascension');
          } else {
            endGame(null, true);
          }
          return;
        }

        // Ночной рейд к холодильнику в середине ночи (~04:30)
        if (!state.raidTriggered && state.nightProgress >= state.mode.raidAt && state.stunTimer <= 0) {
          state.raidTriggered = true;
          openNightEvent('raid');
          return;
        }

        // Периодические ночные мини-ивенты
        if (state.realElapsed >= state.nextEventAt && state.nightProgress < state.mode.durationSec - 10 && state.stunTimer <= 0) {
          state.nextEventAt = state.realElapsed + state.mode.eventInterval;
          triggerNextNightEvent();
          return;
        }

        // Смена постироничной строки статуса
        state.tickerTimer -= dt;
        if (state.tickerTimer <= 0) {
          state.tickerTimer = 5.5;
          state.tickerIdx = (state.tickerIdx + 1) % TICKER_QUOTES.length;
          dom.memeTicker.textContent = TICKER_QUOTES[state.tickerIdx];
        }

        // Таймеры баффов / дебаффов
        if (state.ascensionTimer > 0) state.ascensionTimer = Math.max(0, state.ascensionTimer - dt);
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
        const noiseCooling = state.vhsTimer > 0 ? state.mode.noiseDecay * 2.1 : state.mode.noiseDecay;
        state.noise = Math.max(0, state.noise - noiseCooling * dt);

        // Температура активной бутылки и работа Морозилки
        const d = DRINKS[state.activeDrink];
        const b = state.bottles[state.activeDrink];

        if (state.freezerActive && b.frozenTaps === 0) {
          // Быстрое охлаждение в морозилке
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

        // Остальные бутылки на столе греются медленно
        Object.keys(state.bottles).forEach((k) => {
          if (k !== state.activeDrink && state.bottles[k].frozenTaps === 0) {
            state.bottles[k].temp = Math.min(24, state.bottles[k].temp + 0.22 * state.mode.heatMult * dt);
          }
        });

        // Рост Сушняка (зависит от режима и Сахарного долга 0–100% -> 1.0x..2.6x)
        const sugarMult = 1.0 + (state.sugarDebt / 100) * 1.65;
        let thirstClimb = (5.4 + (state.nightProgress / state.mode.durationSec) * 1.5) * state.mode.thirstMult * sugarMult;
        if (state.ascensionTimer > 0) {
          thirstClimb = 0; // Вознесение полностью замораживает Сушняк
        }
        state.thirst += thirstClimb * dt;

        // Процесс питья при удержании бутылки
        if (state.isDrinking && state.stunTimer <= 0 && !state.freezerActive) {
          let tempEff = 1.0;
          if (b.temp > 18.5) tempEff = 0.36;
          else if (b.temp > 11.5) tempEff = 0.74;

          state.thirst = Math.max(0, state.thirst - d.thirstDrop * tempEff * dt);

          // Рост газов (у Энергоса дёрганый пульсирующий рост)
          const jitter = state.activeDrink === 'energy' ? (0.85 + Math.random() * 0.45) : 1.0;
          state.fizz += d.fizzRate * (b.temp <= 11 ? 1.0 : 0.78) * jitter * dt;

          // Рост Сахарного долга
          state.sugarDebt = Math.min(100, state.sugarDebt + d.sugarAdd * dt);

          // Визуальное убывание и автодолив уровня жидкости
          b.liquidLevel -= 0.17 * dt;
          if (b.liquidLevel < 0.2) b.liquidLevel = 0.9;

          // Периодический звук глотка
          state.gulpTick += dt;
          if (state.gulpTick >= 0.48) {
            state.gulpTick = 0;
            sound.playGulp();
          }

          // Спецэффекты конкретных напитков
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
              state.energySipAccum = -99; // 1 зачётный глоток за удержание
              state.energySips++;
              showToast(`⚡ ЭНЕРГОС #${state.energySips}/5! ПУЛЬС 190 УД/МИН!`, 'danger');
              if (state.energySips >= 5) {
                endGame('energy_astral');
                return;
              }
            }
          }

          sound.updateFizz(state.fizz, state.activeDrink === 'flat');

          // Пережал газы до 100% -> оглушение кашлем и +шум!
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

        // Проверка поражения при Сушняке 100%
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
    updateHUD(ts / 1000);
  }
  requestAnimationFrame(loop);
}

function bindEvents() {
        window.addEventListener('contextmenu', (e) => e.preventDefault());
        window.addEventListener('selectstart', (e) => e.preventDefault());
        window.addEventListener('resize', resizeCanvas);

        // Зажатие бутылки (touch-action: none + PointerCapture)
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

        window.addEventListener('blur', () => {
          stopDrinking(false);
          sound.stopFreezerHum();
        });
        document.addEventListener('visibilitychange', () => {
          if (document.hidden) {
            stopDrinking(false);
            sound.stopFreezerHum();
          }
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

        // 6 напитков (click позволяет плавно свайпать карусель на iPhone без случайного выбора)
        dom.drinkCards.forEach((btn) => {
          btn.addEventListener('click', () => selectDrink(btn.dataset.drink));
        });

        // Искушение водой из-под крана
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

        // Выбор режима на стартовом экране
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

        // Старт, рестарт, меню, копирование, альбом
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
    if (e.code === 'Escape') {
      closeAlbumModal();
      return;
    }

    // Не перехватываем клавиши, если игра не в активной фазе или открыт модальный ночной ивент
    if (state.phase !== 'playing' || state.ev.active) return;

    if (e.code === 'Space') {
      e.preventDefault();
      if (e.repeat) return;
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
    }
  });

  window.addEventListener('blur', () => {
    if (state.isDrinking) {
      stopDrinking(false);
    }
  });
}

function init() {
  initDom();
  initCanvas(dom.fxCanvas);
  initSvgBubbles(dom.svgBubbles);
  initEvents({
    dom,
    showToast,
    addNoise,
    stopDrinking,
    endGame
  });
  bindEvents();
  setupKeyboardControls();
  refreshRecordsUI();
  requestAnimationFrame(loop);
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', init);
} else {
  init();
}
