/* ==========================================================================
   КОНФИГУРАЦИЯ РЕЖИМОВ, АКТОВ НОЧИ, НАПИТКОВ, ХРУМКИ, МАГАЗИНА И 12 КОНЦОВОК
   ========================================================================== */
export const STORAGE_KEY = 'gazyava_v2_vasya_records';

export const MODES = {
  tutorial: {
    id: 'tutorial',
    name: '🎓 Обучение',
    shortTag: '07:00',
    startMin: 3 * 60 + 17,
    endMin: 7 * 60 + 0,
    durationSec: 210,      // 3 min 30 sec — experiences all 4 Acts
    thirstMult: 0.84,      // Slightly gentler than Classic, still engaging
    heatMult: 0.9,
    noiseGainMult: 0.85,
    noiseDecay: 5.2,
    eventInterval: 30,
    raidAt: 95
  },
  classic: {
    id: 'classic',
    name: '03:17. Классика',
    shortTag: '07:00',
    startMin: 3 * 60 + 17, // 03:17
    endMin: 7 * 60 + 0,    // 07:00
    durationSec: 270,      // 4 мин 30 сек
    thirstMult: 1.0,
    heatMult: 1.0,
    noiseGainMult: 1.0,
    noiseDecay: 4.5,
    eventInterval: 26,
    raidAt: 120            // ~04:55
  },
  hardcore: {
    id: 'hardcore',
    name: '🔥 Похмелье 1 января',
    shortTag: '08:00',
    startMin: 2 * 60 + 0,  // 02:00
    endMin: 8 * 60 + 0,    // 08:00
    durationSec: 390,      // 6 мин 30 сек
    thirstMult: 1.32,
    heatMult: 1.3,
    noiseGainMult: 1.22,
    noiseDecay: 3.1,
    eventInterval: 20,
    raidAt: 175
  }
};

export const ACTS = [
  {
    index: 1,
    name: 'АКТ I: ОТРИЦАНИЕ',
    shortTag: 'АКТ I',
    banner: 'АКТ I • ОТРИЦАНИЕ — ГАЗЯВА НА СТОЛЕ!',
    thirstBase: 3.6,
    carbDrainMult: 1.0,
    eventFreqMult: 1.0,
    noiseDecayMult: 1.0,
    blackout: false,
    dawn: false
  },
  {
    index: 2,
    name: 'АКТ II: ВЫДЫХАНИЕ',
    shortTag: 'АКТ II',
    banner: 'АКТ II • ВЫДЫХАНИЕ — ПУЗЫРЬКИ УХОДЯТ!',
    thirstBase: 4.15,
    carbDrainMult: 1.75,
    eventFreqMult: 0.92,
    noiseDecayMult: 0.95,
    blackout: false,
    dawn: false
  },
  {
    index: 3,
    name: 'АКТ III: КРИЗИС 5 УТРА',
    shortTag: 'АКТ III ⚡',
    banner: 'АКТ III • КРИЗИС 5 УТРА — СВЕТ ПОГАС, ВАСЬ!',
    thirstBase: 4.75,
    carbDrainMult: 1.9,
    eventFreqMult: 0.78,
    noiseDecayMult: 0.9,
    blackout: true,
    dawn: false
  },
  {
    index: 4,
    name: 'АКТ IV: ДО БУДИЛЬНИКА',
    shortTag: 'АКТ IV 🌅',
    banner: 'АКТ IV • ДО БУДИЛЬНИКА — СКОРО РАССВЕТ!',
    thirstBase: 5.35,
    carbDrainMult: 2.0,
    eventFreqMult: 0.72,
    noiseDecayMult: 0.8,
    blackout: false,
    dawn: true
  }
];

export const HRUMKA_FLAVORS = [
  'Кирики с холодцом',
  'Чипсоны с крабом',
  'Сырные хрумки',
  'Сухарики с чесноком'
];

export const DRINKS = {
  cola: {
    id: 'cola',
    name: 'Кола 2007 (Ледяная)',
    shortName: 'Кола 2007',
    thirstDrop: 36,
    fizzRate: 40,
    sugarAdd: 28,
    warmRate: 1.15,
    initTemp: 4,
    initCarb: 100,
    canAscend: true,
    colors: { top: '#8c3b12', bot: '#240902', label: '#dc2626', cap: '#ef4444', sub: '#fef08a' },
    label: 'КОЛА',
    sub: '2007 • 03:17'
  },
  mineral: {
    id: 'mineral',
    name: 'Ядрёная минералка',
    shortName: 'Минералка',
    thirstDrop: 24,
    fizzRate: 80,
    sugarAdd: 0,
    warmRate: 0.95,
    initTemp: 3,
    initCarb: 100,
    canAscend: true,
    colors: { top: '#99f6e4', bot: '#0284c7', label: '#0369a1', cap: '#38bdf8', sub: '#e0f2fe' },
    label: 'ГАЗЯВА',
    sub: '№17 ЯДРЁНАЯ'
  },
  duchess: {
    id: 'duchess',
    name: 'Дюшес из ларька',
    shortName: 'Дюшес',
    thirstDrop: 19,
    fizzRate: 34,
    sugarAdd: 11,
    warmRate: 1.0,
    initTemp: 5,
    initCarb: 100,
    canAscend: true,
    colors: { top: '#d9f99d', bot: '#65a30d', label: '#4d7c0f', cap: '#84cc16', sub: '#fef9c3' },
    label: 'ДЮШЕС',
    sub: 'ЛАРЁК 2006'
  },
  energy: {
    id: 'energy',
    name: 'Энергос «МОНСТР 03:17»',
    shortName: 'Энергос',
    thirstDrop: 29,
    fizzRate: 54,
    sugarAdd: 30,
    warmRate: 1.25,
    initTemp: 4,
    initCarb: 100,
    canAscend: true,
    colors: { top: '#fb7185', bot: '#881337', label: '#18181b', cap: '#f43f5e', sub: '#fb7185' },
    label: 'МОНСТР',
    sub: '03:17 • 500МГ'
  },
  brine: {
    id: 'brine',
    name: 'Бабушкин Рассол',
    shortName: 'Рассол',
    thirstDrop: 0,
    fizzRate: 0,
    sugarAdd: 0,
    warmRate: 0.6,
    initTemp: 5,
    initCarb: 100,
    canAscend: false,
    colors: { top: '#bef264', bot: '#3f6212', label: '#365314', cap: '#a3e635', sub: '#ecfccb' },
    label: 'РАССОЛ',
    sub: 'ОТ БАБУШКИ'
  },
  flat: {
    id: 'flat',
    name: 'Тёплая выдохшаяся',
    shortName: 'Выдохлась',
    thirstDrop: 4.5,
    fizzRate: 6,
    sugarAdd: 8,
    warmRate: 0.3,
    initTemp: 23,
    initCarb: 12,
    canAscend: false,
    colors: { top: '#85806f', bot: '#444034', label: '#52525b', cap: '#71717a', sub: '#d4d4d8' },
    label: 'ВЫДОХЛАСЬ',
    sub: 'СТОЯЛА 3 ДНЯ'
  }
};

export const SHOP_ITEMS = [
  {
    id: 'ice',
    name: 'Горсть льда',
    cost: 3,
    emoji: '🧊',
    desc: 'Остужает бутылку до 2°C и мгновенно сбивает глыбу льда.'
  },
  {
    id: 'mint',
    name: 'Жвачка «Ледяная Мята»',
    cost: 4,
    emoji: '🍬',
    desc: 'Морозит сушняк на 12с + даёт 1 Дыхание Дракона (2.1x газ!).'
  },
  {
    id: 'fresh',
    name: 'Свежая 2-литровка',
    cost: 5,
    emoji: '🧴',
    desc: 'Возвращает 100% газов, охлаждает до 3°C и доливает бутылку.'
  },
  {
    id: 'silencer',
    name: 'Подушка-глушитель',
    cost: 4,
    emoji: '🔇',
    desc: 'Режет любой шум вдвое и ускоряет затухание шума в 2 раза на 20с.'
  }
];

export const PHONE_NOTIFICATIONS = [
  { id: 'mama', sender: 'Мама', icon: '💬', text: 'Вась, ты чего там на кухне шуршишь в 4 утра?' },
  { id: 'dimon', sender: 'Димон', icon: '✈️', text: 'спишь? скинь срочно мем про газяву и сушняк' },
  { id: 'alarm', sender: 'Будильник', icon: '⏰', text: 'До подъёма осталось 2 ч 13 мин. Удачи, легенда.' },
  { id: 'kidneys', sender: 'Почки', icon: '🫀', text: 'Официальное обращение: пожалуйста, просто выпей воды.' },
  { id: 'fridge_lg', sender: 'Холодильник LG', icon: '❄️', text: 'Закрой меня, я уже намотал 400₽ за свет.' },
  { id: 'neighbor', sender: 'Сосед сверху', icon: '🔨', text: 'Брат, перфоратор на 08:00 уже прогревается.' },
  { id: 'conscience', sender: 'Совесть-бот', icon: '🤖', text: 'Ты реально хрустишь кириками с холодцом посреди ночи?' },
  { id: 'delivery', sender: 'Доставка 24/7', icon: '🛵', text: 'Курьер везёт ещё 2 литра колы (шутка, страдай сам).' },
  { id: 'cat', sender: 'Кот Барсик', icon: '🐈', text: 'МЯУ. (Перевод: открывай вискас, иначе я уроню кружку).' },
  { id: 'dad', sender: 'Батя', icon: '👨', text: 'Мне показалось или кто-то пшикнул крышкой?..' },
  { id: 'brain', sender: 'Мозг Вася', icon: '🧠', text: 'Спать? Не, давай вспомним кринж из 7 класса и попьём газявы.' },
  { id: 'weather', sender: 'Погода', icon: '🏜️', text: 'Во рту Вася: +48°C, влажность 0%, песчаная буря.' },
  { id: 'conscience_shame', sender: 'Совесть-бот', icon: '🚽', text: 'Вась, ты серьёзно НЕ СМЫЛ?! Позор на весь стояк!' },
  { id: 'cop', sender: 'Участковый', icon: '👮', text: 'Поступила жалоба на ночное бульканье в кв. 42.' }
];

export const ENDINGS = {
  ascension: {
    id: 'ascension',
    title: 'Вознесение Вася',
    emoji: '👼',
    badge: 'ИСТИННАЯ ПОБЕДА',
    badgeClass: 'win',
    hint: 'Доживи до рассвета с 8+ золотыми глотками без воды из-под крана.',
    desc: 'Вась, воистину ледяная! Ты встретил рассвет, поймав 8+ идеальных глотков. Пузырьки пересобрали твою душу на атомном уровне.'
  },
  tap_water: {
    id: 'tap_water',
    title: 'Скучный здравый смысл',
    emoji: '🚰',
    badge: 'РАЦИОНАЛЬНОСТЬ',
    badgeClass: 'meh',
    hint: 'Поддайся искушению и нажми кнопку обычной воды при сильном сушняке.',
    desc: 'Почки говорят спасибо, врачи хлопают стоя. Но вердикт суров: 0/10 уважения от пацанов, Вась.'
  },
  betrayal: {
    id: 'betrayal',
    title: 'Великое предательство',
    emoji: '🫠',
    badge: 'ТРАГЕДИЯ',
    badgeClass: 'lose',
    hint: 'Проиграй от сушняка, когда последний напиток — Тёплая выдохшаяся.',
    desc: 'В самый тёмный час Вась доверился тёплой выдохшейся газяве. Ни газов, ни холода, ни смысла. Нож в спину от пластика.'
  },
  ouroboros: {
    id: 'ouroboros',
    title: 'Сахарный Уроборос',
    emoji: '🐍',
    badge: 'САХАРНАЯ ЛОВУШКА',
    badgeClass: 'lose',
    hint: 'Проиграй от сушняка с Сахарным долгом 85% или выше.',
    desc: 'Вась, сладкое тебя переиграло! Сахарный долг пробил потолок: чем больше ты пил сладкой газявы, тем быстрее накатывал сушняк.'
  },
  kuraga: {
    id: 'kuraga',
    title: 'Официально Курага',
    emoji: '🏜️',
    badge: 'ПОРАЖЕНИЕ',
    badgeClass: 'lose',
    hint: 'Доведи шкалу Сушняка до 100%.',
    desc: 'Сушняк достиг 100%. Вся влага покинула чат, и к утру на кухне нашли идеально засушенного Вася.'
  },
  woke_building: {
    id: 'woke_building',
    title: 'Разбудил всю девятиэтажку',
    emoji: '📢',
    badge: 'ПРОВАЛ СТЕЛСА',
    badgeClass: 'lose',
    hint: 'Доведи Шум до 100% или спались Бате на кухне.',
    desc: 'Громкий хруст и ПШШШ в 3 ночи подняли на ноги Батю, соседей снизу и даже голубей на балконе. Свет на кухне зажёгся!'
  },
  hrumka_king: {
    id: 'hrumka_king',
    title: 'Король Хрумки и Газявы',
    emoji: '👑',
    badge: 'СЕКРЕТНАЯ КОНЦОВКА',
    badgeClass: 'secret',
    hint: '[СЕКРЕТ] Доживи до рассвета, сделав 5+ комбо «Хрумка + Золотой глоток».',
    desc: 'Вась, это чисто имба! Идеальный баланс солёной хрумки и ледяной газявы открыл высшую гастрономическую нирвану в 03:17.'
  },
  energy_astral: {
    id: 'energy_astral',
    title: 'Астрал Энергоса',
    emoji: '⚡',
    badge: 'СЕКРЕТНАЯ КОНЦОВКА',
    badgeClass: 'secret',
    hint: '[СЕКРЕТ] Выпей Энергос «МОНСТР 03:17» 5 раз за одну ночь.',
    desc: 'Пятый глоток энергоса вывел Вася за пределы времени. К 05:00 он починил Wi-Fi всему подъезду, перебрал карбюратор и увидел звук.'
  },
  hardcore_immortal: {
    id: 'hardcore_immortal',
    title: 'Бессмертный Первоянварский',
    emoji: '🔥',
    badge: 'ХАРДКОР-ЛЕГЕНДА',
    badgeClass: 'hardcore',
    hint: '[ХАРДКОР] Выживи до 08:00 в режиме «Похмелье 1 января».',
    desc: '08:00, 1 января. Вась выстоял против адского сушняка, вчерашнего оливье и скрипучего паркета. Легенда района!'
  },
  burp_apocalypse: {
    id: 'burp_apocalypse',
    title: 'Акустический Апокалипсис',
    emoji: '💥',
    badge: 'МЕГА-ОТРЫЖКА',
    badgeClass: 'lose',
    hint: 'Доведи Шум до 100% взрывом отрыжки без подушки.',
    desc: 'Вась не успел воткнуться лицом в подушку! Мега-отрыжка на 140 децибел выбила стёкла в подъезде, разбудила Батю и активировала сигнализации во дворе!'
  },
  shame_unflushed: {
    id: 'shame_unflushed',
    title: 'Призрак Несмытого Унитаза',
    emoji: '🚽',
    badge: 'ПОЗОРНЫЙ СТЕЛС',
    badgeClass: 'secret',
    hint: '[СЕКРЕТ] Доживи до рассвета, выбрав «Не смывать» 3+ раза за ночь.',
    desc: 'Вась пережил сушняк, но какой ценой? Утром в туалет зашёл сонный Батя... Звук его праведного крика был слышен даже в соседнем городе.'
  },
  dragon_breath: {
    id: 'dragon_breath',
    title: 'Дыхание Ледяного Дракона',
    emoji: '🐉',
    badge: 'МИФИЧЕСКАЯ КОНЦОВКА',
    badgeClass: 'secret',
    hint: '[СЕКРЕТ] Поймай 3 Золотых глотка под мятной жвачкой из ночного схрона.',
    desc: 'Мятная жвачка и ледяной газ слились воедино! Из пасти Вася вырвался абсолютный ноль. Кухня мгновенно замёрзла, а сушняк испарился навсегда!'
  }
};

export const TICKER_QUOTES = [
  'Вась, сушняк душит. Неси газяву и хрумку!',
  'Вась, не передержи бутылку в морозилке — будет глыба!',
  'Народная мудрость: Хрумка + Золотой глоток = ИМБА.',
  '03:17 на часах. Мозг требует ледяных пузырьков.',
  'Вась, тише хрусти кириками — Батя чутко спит!',
  'Обычная вода из-под крана? 0/10 уважения от пацанов.',
  'Бабушкин рассол обнуляет сахарный долг в один глоток!',
  'Дюшес из ларька переносит в тёплый 2006-й и гасит шум.',
  'Встряхивай выдохшуюся газяву, Вась! Но берегись пены!',
  'Газы распирают? Зажимай «В ПОДУШКУ», пока не бахнуло!',
  'Клапан на пределе? Крадись в туалет по половицам!',
  'Копи крышечки: ледяная жвачка превратит тебя в дракона!'
];

export const ASCENSION_QUOTES = [
  'ВОИСТИНУ ЛЕДЯНАЯ, ВАСЬ! ✨',
  'ПУЗЫРЬКИ ПРОБИЛИ ДО ПЯТОК 👼',
  'ВАСЬ, ЭТО ЧИСТО ПЕРЕЗАГРУЗКА ⚡',
  'ААААХХХ... ЖИТЬ МОЖНО 🌌',
  'СИНАПСЫ ОХЛАЖДЕНЫ ДО 0°K 💎'
];

export const FLAT_ROASTS = [
  'ВАСЬ, ЗАЧЕМ ТЫ ПЬЁШЬ ЭТУ ЛУЖУ?!',
  'КТО ОСТАВИЛ ЕЁ ОТКРЫТОЙ НА 3 ДНЯ?!',
  'ГАЗЫ ПОКИНУЛИ ЧАТ 🥀',
  'ТЁПЛАЯ ПРЕДАТЕЛЬНИЦА...'
];
