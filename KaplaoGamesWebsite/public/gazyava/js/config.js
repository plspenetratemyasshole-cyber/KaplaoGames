/* ==========================================================================
   КОНФИГУРАЦИЯ РЕЖИМОВ, НАПИТКОВ, ХРУМКИ, ЦИТАТ И 9 КОНЦОВОК
   ========================================================================== */
export const STORAGE_KEY = 'gazyava_v2_vasya_records';
export const MODES = {
        classic: {
          id: 'classic',
          name: '03:17. Классика',
          shortTag: '07:00',
          startMin: 3 * 60 + 17, // 03:17
          endMin: 7 * 60 + 0,    // 07:00
          durationSec: 70,
          thirstMult: 1.0,
          heatMult: 1.0,
          noiseGainMult: 1.0,
          noiseDecay: 4.8,
          eventInterval: 16.5,
          raidAt: 33
        },
        hardcore: {
          id: 'hardcore',
          name: '🔥 Похмелье 1 января',
          shortTag: '08:00',
          startMin: 2 * 60 + 0,  // 02:00
          endMin: 8 * 60 + 0,    // 08:00
          durationSec: 84,
          thirstMult: 1.5,
          heatMult: 1.4,
          noiseGainMult: 1.28,
          noiseDecay: 3.1,
          eventInterval: 12.2,
          raidAt: 40
        }
      };
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
          sugarAdd: 28,       // % сахарного долга в секунду питья
          warmRate: 1.15,
          initTemp: 4,
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
          fizzRate: 80,       // 2x скорость пробития в нос!
          sugarAdd: 0,        // Ноль сахарного долга
          warmRate: 0.95,
          initTemp: 3,
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
          canAscend: true,
          colors: { top: '#fb7185', bot: '#881337', label: '#18181b', cap: '#f43f5e', sub: '#fb7185' },
          label: 'МОНСТР',
          sub: '03:17 • 500МГ'
        },
        brine: {
          id: 'brine',
          name: 'Бабушкин Рассол',
          shortName: 'Рассол',
          thirstDrop: 0,      // Срабатывает мгновенным глотком
          fizzRate: 0,
          sugarAdd: 0,
          warmRate: 0.6,
          initTemp: 5,
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
          canAscend: false,
          colors: { top: '#85806f', bot: '#444034', label: '#52525b', cap: '#71717a', sub: '#d4d4d8' },
          label: 'ВЫДОХЛАСЬ',
          sub: 'СТОЯЛА 3 ДНЯ'
        }
      };
export const ENDINGS = {
        ascension: {
          id: 'ascension',
          title: 'Вознесение Вася',
          emoji: '👼',
          badge: 'ИСТИННАЯ ПОБЕДА',
          badgeClass: 'win',
          hint: 'Доживи до рассвета с 5+ золотыми глотками без воды из-под крана.',
          desc: 'Вась, воистину ледяная! Ты встретил рассвет, поймав 5+ идеальных глотков. Пузырьки пересобрали твою душу на атомном уровне.'
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
          hint: '[СЕКРЕТ] Доживи до рассвета, сделав 3+ комбо «Хрумка + Золотой глоток».',
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
        'Дюшес из ларька переносит в тёплый 2006-й и гасит шум.'
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
