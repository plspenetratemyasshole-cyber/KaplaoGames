using System;
using System.Collections.Generic;
using KaplaoGames.Shared;
using Mirror;
using UnityEngine;

namespace KaplaoGames.Server
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public class ParticleEmitterTile : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnEnabledChanged))]
        bool syncedEnabled = true;

        [SyncVar] float syncedDuration = -1f;
        [SyncVar] int syncedEmission = 20;

        [SyncVar] float syncedLifetimeValue = 0.9f;
        [SyncVar] float syncedLifetimeRange = 0.3f;

        [SyncVar] float syncedAngleValue = -90f;
        [SyncVar] float syncedAngleRange = 0f;

        [SyncVar] float syncedSpeedValue = 10f;
        [SyncVar] float syncedSpeedRange = 0f;

        [SyncVar] float syncedPosVarX = 0f;
        [SyncVar] float syncedPosVarY = 0f;

        [SyncVar] float syncedGravityX = 0f;
        [SyncVar] float syncedGravityY = 0f;

        [SyncVar] float syncedAccelRadValue = 0f;
        [SyncVar] float syncedAccelRadRange = 0f;

        [SyncVar] float syncedAccelTanValue = 0f;
        [SyncVar] float syncedAccelTanRange = 0f;

        [SyncVar] float syncedStartSizeValue = 0.6f;
        [SyncVar] float syncedStartSizeRange = 0f;
        [SyncVar] float syncedEndSizeValue = 0.2f;
        [SyncVar] float syncedEndSizeRange = 0f;

        [SyncVar] float syncedStartSpinValue = 0f;
        [SyncVar] float syncedStartSpinRange = 0f;
        [SyncVar] float syncedEndSpinValue = 0f;
        [SyncVar] float syncedEndSpinRange = 0f;

        [SyncVar] float syncedStartRValue = 255f;
        [SyncVar] float syncedStartRRange = 0f;
        [SyncVar] float syncedStartGValue = 255f;
        [SyncVar] float syncedStartGRange = 0f;
        [SyncVar] float syncedStartBValue = 255f;
        [SyncVar] float syncedStartBRange = 0f;
        [SyncVar] float syncedStartAValue = 1f;
        [SyncVar] float syncedStartARange = 0f;

        [SyncVar] float syncedEndRValue = 255f;
        [SyncVar] float syncedEndRRange = 0f;
        [SyncVar] float syncedEndGValue = 255f;
        [SyncVar] float syncedEndGRange = 0f;
        [SyncVar] float syncedEndBValue = 255f;
        [SyncVar] float syncedEndBRange = 0f;
        [SyncVar] float syncedEndAValue = 0f;
        [SyncVar] float syncedEndARange = 0f;

        [SyncVar(hook = nameof(OnTextureIdChanged))]
        string syncedTextureId = string.Empty;

        float serverEnabledUntil = -1f;

        Transform particleRoot;
        readonly List<ParticleInstance> particles = new();
        readonly Stack<SpriteRenderer> pool = new();
        float emissionAccumulator;

        sealed class ParticleInstance
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float AccelRad;
            public float AccelTan;
            public float Age;
            public float Lifetime;
            public float StartSize;
            public float EndSize;
            public float StartSpin;
            public float EndSpin;
            public Color StartColor;
            public Color EndColor;
        }

        public bool Enabled => syncedEnabled;

        [Server]
        public void ServerSetBool(string propertyPath, bool value)
        {
            if (propertyPath == "enabled")
            {
                syncedEnabled = value;
                if (syncedEnabled)
                    ArmServerDurationIfNeeded();
                return;
            }

            throw new InvalidOperationException($"Unknown Particle bool property '{propertyPath}'.");
        }

        [Server]
        public void ServerSetInt(string propertyPath, int value)
        {
            if (propertyPath == "Emission.Value")
            {
                syncedEmission = Mathf.Clamp(value, 0, 5000);
                return;
            }

            throw new InvalidOperationException($"Unknown Particle int property '{propertyPath}'.");
        }

        [Server]
        public void ServerSetFloat(string propertyPath, float value)
        {
            switch (propertyPath)
            {
                case "Duration":
                case "duration":
                    syncedDuration = value;
                    if (syncedEnabled)
                        ArmServerDurationIfNeeded();
                    return;

                case "Lifetime.Value": syncedLifetimeValue = value; return;
                case "Lifetime.Range": syncedLifetimeRange = value; return;
                case "Angle.Value": syncedAngleValue = value; return;
                case "Angle.Range": syncedAngleRange = value; return;
                case "Speed.Value": syncedSpeedValue = value; return;
                case "Speed.Range": syncedSpeedRange = value; return;
                case "PosVarX.Value": syncedPosVarX = value; return;
                case "PosVarY.Value": syncedPosVarY = value; return;
                case "GravityX.Value": syncedGravityX = value; return;
                case "GravityY.Value": syncedGravityY = value; return;
                case "AccelRad.Value": syncedAccelRadValue = value; return;
                case "AccelRad.Range": syncedAccelRadRange = value; return;
                case "AccelTan.Value": syncedAccelTanValue = value; return;
                case "AccelTan.Range": syncedAccelTanRange = value; return;

                case "StartSize.Value": syncedStartSizeValue = value; return;
                case "StartSize.Range": syncedStartSizeRange = value; return;
                case "EndSize.Value": syncedEndSizeValue = value; return;
                case "EndSize.Range": syncedEndSizeRange = value; return;
                case "StartSpin.Value": syncedStartSpinValue = value; return;
                case "StartSpin.Range": syncedStartSpinRange = value; return;
                case "EndSpin.Value": syncedEndSpinValue = value; return;
                case "EndSpin.Range": syncedEndSpinRange = value; return;

                case "Start_R.Value": syncedStartRValue = value; return;
                case "Start_R.Range": syncedStartRRange = value; return;
                case "Start_G.Value": syncedStartGValue = value; return;
                case "Start_G.Range": syncedStartGRange = value; return;
                case "Start_B.Value": syncedStartBValue = value; return;
                case "Start_B.Range": syncedStartBRange = value; return;
                case "Start_A.Value": syncedStartAValue = value; return;
                case "Start_A.Range": syncedStartARange = value; return;
                case "End_R.Value": syncedEndRValue = value; return;
                case "End_R.Range": syncedEndRRange = value; return;
                case "End_G.Value": syncedEndGValue = value; return;
                case "End_G.Range": syncedEndGRange = value; return;
                case "End_B.Value": syncedEndBValue = value; return;
                case "End_B.Range": syncedEndBRange = value; return;
                case "End_A.Value": syncedEndAValue = value; return;
                case "End_A.Range": syncedEndARange = value; return;

                default:
                    throw new InvalidOperationException($"Unknown Particle float property '{propertyPath}'.");
            }
        }

        [Server]
        public void ServerApply(ParticleEmitterRecord record)
        {
            syncedEnabled = record.enabled;
            syncedDuration = record.duration;
            syncedEmission = record.emission;

            syncedLifetimeValue = record.lifetime.value;
            syncedLifetimeRange = record.lifetime.range;

            syncedAngleValue = record.angle.value;
            syncedAngleRange = record.angle.range;

            syncedSpeedValue = record.speed.value;
            syncedSpeedRange = record.speed.range;

            syncedPosVarX = record.posVarX;
            syncedPosVarY = record.posVarY;

            syncedGravityX = record.gravityX;
            syncedGravityY = record.gravityY;

            syncedAccelRadValue = record.accelRad.value;
            syncedAccelRadRange = record.accelRad.range;

            syncedAccelTanValue = record.accelTan.value;
            syncedAccelTanRange = record.accelTan.range;

            syncedStartSizeValue = record.startSize.value;
            syncedStartSizeRange = record.startSize.range;
            syncedEndSizeValue = record.endSize.value;
            syncedEndSizeRange = record.endSize.range;

            syncedStartSpinValue = record.startSpin.value;
            syncedStartSpinRange = record.startSpin.range;
            syncedEndSpinValue = record.endSpin.value;
            syncedEndSpinRange = record.endSpin.range;

            syncedStartRValue = record.startR.value;
            syncedStartRRange = record.startR.range;
            syncedStartGValue = record.startG.value;
            syncedStartGRange = record.startG.range;
            syncedStartBValue = record.startB.value;
            syncedStartBRange = record.startB.range;
            syncedStartAValue = record.startA.value;
            syncedStartARange = record.startA.range;

            syncedEndRValue = record.endR.value;
            syncedEndRRange = record.endR.range;
            syncedEndGValue = record.endG.value;
            syncedEndGRange = record.endG.range;
            syncedEndBValue = record.endB.value;
            syncedEndBRange = record.endB.range;
            syncedEndAValue = record.endA.value;
            syncedEndARange = record.endA.range;

            syncedTextureId = record.textureId ?? string.Empty;

            if (syncedEnabled)
                ArmServerDurationIfNeeded();
        }

        [Server]
        public void ServerSetTexture(string textureId)
        {
            syncedTextureId = string.IsNullOrWhiteSpace(textureId) ? string.Empty : textureId.Trim();
        }

        public ParticleEmitterRecord ExportRecord()
        {
            return new ParticleEmitterRecord
            {
                enabled = syncedEnabled,
                duration = syncedDuration,
                emission = syncedEmission,
                lifetime = new ParticleFloatRange { value = syncedLifetimeValue, range = syncedLifetimeRange },
                angle = new ParticleFloatRange { value = syncedAngleValue, range = syncedAngleRange },
                speed = new ParticleFloatRange { value = syncedSpeedValue, range = syncedSpeedRange },
                accelRad = new ParticleFloatRange { value = syncedAccelRadValue, range = syncedAccelRadRange },
                accelTan = new ParticleFloatRange { value = syncedAccelTanValue, range = syncedAccelTanRange },
                posVarX = syncedPosVarX,
                posVarY = syncedPosVarY,
                gravityX = syncedGravityX,
                gravityY = syncedGravityY,
                startSize = new ParticleFloatRange { value = syncedStartSizeValue, range = syncedStartSizeRange },
                endSize = new ParticleFloatRange { value = syncedEndSizeValue, range = syncedEndSizeRange },
                startSpin = new ParticleFloatRange { value = syncedStartSpinValue, range = syncedStartSpinRange },
                endSpin = new ParticleFloatRange { value = syncedEndSpinValue, range = syncedEndSpinRange },
                startR = new ParticleFloatRange { value = syncedStartRValue, range = syncedStartRRange },
                startG = new ParticleFloatRange { value = syncedStartGValue, range = syncedStartGRange },
                startB = new ParticleFloatRange { value = syncedStartBValue, range = syncedStartBRange },
                startA = new ParticleFloatRange { value = syncedStartAValue, range = syncedStartARange },
                endR = new ParticleFloatRange { value = syncedEndRValue, range = syncedEndRRange },
                endG = new ParticleFloatRange { value = syncedEndGValue, range = syncedEndGRange },
                endB = new ParticleFloatRange { value = syncedEndBValue, range = syncedEndBRange },
                endA = new ParticleFloatRange { value = syncedEndAValue, range = syncedEndARange },
                textureId = syncedTextureId ?? string.Empty
            };
        }

        [Server]
        public ParticleEmitterRecord ServerExport() => ExportRecord();

        void Update()
        {
            if (isServer)
                TickServerDuration();

            if (!isClient)
                return;

            EnsureParticleRoot();
            TickClientSimulation(Time.deltaTime);
        }

        void OnEnabledChanged(bool _, bool __)
        {
            if (isServer && syncedEnabled)
                ArmServerDurationIfNeeded();
        }

        void OnTextureIdChanged(string _, string __)
        {
            Sprite sprite = ResolveParticleSprite();
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i].Renderer != null)
                    particles[i].Renderer.sprite = sprite;
            }

            foreach (SpriteRenderer renderer in pool)
            {
                if (renderer != null)
                    renderer.sprite = sprite;
            }
        }

        [Server]
        void ArmServerDurationIfNeeded()
        {
            if (syncedDuration >= 0f)
                serverEnabledUntil = Time.time + syncedDuration;
            else
                serverEnabledUntil = -1f;
        }

        [Server]
        void TickServerDuration()
        {
            if (!syncedEnabled)
                return;

            if (serverEnabledUntil < 0f)
                return;

            if (Time.time >= serverEnabledUntil)
            {
                syncedEnabled = false;
                serverEnabledUntil = -1f;
            }
        }

        void EnsureParticleRoot()
        {
            if (particleRoot != null)
                return;

            var root = new GameObject("ParticleEmitterRuntime");
            root.transform.SetParent(transform, false);
            particleRoot = root.transform;
        }

        void TickClientSimulation(float dt)
        {
            EmitIfNeeded(dt);
            UpdateParticles(dt);
        }

        void EmitIfNeeded(float dt)
        {
            if (!syncedEnabled)
                return;

            const int MaxLiveParticles = 180;
            if (particles.Count >= MaxLiveParticles)
            {
                emissionAccumulator = 0f;
                return;
            }

            float rate = Mathf.Min(Mathf.Max(0f, syncedEmission), 400f);
            emissionAccumulator += rate * dt;
            int toSpawn = Mathf.FloorToInt(emissionAccumulator);
            if (toSpawn <= 0)
                return;

            emissionAccumulator -= toSpawn;
            int room = MaxLiveParticles - particles.Count;
            toSpawn = Mathf.Min(toSpawn, room);
            for (int i = 0; i < toSpawn; i++)
                SpawnParticle();
        }

        void SpawnParticle()
        {
            SpriteRenderer renderer = GetRendererFromPool();

            Vector2 localOffset = new Vector2(
                UnityEngine.Random.Range(-syncedPosVarX, syncedPosVarX),
                UnityEngine.Random.Range(-syncedPosVarY, syncedPosVarY));

            float lifetime = SampleRange(syncedLifetimeValue, syncedLifetimeRange);
            lifetime = Mathf.Max(0.01f, lifetime);

            float angle = SampleRange(syncedAngleValue, syncedAngleRange) * Mathf.Deg2Rad;
            float speed = Mathf.Max(0f, SampleRange(syncedSpeedValue, syncedSpeedRange));
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            float accelRad = SampleRange(syncedAccelRadValue, syncedAccelRadRange);
            float accelTan = SampleRange(syncedAccelTanValue, syncedAccelTanRange);

            float startSize = Mathf.Max(0f, SampleRange(syncedStartSizeValue, syncedStartSizeRange));
            float endSize = Mathf.Max(0f, SampleRange(syncedEndSizeValue, syncedEndSizeRange));
            float startSpin = SampleRange(syncedStartSpinValue, syncedStartSpinRange);
            float endSpin = SampleRange(syncedEndSpinValue, syncedEndSpinRange);

            Color startColor = new Color(
                Mathf.Clamp01(SampleRange(syncedStartRValue, syncedStartRRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedStartGValue, syncedStartGRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedStartBValue, syncedStartBRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedStartAValue, syncedStartARange)));

            Color endColor = new Color(
                Mathf.Clamp01(SampleRange(syncedEndRValue, syncedEndRRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedEndGValue, syncedEndGRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedEndBValue, syncedEndBRange) / 255f),
                Mathf.Clamp01(SampleRange(syncedEndAValue, syncedEndARange)));

            var instance = new ParticleInstance
            {
                Renderer = renderer,
                Position = localOffset,
                Velocity = velocity,
                AccelRad = accelRad,
                AccelTan = accelTan,
                Age = 0f,
                Lifetime = lifetime,
                StartSize = startSize,
                EndSize = endSize,
                StartSpin = startSpin,
                EndSpin = endSpin,
                StartColor = startColor,
                EndColor = endColor
            };

            ApplyParticleVisuals(instance, normalizedAge: 0f);
            particles.Add(instance);
        }

        void UpdateParticles(float dt)
        {
            Vector2 gravity = new Vector2(syncedGravityX, syncedGravityY);

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                ParticleInstance p = particles[i];
                p.Age += dt;
                float t = p.Age / p.Lifetime;
                if (t >= 1f)
                {
                    Recycle(p);
                    particles.RemoveAt(i);
                    continue;
                }

                Vector2 v = p.Velocity;
                Vector2 accel = ParticleMotion.ComputeAcceleration(p.Position, gravity, p.AccelRad, p.AccelTan);

                v += accel * dt;
                p.Velocity = v;
                p.Position += v * dt;

                ApplyParticleVisuals(p, t);
            }
        }

        void ApplyParticleVisuals(ParticleInstance p, float normalizedAge)
        {
            if (p.Renderer == null)
                return;

            p.Renderer.transform.localPosition = new Vector3(p.Position.x, p.Position.y, 0f);
            float size = Mathf.Lerp(p.StartSize, p.EndSize, normalizedAge);
            p.Renderer.transform.localScale = new Vector3(size, size, 1f);
            float spin = Mathf.Lerp(p.StartSpin, p.EndSpin, normalizedAge);
            p.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            p.Renderer.color = Color.Lerp(p.StartColor, p.EndColor, normalizedAge);
        }

        SpriteRenderer GetRendererFromPool()
        {
            SpriteRenderer renderer;
            if (pool.Count > 0)
            {
                renderer = pool.Pop();
                if (renderer != null)
                {
                    renderer.gameObject.SetActive(true);
                    return renderer;
                }
            }

            var go = new GameObject("Particle", typeof(SpriteRenderer));
            go.transform.SetParent(particleRoot, false);
            renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = ResolveParticleSprite();
            renderer.sortingOrder = ResolveBaseSortingOrder();
            return renderer;
        }

        void Recycle(ParticleInstance p)
        {
            if (p.Renderer == null)
                return;

            p.Renderer.gameObject.SetActive(false);
            pool.Push(p.Renderer);
        }

        int ResolveBaseSortingOrder()
        {
            if (TryGetComponent(out LevelTileServer tile))
                return tile.SortingOrder + 1;
            return 11;
        }

        Sprite ResolveParticleSprite()
        {
            return ParticleSpriteUtility.ResolveSprite(syncedTextureId);
        }

        static float SampleRange(float value, float range)
        {
            if (range <= 0f)
                return value;
            return UnityEngine.Random.Range(value - range, value + range);
        }
    }
}

