export type NickGradientStop = {
  t: number;
  hex: string;
};

export type NickVec2 = {
  x: number;
  y: number;
};

export type NickEasingKey = {
  time: number;
  value: number;
};

export type NickEffectTiming = {
  triggerWhen?: "OnStart" | "Manual";
  overrideTagEffects?: boolean;
  animationType?: "Loop" | "LoopFixedDuration" | "PingPong" | "OneTime";
  fixedDuration?: number;
  durationPerChar?: number;
  timeBetweenChars?: number;
  noDelayForChars?: boolean;
  reverseCharOrder?: boolean;
  clampBetween0And1?: boolean;
  timeType?: "ScaledTime" | "UnscaledTime";
  easing?: "linear" | "easeIn" | "easeOut" | "easeInOut";
  easingKeys?: NickEasingKey[];
};

export type NickEffect = NickEffectTiming & {
  type: "color" | "move" | "rotate" | "scale" | "composite" | "perVertex";
  // color
  colorType?: "Gradient" | "BetweenTwoColors" | "OnlyAlpha" | "ColorToOriginal" | "OriginalToColor";
  startColor?: string;
  endColor?: string;
  gradientStops?: NickGradientStop[];
  orientation?: "Horizontal" | "HorizontalPerCharacter" | "Vertical";
  stride?: number;
  startAlpha?: number;
  endAlpha?: number;
  // move
  startOffset?: NickVec2;
  endOffset?: NickVec2;
  // rotate
  centerOffset?: NickVec2;
  startAngle?: number;
  endAngle?: number;
  // scale
  startScale?: number;
  endScale?: number;
  // composite
  effects?: NickEffect[];
  // per-vertex
  topLeft?: NickEffect[];
  topRight?: NickEffect[];
  bottomLeft?: NickEffect[];
  bottomRight?: NickEffect[];
};

export type NicknamePresetStyle = {
  fontName?: string;
  baseColor?: string;
  /** TMP SDF outline color (hex). */
  outlineColor?: string;
  /** TMP outline thickness / fatness, typically 0..1. */
  outlineWidth?: number;
  effects?: NickEffect[];
};

export const EMPTY_NICKNAME_STYLE: NicknamePresetStyle = {
  fontName: "",
  baseColor: "#FFFFFF",
  outlineColor: "#000000",
  outlineWidth: 0,
  effects: [],
};

export function defaultTiming(): NickEffectTiming {
  return {
    triggerWhen: "OnStart",
    overrideTagEffects: false,
    animationType: "PingPong",
    fixedDuration: 1,
    durationPerChar: 0.5,
    timeBetweenChars: 0.05,
    noDelayForChars: false,
    reverseCharOrder: false,
    clampBetween0And1: false,
    timeType: "UnscaledTime",
    easing: "linear",
  };
}

export function createDefaultEffect(type: NickEffect["type"]): NickEffect {
  const timing = defaultTiming();
  switch (type) {
    case "color":
      return {
        ...timing,
        type: "color",
        colorType: "Gradient",
        orientation: "Horizontal",
        stride: 10,
        startColor: "#FF4D6D",
        endColor: "#4D9FFF",
        gradientStops: [
          { t: 0, hex: "#FF4D6D" },
          { t: 0.33, hex: "#FFD166" },
          { t: 0.66, hex: "#06D6A0" },
          { t: 1, hex: "#4D9FFF" },
        ],
        startAlpha: 0,
        endAlpha: 1,
      };
    case "move":
      return {
        ...timing,
        type: "move",
        startOffset: { x: 0, y: -12 },
        endOffset: { x: 0, y: 0 },
      };
    case "rotate":
      return {
        ...timing,
        type: "rotate",
        centerOffset: { x: 0, y: 0 },
        startAngle: -15,
        endAngle: 15,
      };
    case "scale":
      return {
        ...timing,
        type: "scale",
        startScale: 0.85,
        endScale: 1.1,
      };
    case "composite":
      return {
        ...timing,
        type: "composite",
        effects: [createDefaultEffect("color"), createDefaultEffect("move")],
      };
    case "perVertex":
      return {
        ...timing,
        type: "perVertex",
        topLeft: [createDefaultEffect("move")],
        topRight: [],
        bottomLeft: [],
        bottomRight: [],
      };
    default:
      return { ...timing, type: "color" };
  }
}

function asObject(value: unknown): Record<string, unknown> | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : null;
}

function asString(value: unknown, fallback = ""): string {
  return typeof value === "string" ? value : fallback;
}

function asNumber(value: unknown, fallback = 0): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function asBool(value: unknown, fallback = false): boolean {
  return typeof value === "boolean" ? value : fallback;
}

function normalizeHex(value: unknown, fallback = "#FFFFFF"): string {
  const raw = asString(value, fallback).trim();
  if (!raw) return fallback;
  const withHash = raw.startsWith("#") ? raw : `#${raw}`;
  if (/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/.test(withHash)) {
    return withHash.toUpperCase();
  }
  return fallback;
}

function normalizeVec2(value: unknown, fallback: NickVec2): NickVec2 {
  const obj = asObject(value);
  if (!obj) return { ...fallback };
  return {
    x: asNumber(obj.x, fallback.x),
    y: asNumber(obj.y, fallback.y),
  };
}

function normalizeStops(value: unknown): NickGradientStop[] {
  if (!Array.isArray(value)) return [];
  return value
    .map((item) => {
      const obj = asObject(item);
      if (!obj) return null;
      return {
        t: Math.min(1, Math.max(0, asNumber(obj.t, 0))),
        hex: normalizeHex(obj.hex, "#FFFFFF"),
      };
    })
    .filter((x): x is NickGradientStop => !!x);
}

function normalizeEffectList(value: unknown, depth: number): NickEffect[] {
  if (!Array.isArray(value) || depth > 4) return [];
  return value
    .map((item) => normalizeEffect(item, depth))
    .filter((x): x is NickEffect => !!x);
}

function normalizeEffect(value: unknown, depth = 0): NickEffect | null {
  const obj = asObject(value);
  if (!obj) return null;
  const typeRaw = asString(obj.type, "color").toLowerCase();
  const type: NickEffect["type"] =
    typeRaw === "move"
      ? "move"
      : typeRaw === "rotate"
        ? "rotate"
        : typeRaw === "scale"
          ? "scale"
          : typeRaw === "composite"
            ? "composite"
            : typeRaw === "pervertex" || typeRaw === "per_vertex" || typeRaw === "per-vertex"
              ? "perVertex"
              : "color";

  const base: NickEffect = {
    type,
    triggerWhen: asString(obj.triggerWhen, "OnStart") === "Manual" ? "Manual" : "OnStart",
    overrideTagEffects: asBool(obj.overrideTagEffects, false),
    animationType: (["Loop", "LoopFixedDuration", "PingPong", "OneTime"].includes(
      asString(obj.animationType, "PingPong")
    )
      ? asString(obj.animationType, "PingPong")
      : "PingPong") as NickEffect["animationType"],
    fixedDuration: asNumber(obj.fixedDuration, 1),
    durationPerChar: asNumber(obj.durationPerChar, 0.5),
    timeBetweenChars: asNumber(obj.timeBetweenChars, 0.05),
    noDelayForChars: asBool(obj.noDelayForChars, false),
    reverseCharOrder: asBool(obj.reverseCharOrder, false),
    clampBetween0And1: asBool(obj.clampBetween0And1, false),
    timeType: asString(obj.timeType, "UnscaledTime") === "ScaledTime" ? "ScaledTime" : "UnscaledTime",
    easing: (["linear", "easeIn", "easeOut", "easeInOut"].includes(asString(obj.easing, "linear"))
      ? asString(obj.easing, "linear")
      : "linear") as NickEffect["easing"],
  };

  if (type === "color") {
    return {
      ...base,
      colorType: (["Gradient", "BetweenTwoColors", "OnlyAlpha", "ColorToOriginal", "OriginalToColor"].includes(
        asString(obj.colorType, "Gradient")
      )
        ? asString(obj.colorType, "Gradient")
        : "Gradient") as NickEffect["colorType"],
      startColor: normalizeHex(obj.startColor, "#FFFFFF"),
      endColor: normalizeHex(obj.endColor, "#FFFFFF"),
      gradientStops: normalizeStops(obj.gradientStops),
      orientation: (["Horizontal", "HorizontalPerCharacter", "Vertical"].includes(
        asString(obj.orientation, "Horizontal")
      )
        ? asString(obj.orientation, "Horizontal")
        : "Horizontal") as NickEffect["orientation"],
      stride: Math.max(1, Math.floor(asNumber(obj.stride, 10))),
      startAlpha: Math.min(1, Math.max(0, asNumber(obj.startAlpha, 0))),
      endAlpha: Math.min(1, Math.max(0, asNumber(obj.endAlpha, 1))),
    };
  }

  if (type === "move") {
    return {
      ...base,
      startOffset: normalizeVec2(obj.startOffset, { x: 0, y: -12 }),
      endOffset: normalizeVec2(obj.endOffset, { x: 0, y: 0 }),
    };
  }

  if (type === "rotate") {
    return {
      ...base,
      centerOffset: normalizeVec2(obj.centerOffset, { x: 0, y: 0 }),
      startAngle: asNumber(obj.startAngle, -15),
      endAngle: asNumber(obj.endAngle, 15),
    };
  }

  if (type === "scale") {
    return {
      ...base,
      startScale: asNumber(obj.startScale, 0.85),
      endScale: asNumber(obj.endScale, 1.1),
    };
  }

  if (type === "composite") {
    return {
      ...base,
      effects: normalizeEffectList(obj.effects, depth + 1),
    };
  }

  return {
    ...base,
    topLeft: normalizeEffectList(obj.topLeft, depth + 1),
    topRight: normalizeEffectList(obj.topRight, depth + 1),
    bottomLeft: normalizeEffectList(obj.bottomLeft, depth + 1),
    bottomRight: normalizeEffectList(obj.bottomRight, depth + 1),
  };
}

export function normalizeNicknamePresetStyle(value: unknown): NicknamePresetStyle {
  const obj = asObject(value);
  if (!obj) return { ...EMPTY_NICKNAME_STYLE, effects: [] };

  return {
    fontName: asString(obj.fontName, "").trim(),
    baseColor: normalizeHex(obj.baseColor, "#FFFFFF"),
    outlineColor: normalizeHex(obj.outlineColor, "#000000"),
    outlineWidth: Math.min(1, Math.max(0, asNumber(obj.outlineWidth, 0))),
    effects: normalizeEffectList(obj.effects, 0),
  };
}
