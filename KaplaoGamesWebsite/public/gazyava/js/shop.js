/* ==========================================================================
   МАГАЗИН НОЧНОГО СХРОНА (TIER 3: КРЫШЕЧКИ, БАФФЫ И РЕСУРСЫ)
   ========================================================================== */
import { SHOP_ITEMS, DRINKS } from './config.js';
import { state } from './state.js';
import { sound } from './audio.js';
import { spawnParticles } from './particles.js';

let dom = null;
let showToast = () => {};
let addNoise = () => {};
let stopDrinking = () => {};
let applyBottleVisuals = () => {};
let notifyTutorialAction = () => {};

export function initShop(deps) {
  dom = deps.dom;
  showToast = deps.showToast || (() => {});
  addNoise = deps.addNoise || (() => {});
  stopDrinking = deps.stopDrinking || (() => {});
  applyBottleVisuals = deps.applyBottleVisuals || (() => {});
  notifyTutorialAction = deps.notifyTutorialAction || (() => {});

  if (dom.shopBtn) {
    dom.shopBtn.addEventListener('click', openShopModal);
  }
  if (dom.closeShopBtn) {
    dom.closeShopBtn.addEventListener('click', closeShopModal);
  }
  if (dom.shopOkBtn) {
    dom.shopOkBtn.addEventListener('click', closeShopModal);
  }
}

export function canAffordAnyItem() {
  return SHOP_ITEMS.some((item) => state.caps >= item.cost);
}

export function openShopModal() {
  if (state.phase !== 'playing' || state.ev.active) return;
  sound.unlock();
  sound.playClick();

  state.shopOpen = true;
  notifyTutorialAction('shop');

  // Безопасная остановка всех активных действий
  stopDrinking(false);
  state.isVentingBurp = false;
  sound.stopVentSound();
  if (state.freezerActive) {
    state.freezerActive = false;
    sound.stopFreezerHum();
  }

  renderShopItems();
  dom.shopModal.classList.add('open');
}

export function closeShopModal() {
  if (!state.shopOpen) return;
  sound.unlock();
  sound.playClick();
  state.shopOpen = false;
  dom.shopModal.classList.remove('open');
}

export function renderShopItems() {
  if (!dom || !dom.shopList) return;

  if (dom.shopCapsVal) {
    dom.shopCapsVal.textContent = String(state.caps);
  }

  dom.shopList.innerHTML = '';

  SHOP_ITEMS.forEach((item) => {
    const canBuy = state.caps >= item.cost;
    const card = document.createElement('div');
    card.className = `shop-item ${canBuy ? 'affordable' : 'locked'}`;

    card.innerHTML = `
      <div class="shop-item-left">
        <span class="shop-item-emoji">${item.emoji}</span>
        <div class="shop-item-info">
          <div class="shop-item-name">${item.name}</div>
          <div class="shop-item-desc">${item.desc}</div>
        </div>
      </div>
      <button class="shop-buy-btn ${canBuy ? 'active' : 'disabled'}" type="button" data-id="${item.id}">
        ${item.cost} 🪙
      </button>
    `;

    const btn = card.querySelector('.shop-buy-btn');
    btn.addEventListener('click', (e) => {
      e.stopPropagation();
      buyShopItem(item.id);
    });

    dom.shopList.appendChild(card);
  });
}

export function buyShopItem(itemId) {
  const item = SHOP_ITEMS.find((it) => it.id === itemId);
  if (!item) return;

  if (item.id === 'fresh' && state.activeDrink === 'flat') {
    sound.unlock();
    sound.playSadTrombone();
    showToast('🫠 ЭТУ ЛУЖУ УЖЕ НЕ СПАСТИ! ВЫБЕРИ НОРМ ГАЗЯВУ!', 'roast');
    return;
  }

  if (state.caps < item.cost) {
    sound.unlock();
    sound.playSadTrombone();
    showToast('🪙 НЕ ХВАТАЕТ КРЫШЕЧЕК, ВАСЬ!', 'danger');
    return;
  }

  state.caps -= item.cost;
  sound.unlock();
  sound.playBuy();

  const b = state.bottles[state.activeDrink];
  const rect = dom.bottleWrap ? dom.bottleWrap.getBoundingClientRect() : null;
  const cx = rect ? rect.left + rect.width / 2 : window.innerWidth / 2;
  const cy = rect ? rect.top + rect.height / 2 : window.innerHeight / 2;

  if (item.id === 'ice') {
    b.temp = 2;
    b.frozenTaps = 0;
    sound.playIceShatter();
    spawnParticles('ice', cx, cy, 18);
    showToast('🧊 ЛЁД В БУТЫЛКЕ! ТЕМПЕРАТУРА 2°C!', 'vhs');
  } else if (item.id === 'mint') {
    state.mintTimer = 12.0;
    state.dragonBreathCharges = 1;
    spawnParticles('dragon', cx, cy, 22);
    showToast('🍬 МЯТА ЗАМОРОЗИЛА СУШНЯК! ЗАРЯЖЕНО ДЫХАНИЕ ДРАКОНА!', 'gold');
  } else if (item.id === 'fresh') {
    if (state.activeDrink === 'brine') {
      state.brineSipsLeft += 2;
      spawnParticles('ice', cx, cy, 14);
      showToast('🥒 +2 ГЛОТКА БАБУШКИНА РАССОЛА!', 'vhs');
    } else {
      b.carb = 100;
      b.temp = 3;
      b.liquidLevel = 0.92;
      b.frozenTaps = 0;
      addNoise(8);
      sound.playPssh(false);
      spawnParticles('gold', cx, cy, 16);
      showToast('🧴 СВЕЖАЯ 2-ЛИТРОВКА! 100% ГАЗА, 3°C И ПОЛНЫЙ ОБЪЁМ!', 'info');
    }
  } else if (item.id === 'silencer') {
    state.silencerTimer = 20.0;
    spawnParticles('crumb', cx, cy, 12);
    showToast('🔇 ПОДУШКА-ГЛУШИТЕЛЬ! -50% ШУМА НА 20 СЕК!', 'info');
  }

  applyBottleVisuals();
  renderShopItems();
}
