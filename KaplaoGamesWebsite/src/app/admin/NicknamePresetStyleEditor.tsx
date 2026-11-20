"use client";

import {
  Button,
  Card,
  Collapse,
  Divider,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Typography,
} from "antd";
import {
  createDefaultEffect,
  type NickEffect,
  type NicknamePresetStyle,
  type NickGradientStop,
} from "@/lib/nickname-preset-style";

const EFFECT_TYPES = [
  { value: "color", label: "Color / Gradient" },
  { value: "move", label: "Move" },
  { value: "rotate", label: "Rotate" },
  { value: "scale", label: "Scale" },
  { value: "composite", label: "Composite (stack)" },
  { value: "perVertex", label: "Per vertex" },
] as const;

type Props = {
  value: NicknamePresetStyle;
  onChange: (next: NicknamePresetStyle) => void;
};

function TimingFields({
  effect,
  onChange,
}: {
  effect: NickEffect;
  onChange: (next: NickEffect) => void;
}) {
  return (
    <Space direction="vertical" style={{ width: "100%" }} size="small">
      <Space wrap>
        <Select
          style={{ width: 160 }}
          value={effect.animationType || "PingPong"}
          options={[
            { value: "Loop", label: "Loop" },
            { value: "LoopFixedDuration", label: "Loop fixed" },
            { value: "PingPong", label: "PingPong" },
            { value: "OneTime", label: "OneTime" },
          ]}
          onChange={(animationType) => onChange({ ...effect, animationType })}
        />
        <Select
          style={{ width: 140 }}
          value={effect.triggerWhen || "OnStart"}
          options={[
            { value: "OnStart", label: "OnStart" },
            { value: "Manual", label: "Manual" },
          ]}
          onChange={(triggerWhen) => onChange({ ...effect, triggerWhen })}
        />
        <Select
          style={{ width: 140 }}
          value={effect.easing || "linear"}
          options={[
            { value: "linear", label: "linear" },
            { value: "easeIn", label: "easeIn" },
            { value: "easeOut", label: "easeOut" },
            { value: "easeInOut", label: "easeInOut" },
          ]}
          onChange={(easing) => onChange({ ...effect, easing })}
        />
        <Select
          style={{ width: 150 }}
          value={effect.timeType || "UnscaledTime"}
          options={[
            { value: "UnscaledTime", label: "UnscaledTime" },
            { value: "ScaledTime", label: "ScaledTime" },
          ]}
          onChange={(timeType) => onChange({ ...effect, timeType })}
        />
      </Space>
      <Space wrap>
        <label>
          duration/char{" "}
          <InputNumber
            value={effect.durationPerChar ?? 0.5}
            step={0.05}
            min={0.01}
            onChange={(v) => onChange({ ...effect, durationPerChar: Number(v) || 0.5 })}
          />
        </label>
        <label>
          between chars{" "}
          <InputNumber
            value={effect.timeBetweenChars ?? 0.05}
            step={0.01}
            min={0}
            onChange={(v) => onChange({ ...effect, timeBetweenChars: Number(v) || 0 })}
          />
        </label>
        <label>
          fixed duration{" "}
          <InputNumber
            value={effect.fixedDuration ?? 1}
            step={0.1}
            min={0}
            onChange={(v) => onChange({ ...effect, fixedDuration: Number(v) || 0 })}
          />
        </label>
      </Space>
      <Space wrap>
        <label>
          no delay{" "}
          <Switch
            checked={!!effect.noDelayForChars}
            onChange={(noDelayForChars) => onChange({ ...effect, noDelayForChars })}
          />
        </label>
        <label>
          reverse{" "}
          <Switch
            checked={!!effect.reverseCharOrder}
            onChange={(reverseCharOrder) => onChange({ ...effect, reverseCharOrder })}
          />
        </label>
        <label>
          clamp 0..1{" "}
          <Switch
            checked={!!effect.clampBetween0And1}
            onChange={(clampBetween0And1) => onChange({ ...effect, clampBetween0And1 })}
          />
        </label>
        <label>
          override tags{" "}
          <Switch
            checked={!!effect.overrideTagEffects}
            onChange={(overrideTagEffects) => onChange({ ...effect, overrideTagEffects })}
          />
        </label>
      </Space>
    </Space>
  );
}

function GradientStopsEditor({
  stops,
  onChange,
}: {
  stops: NickGradientStop[];
  onChange: (next: NickGradientStop[]) => void;
}) {
  return (
    <Space direction="vertical" style={{ width: "100%" }}>
      {(stops || []).map((stop, index) => (
        <Space key={index} wrap>
          <InputNumber
            min={0}
            max={1}
            step={0.01}
            value={stop.t}
            onChange={(t) => {
              const next = [...stops];
              next[index] = { ...stop, t: Number(t) || 0 };
              onChange(next);
            }}
          />
          <Input
            style={{ width: 120 }}
            value={stop.hex}
            placeholder="#RRGGBB"
            onChange={(e) => {
              const next = [...stops];
              next[index] = { ...stop, hex: e.target.value };
              onChange(next);
            }}
          />
          <Button
            danger
            size="small"
            onClick={() => onChange(stops.filter((_, i) => i !== index))}
          >
            Remove stop
          </Button>
        </Space>
      ))}
      <Button
        size="small"
        onClick={() => onChange([...(stops || []), { t: 1, hex: "#FFFFFF" }])}
      >
        Add gradient stop
      </Button>
    </Space>
  );
}

function EffectListEditor({
  title,
  effects,
  onChange,
  depth = 0,
}: {
  title: string;
  effects: NickEffect[];
  onChange: (next: NickEffect[]) => void;
  depth?: number;
}) {
  return (
    <Card size="small" title={title} style={{ marginTop: 8 }}>
      <Space direction="vertical" style={{ width: "100%" }} size="middle">
        {(effects || []).map((effect, index) => (
          <Card
            key={index}
            size="small"
            type="inner"
            title={`Effect #${index + 1} (${effect.type})`}
            extra={
              <Button
                danger
                size="small"
                onClick={() => onChange(effects.filter((_, i) => i !== index))}
              >
                Remove
              </Button>
            }
          >
            <Space direction="vertical" style={{ width: "100%" }} size="small">
              <Select
                style={{ width: 220 }}
                value={effect.type}
                options={[...EFFECT_TYPES]}
                onChange={(type) => {
                  const next = [...effects];
                  next[index] = createDefaultEffect(type as NickEffect["type"]);
                  onChange(next);
                }}
              />
              <TimingFields
                effect={effect}
                onChange={(nextEffect) => {
                  const next = [...effects];
                  next[index] = nextEffect;
                  onChange(next);
                }}
              />
              <Divider style={{ margin: "8px 0" }} />
              {effect.type === "color" ? (
                <Space direction="vertical" style={{ width: "100%" }}>
                  <Select
                    style={{ width: 220 }}
                    value={effect.colorType || "Gradient"}
                    options={[
                      { value: "Gradient", label: "Gradient" },
                      { value: "BetweenTwoColors", label: "Between two colors" },
                      { value: "OnlyAlpha", label: "Only alpha" },
                      { value: "ColorToOriginal", label: "Color → original" },
                      { value: "OriginalToColor", label: "Original → color" },
                    ]}
                    onChange={(colorType) => {
                      const next = [...effects];
                      next[index] = { ...effect, colorType };
                      onChange(next);
                    }}
                  />
                  <Select
                    style={{ width: 240 }}
                    value={effect.orientation || "Horizontal"}
                    options={[
                      { value: "Horizontal", label: "Horizontal" },
                      { value: "HorizontalPerCharacter", label: "Horizontal per char" },
                      { value: "Vertical", label: "Vertical" },
                    ]}
                    onChange={(orientation) => {
                      const next = [...effects];
                      next[index] = { ...effect, orientation };
                      onChange(next);
                    }}
                  />
                  <Space wrap>
                    <label>
                      start hex{" "}
                      <Input
                        style={{ width: 120 }}
                        value={effect.startColor || ""}
                        onChange={(e) => {
                          const next = [...effects];
                          next[index] = { ...effect, startColor: e.target.value };
                          onChange(next);
                        }}
                      />
                    </label>
                    <label>
                      end hex{" "}
                      <Input
                        style={{ width: 120 }}
                        value={effect.endColor || ""}
                        onChange={(e) => {
                          const next = [...effects];
                          next[index] = { ...effect, endColor: e.target.value };
                          onChange(next);
                        }}
                      />
                    </label>
                    <label>
                      stride{" "}
                      <InputNumber
                        value={effect.stride ?? 10}
                        onChange={(v) => {
                          const next = [...effects];
                          next[index] = { ...effect, stride: Number(v) || 10 };
                          onChange(next);
                        }}
                      />
                    </label>
                    <label>
                      start α{" "}
                      <InputNumber
                        min={0}
                        max={1}
                        step={0.05}
                        value={effect.startAlpha ?? 0}
                        onChange={(v) => {
                          const next = [...effects];
                          next[index] = { ...effect, startAlpha: Number(v) || 0 };
                          onChange(next);
                        }}
                      />
                    </label>
                    <label>
                      end α{" "}
                      <InputNumber
                        min={0}
                        max={1}
                        step={0.05}
                        value={effect.endAlpha ?? 1}
                        onChange={(v) => {
                          const next = [...effects];
                          next[index] = { ...effect, endAlpha: Number(v) || 0 };
                          onChange(next);
                        }}
                      />
                    </label>
                  </Space>
                  <GradientStopsEditor
                    stops={effect.gradientStops || []}
                    onChange={(gradientStops) => {
                      const next = [...effects];
                      next[index] = { ...effect, gradientStops };
                      onChange(next);
                    }}
                  />
                </Space>
              ) : null}

              {effect.type === "move" ? (
                <Space wrap>
                  <label>
                    start X{" "}
                    <InputNumber
                      value={effect.startOffset?.x ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          startOffset: { x: Number(v) || 0, y: effect.startOffset?.y ?? 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    start Y{" "}
                    <InputNumber
                      value={effect.startOffset?.y ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          startOffset: { x: effect.startOffset?.x ?? 0, y: Number(v) || 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    end X{" "}
                    <InputNumber
                      value={effect.endOffset?.x ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          endOffset: { x: Number(v) || 0, y: effect.endOffset?.y ?? 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    end Y{" "}
                    <InputNumber
                      value={effect.endOffset?.y ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          endOffset: { x: effect.endOffset?.x ?? 0, y: Number(v) || 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                </Space>
              ) : null}

              {effect.type === "rotate" ? (
                <Space wrap>
                  <label>
                    center X{" "}
                    <InputNumber
                      value={effect.centerOffset?.x ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          centerOffset: { x: Number(v) || 0, y: effect.centerOffset?.y ?? 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    center Y{" "}
                    <InputNumber
                      value={effect.centerOffset?.y ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = {
                          ...effect,
                          centerOffset: { x: effect.centerOffset?.x ?? 0, y: Number(v) || 0 },
                        };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    start °{" "}
                    <InputNumber
                      value={effect.startAngle ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = { ...effect, startAngle: Number(v) || 0 };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    end °{" "}
                    <InputNumber
                      value={effect.endAngle ?? 0}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = { ...effect, endAngle: Number(v) || 0 };
                        onChange(next);
                      }}
                    />
                  </label>
                </Space>
              ) : null}

              {effect.type === "scale" ? (
                <Space wrap>
                  <label>
                    start scale{" "}
                    <InputNumber
                      value={effect.startScale ?? 0}
                      step={0.05}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = { ...effect, startScale: Number(v) || 0 };
                        onChange(next);
                      }}
                    />
                  </label>
                  <label>
                    end scale{" "}
                    <InputNumber
                      value={effect.endScale ?? 1}
                      step={0.05}
                      onChange={(v) => {
                        const next = [...effects];
                        next[index] = { ...effect, endScale: Number(v) || 0 };
                        onChange(next);
                      }}
                    />
                  </label>
                </Space>
              ) : null}

              {effect.type === "composite" && depth < 3 ? (
                <EffectListEditor
                  title="Child effects"
                  effects={effect.effects || []}
                  depth={depth + 1}
                  onChange={(childEffects) => {
                    const next = [...effects];
                    next[index] = { ...effect, effects: childEffects };
                    onChange(next);
                  }}
                />
              ) : null}

              {effect.type === "perVertex" && depth < 3 ? (
                <Collapse
                  items={[
                    {
                      key: "tl",
                      label: "Top left",
                      children: (
                        <EffectListEditor
                          title="Top left"
                          effects={effect.topLeft || []}
                          depth={depth + 1}
                          onChange={(list) => {
                            const next = [...effects];
                            next[index] = { ...effect, topLeft: list };
                            onChange(next);
                          }}
                        />
                      ),
                    },
                    {
                      key: "tr",
                      label: "Top right",
                      children: (
                        <EffectListEditor
                          title="Top right"
                          effects={effect.topRight || []}
                          depth={depth + 1}
                          onChange={(list) => {
                            const next = [...effects];
                            next[index] = { ...effect, topRight: list };
                            onChange(next);
                          }}
                        />
                      ),
                    },
                    {
                      key: "bl",
                      label: "Bottom left",
                      children: (
                        <EffectListEditor
                          title="Bottom left"
                          effects={effect.bottomLeft || []}
                          depth={depth + 1}
                          onChange={(list) => {
                            const next = [...effects];
                            next[index] = { ...effect, bottomLeft: list };
                            onChange(next);
                          }}
                        />
                      ),
                    },
                    {
                      key: "br",
                      label: "Bottom right",
                      children: (
                        <EffectListEditor
                          title="Bottom right"
                          effects={effect.bottomRight || []}
                          depth={depth + 1}
                          onChange={(list) => {
                            const next = [...effects];
                            next[index] = { ...effect, bottomRight: list };
                            onChange(next);
                          }}
                        />
                      ),
                    },
                  ]}
                />
              ) : null}
            </Space>
          </Card>
        ))}

        <Space wrap>
          {EFFECT_TYPES.map((item) => (
            <Button
              key={item.value}
              size="small"
              onClick={() => onChange([...(effects || []), createDefaultEffect(item.value)])}
            >
              + {item.label}
            </Button>
          ))}
        </Space>
      </Space>
    </Card>
  );
}

export function NicknamePresetStyleEditor({ value, onChange }: Props) {
  return (
    <Space direction="vertical" style={{ width: "100%" }} size="middle">
      <Space wrap>
        <label>
          Font name (Unity registry){" "}
          <Input
            style={{ width: 220 }}
            value={value.fontName || ""}
            placeholder="e.g. fancy"
            onChange={(e) => onChange({ ...value, fontName: e.target.value })}
          />
        </label>
        <label>
          Base color hex{" "}
          <Input
            style={{ width: 140 }}
            value={value.baseColor || "#FFFFFF"}
            placeholder="#FFFFFF"
            onChange={(e) => onChange({ ...value, baseColor: e.target.value })}
          />
        </label>
        <label>
          Outline color hex{" "}
          <Input
            style={{ width: 140 }}
            value={value.outlineColor || "#000000"}
            placeholder="#000000"
            onChange={(e) => onChange({ ...value, outlineColor: e.target.value })}
          />
        </label>
        <label>
          Outline width (жирность){" "}
          <InputNumber
            min={0}
            max={1}
            step={0.01}
            value={value.outlineWidth ?? 0}
            onChange={(v) => onChange({ ...value, outlineWidth: Number(v) || 0 })}
          />
        </label>
      </Space>
      <Typography.Text type="secondary">
        Font name must match an entry in Unity KaplaoNicknameFontRegistry. Leave empty for default
        font. Outline uses TMP SDF outline (works with Easy Text Effects). Width 0 = no outline.
        Typical values: 0.15–0.35.
      </Typography.Text>
      <EffectListEditor
        title="Easy Text Effects"
        effects={value.effects || []}
        onChange={(effects) => onChange({ ...value, effects })}
      />
    </Space>
  );
}
