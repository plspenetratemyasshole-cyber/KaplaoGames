/* ==========================================================================
   ИНТЕРАКТИВНОЕ ОБУЧЕНИЕ («🎓 ОБУЧЕНИЕ») — ТРЕНЕР, СЛОУ-МО И ПОДСВЕТКА (TIER 3)
   ========================================================================== */
import { DRINKS } from './config.js';
import { state } from './state.js';
import { sound } from './audio.js';

let dom = null;
let showToast = () => {};

export const TUTORIAL_STEPS = [
  {
    id: 'gold_sip',
    targetSelector: '#bottleZone',
    actionId: 'golden_sip',
    title: '✨ ЗОЛОТАЯ ЗОНА',
    text: 'Зажми бутылку и отпусти на 70–90% — заморозишь сушняк и получишь 🪙!',
    trigger: () => state.realElapsed >= 1.8
  },
  {
    id: 'hrumka',
    targetSelector: '#hrumkaBtn',
    actionId: 'hrumka',
    title: '🥨 САХАРНЫЙ ДОЛГ',
    text: 'Сахар ускоряет сушняк! Тапни Хрумку — сбивает сахар и даёт окно для КОМБО.',
    trigger: () => state.sugarDebt >= 24
  },
  {
    id: 'freezer',
    targetSelector: '#freezerBtn',
    actionId: 'freezer',
    title: '❄️ ГАЗЯВА НАГРЕЛАСЬ',
    text: 'Вруби Морозилку! Но выключи до 0°C, иначе бутылка станет глыбой льда.',
    trigger: () => {
      const b = state.bottles[state.activeDrink];
      return b && b.temp >= 12.5 && b.frozenTaps === 0;
    }
  },
  {
    id: 'shop',
    targetSelector: '#shopBtn',
    actionId: 'shop',
    title: '🛒 НОЧНОЙ СХРОН',
    text: 'Есть крышечки 🪙! Открой Схрон — там лёд, жвачка-дракон и глушитель шума.',
    trigger: () => state.caps >= 4 && state.realElapsed >= 16
  },
  {
    id: 'drinks',
    targetSelector: '#drinkDock',
    actionId: 'drink_switch',
    title: '🏪 ЛАРЁК НАПИТКОВ',
    text: 'Свайпай внизу! Минералка без сахара, Дюшес гасит шум, Рассол обнуляет сахар.',
    trigger: () => state.realElapsed >= 30 && !state.tutorial.hasSwitchedDrink
  },
  {
    id: 'burp',
    targetSelector: '#burpBtn',
    actionId: 'burp_vent',
    title: '💨 ГАЗЫ РАСПИРАЮТ',
    text: 'Зажми «В подушку», пока не дошло до 100% — иначе Мега-отрыжка (+45 шума)!',
    trigger: () => state.burp >= 46
  },
  {
    id: 'shake',
    targetSelector: '#shakeBtn',
    actionId: 'shake',
    title: '🫧 ГАЗЯВА ВЫДЫХАЕТСЯ',
    text: 'Тапни «Встряхнуть» — вернёшь газы (осторожно: на 4.5с пойдёт быстрая пена!).',
    trigger: () => {
      const d = DRINKS[state.activeDrink];
      const b = state.bottles[state.activeDrink];
      return d && d.canAscend && b && b.carb <= 66 && b.frozenTaps === 0 && !state.freezerActive;
    }
  },
  {
    id: 'toilet',
    targetSelector: '#toiletBtn',
    actionId: 'toilet',
    title: '🚽 КЛАПАН ПОЛНЫЙ',
    text: 'На 80%+ затрясутся руки! Сгоняй в Туалет по стелсу (там ждёт выбор смыва).',
    trigger: () => state.bladder >= 45
  }
];

export function initTutorial(deps) {
  dom = deps.dom;
  showToast = deps.showToast || (() => {});

  if (dom.tutCoachSkip) {
    dom.tutCoachSkip.addEventListener('click', (e) => {
      e.stopPropagation();
      skipActiveTutorialStep();
    });
  }
}

export function resetTutorial() {
  state.tutorial.enabled = true;
  state.tutorial.activeStep = null;
  state.tutorial.completed = [];
  state.tutorial.cooldown = 1.0;
  state.tutorial.elapsedSinceHint = 0;
  state.tutorial.hasSwitchedDrink = false;
  state.timeScale = 1.0;

  if (dom && dom.tutCoach) {
    dom.tutCoach.style.display = 'none';
  }
  document.querySelectorAll('.tut-highlight').forEach((el) => {
    el.classList.remove('tut-highlight');
  });
}

function activateStep(step) {
  state.tutorial.activeStep = step;
  state.tutorial.elapsedSinceHint = 0;

  if (dom && dom.tutCoach) {
    if (dom.tutCoachTitle) dom.tutCoachTitle.textContent = step.title;
    if (dom.tutCoachText) dom.tutCoachText.textContent = step.text;
    dom.tutCoach.style.display = 'block';
  }

  // Снимаем старую подсветку и ставим новую
  document.querySelectorAll('.tut-highlight').forEach((el) => {
    el.classList.remove('tut-highlight');
  });

  const targetEl = document.querySelector(step.targetSelector);
  if (targetEl) {
    targetEl.classList.add('tut-highlight');
  }
}

function completeStep(stepId, showSuccessToast = true) {
  if (!state.tutorial.completed.includes(stepId)) {
    state.tutorial.completed.push(stepId);
  }
  state.tutorial.activeStep = null;
  state.tutorial.cooldown = 3.5;
  state.tutorial.elapsedSinceHint = 0;

  if (dom && dom.tutCoach) {
    dom.tutCoach.style.display = 'none';
  }
  document.querySelectorAll('.tut-highlight').forEach((el) => {
    el.classList.remove('tut-highlight');
  });

  if (showSuccessToast) {
    sound.unlock();
    sound.playClick();
    showToast('✅ ПОНЯЛ, ВАСЬ!', 'vhs');
  }
}

export function skipActiveTutorialStep() {
  if (!state.tutorial.activeStep) return;
  const stepId = state.tutorial.activeStep.id;
  completeStep(stepId, false);
  sound.unlock();
  sound.playClick();
}

export function notifyTutorialAction(actionId) {
  if (!state.tutorial.enabled || !state.tutorial.activeStep) return;
  if (state.tutorial.activeStep.actionId === actionId) {
    completeStep(state.tutorial.activeStep.id, true);
  }
}

export function updateTutorial(dtReal) {
  if (!state.tutorial.enabled || state.phase !== 'playing' || state.ev.active || state.shopOpen) {
    return;
  }

  if (state.tutorial.activeStep) {
    const step = state.tutorial.activeStep;
    state.tutorial.elapsedSinceHint += dtReal;

    // В шаге с золотым глотком во время питья даём 0.62, чтобы держать бутылку было отзывчиво; иначе 0.22
    const targetScale = (step.id === 'gold_sip' && state.isDrinking) ? 0.62 : 0.22;
    state.timeScale += (targetScale - state.timeScale) * Math.min(1, dtReal * 7.5);

    // Авто-завершение шага золотого глотка через 14 сек реального времени, если игрок не сделал глоток
    if (step.id === 'gold_sip' && state.tutorial.elapsedSinceHint >= 14) {
      completeStep(step.id, false);
      return;
    }
  } else {
    // Плавно восстанавливаем нормальное время
    if (state.timeScale < 1.0) {
      state.timeScale += (1.0 - state.timeScale) * Math.min(1, dtReal * 7.5);
      if (Math.abs(state.timeScale - 1.0) < 0.01) {
        state.timeScale = 1.0;
      }
    }

    if (state.tutorial.cooldown > 0) {
      state.tutorial.cooldown = Math.max(0, state.tutorial.cooldown - dtReal);
      return;
    }

    // Ищем следующий подходящий шаг
    for (let i = 0; i < TUTORIAL_STEPS.length; i++) {
      const s = TUTORIAL_STEPS[i];
      if (!state.tutorial.completed.includes(s.id) && s.trigger()) {
        activateStep(s);
        break;
      }
    }
  }
}
