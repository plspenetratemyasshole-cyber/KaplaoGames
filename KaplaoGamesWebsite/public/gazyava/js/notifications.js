/* ==========================================================================
   ФЕЙКОВЫЕ НОЧНЫЕ PUSH-УВЕДОМЛЕНИЯ ВАСЯ (TIER 3)
   ========================================================================== */
import { PHONE_NOTIFICATIONS } from './config.js';
import { sound } from './audio.js';
import { state } from './state.js';

let dom = null;
let showToast = () => {};
let notifTimeout = null;
let notifIdx = 0;

export function initNotifications(deps) {
  dom = deps.dom;
  showToast = deps.showToast || (() => {});

  if (dom.phonePush) {
    dom.phonePush.addEventListener('click', () => {
      dismissNotification(true);
    });
  }
}

export function pushPhoneNotif(sender, appIcon, text) {
  if (!dom || !dom.phonePush) return;
  sound.unlock();
  sound.playPhonePing();

  if (notifTimeout) {
    clearTimeout(notifTimeout);
    notifTimeout = null;
  }

  if (dom.pushIcon) dom.pushIcon.textContent = appIcon || '💬';
  if (dom.pushSender) dom.pushSender.textContent = sender || 'Неизвестный';
  if (dom.pushText) dom.pushText.textContent = text || '...';

  dom.phonePush.classList.add('visible');

  notifTimeout = setTimeout(() => {
    dismissNotification(false);
  }, 4600);
}

export function dismissNotification(byUser = false) {
  if (!dom || !dom.phonePush) return;
  if (notifTimeout) {
    clearTimeout(notifTimeout);
    notifTimeout = null;
  }

  dom.phonePush.classList.remove('visible');

  if (byUser && state.phase === 'playing') {
    sound.unlock();
    sound.playClick();
    state.score += 50;
    showToast('📱 СМАХНУЛ УВЕДОМЛЕНИЕ (+50 ОЧКОВ)', 'info');
  }
}

export function updateNotifications(dt) {
  if (state.phase !== 'playing' || state.ev.active || state.shopOpen) return;

  state.lastNotifTimer += dt;
  if (state.lastNotifTimer >= state.nextNotifAt) {
    state.lastNotifTimer = 0;
    state.nextNotifAt = 24 + Math.random() * 14; // каждые 24..38 секунд

    const pool = PHONE_NOTIFICATIONS;
    const item = pool[notifIdx % pool.length];
    notifIdx++;

    pushPhoneNotif(item.sender, item.icon, item.text);
  }
}
