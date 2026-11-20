using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KaplaoGames.Client
{
    [Serializable]
    public class ParticleFloatBinding
    {
        public TMP_InputField input;
        public Slider slider;
    }

    [Serializable]
    public class ParticleIntBinding
    {
        public TMP_InputField input;
        public Slider slider;
    }

    [Serializable]
    public class ParticleFloatRangeBinding
    {
        public TMP_InputField valueInput;
        public Slider valueSlider;
        public TMP_InputField rangeInput;
        public Slider rangeSlider;
    }

    [Serializable]
    public class ParticleVec2Binding
    {
        public TMP_InputField xInput;
        public Slider xSlider;
        public TMP_InputField yInput;
        public Slider ySlider;
    }

    [Serializable]
    public class ParticleEditorBindings
    {
        [Header("Shell")]
        public RectTransform panelRoot;
        public Button closeButton;
        public Button motionTabButton;
        public Button visualTabButton;
        public RectTransform motionTabRoot;
        public RectTransform visualTabRoot;
        public Toggle enabledToggle;
        public RawImage previewImage;
        public Image previewBackground;

        [Header("Motion")]
        public ParticleFloatBinding duration;
        public ParticleFloatRangeBinding lifetime;
        public ParticleIntBinding emission;
        public ParticleFloatRangeBinding angle;
        public ParticleFloatRangeBinding speed;
        public ParticleVec2Binding posVar;
        public ParticleVec2Binding gravity;
        public ParticleFloatRangeBinding accelRad;
        public ParticleFloatRangeBinding accelTan;

        [Header("Visual")]
        public ParticleFloatRangeBinding startSize;
        public ParticleFloatRangeBinding endSize;
        public ParticleFloatRangeBinding startSpin;
        public ParticleFloatRangeBinding endSpin;
        public ParticleFloatRangeBinding startR;
        public ParticleFloatRangeBinding startG;
        public ParticleFloatRangeBinding startB;
        public ParticleFloatRangeBinding startA;
        public ParticleFloatRangeBinding endR;
        public ParticleFloatRangeBinding endG;
        public ParticleFloatRangeBinding endB;
        public ParticleFloatRangeBinding endA;

        public bool HasShell =>
            panelRoot != null &&
            closeButton != null &&
            motionTabButton != null &&
            visualTabButton != null &&
            motionTabRoot != null &&
            visualTabRoot != null;

        public bool HasMotionRows =>
            duration.input != null &&
            duration.slider != null &&
            lifetime.valueInput != null &&
            lifetime.valueSlider != null &&
            emission.input != null &&
            emission.slider != null;
    }
}
