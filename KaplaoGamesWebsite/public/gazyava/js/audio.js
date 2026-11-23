/* ==========================================================================
   ПРОЦЕДУРНЫЙ ЗВУКОВОЙ ДВИЖОК (WEB AUDIO API)
   ========================================================================== */
import { loadMuteSetting, saveMuteSetting } from './storage.js';

export class SoundEngine {
        constructor() {
          this.ctx = null;
          this.muted = loadMuteSetting();
          this.noiseBuf = null;
          this.fizzSrc = null;
          this.fizzFilter = null;
          this.fizzGain = null;
          this.isFizzing = false;
          this.humOsc = null;
          this.humGain = null;
          this.isHumming = false;
        }

        unlock() {
          try {
            if (!this.ctx) {
              const AC = window.AudioContext || window.webkitAudioContext;
              if (!AC) return;
              this.ctx = new AC();
              const len = this.ctx.sampleRate * 2;
              const buf = this.ctx.createBuffer(1, len, this.ctx.sampleRate);
              const ch = buf.getChannelData(0);
              for (let i = 0; i < len; i++) ch[i] = Math.random() * 2 - 1;
              this.noiseBuf = buf;
            }
            if (this.ctx.state === 'suspended') {
              this.ctx.resume();
            }
          } catch (e) {}
        }

        toggleMute() {
    this.muted = !this.muted;
    saveMuteSetting(this.muted);
    if (this.muted) {
      this.stopFizz();
      this.stopFreezerHum();
    }
    return this.muted;
  }
  _oldToggleMute() {
          this.muted = !this.muted;
          if (this.muted) {
            this.stopFizz();
            this.stopFreezerHum();
          }
          return this.muted;
        }

        // Шипение газировки при удержании
        startFizz() {
          if (this.muted || !this.ctx || !this.noiseBuf || this.isFizzing) return;
          try {
            const src = this.ctx.createBufferSource();
            src.buffer = this.noiseBuf;
            src.loop = true;

            const filter = this.ctx.createBiquadFilter();
            filter.type = 'bandpass';
            filter.frequency.value = 1900;
            filter.Q.value = 1.5;

            const gain = this.ctx.createGain();
            gain.gain.setValueAtTime(0.001, this.ctx.currentTime);
            gain.gain.linearRampToValueAtTime(0.085, this.ctx.currentTime + 0.08);

            src.connect(filter);
            filter.connect(gain);
            gain.connect(this.ctx.destination);
            src.start();

            this.fizzSrc = src;
            this.fizzFilter = filter;
            this.fizzGain = gain;
            this.isFizzing = true;
          } catch (e) {}
        }

        updateFizz(fizzPct, isFlat) {
          if (!this.isFizzing || !this.ctx || !this.fizzFilter || !this.fizzGain) return;
          const now = this.ctx.currentTime;
          if (isFlat) {
            this.fizzFilter.frequency.setTargetAtTime(550, now, 0.05);
            this.fizzGain.gain.setTargetAtTime(0.015, now, 0.05);
          } else {
            this.fizzFilter.frequency.setTargetAtTime(1600 + (fizzPct / 100) * 4400, now, 0.04);
            this.fizzGain.gain.setTargetAtTime(0.055 + (fizzPct / 100) * 0.12, now, 0.04);
          }
        }

        stopFizz() {
          if (!this.isFizzing) return;
          try {
            if (this.ctx && this.fizzGain) {
              this.fizzGain.gain.setTargetAtTime(0.0001, this.ctx.currentTime, 0.03);
            }
            const ref = this.fizzSrc;
            setTimeout(() => { try { if (ref) ref.stop(); } catch (e) {} }, 90);
          } catch (e) {}
          this.fizzSrc = null;
          this.fizzFilter = null;
          this.fizzGain = null;
          this.isFizzing = false;
        }

        // Бульканье глотка
        playGulp() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const osc = this.ctx.createOscillator();
            const gain = this.ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(240, now);
            osc.frequency.exponentialRampToValueAtTime(480, now + 0.06);
            osc.frequency.exponentialRampToValueAtTime(170, now + 0.13);
            gain.gain.setValueAtTime(0.001, now);
            gain.gain.linearRampToValueAtTime(0.11, now + 0.03);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.14);
            osc.connect(gain);
            gain.connect(this.ctx.destination);
            osc.start(now);
            osc.stop(now + 0.15);
          } catch (e) {}
        }

        // Реалистичный многоимпульсный хруст Хрумки
        playCrunch() {
          if (this.muted || !this.ctx || !this.noiseBuf) return;
          try {
            const now = this.ctx.currentTime;
            const bursts = [0, 0.055, 0.11, 0.175];
            bursts.forEach((off, idx) => {
              const src = this.ctx.createBufferSource();
              src.buffer = this.noiseBuf;
              const filter = this.ctx.createBiquadFilter();
              filter.type = 'bandpass';
              filter.frequency.value = 2100 + idx * 550;
              filter.Q.value = 1.2;

              const gain = this.ctx.createGain();
              const dur = 0.055 + (idx === 3 ? 0.04 : 0);
              gain.gain.setValueAtTime(0.24, now + off);
              gain.gain.exponentialRampToValueAtTime(0.001, now + off + dur);

              src.connect(filter);
              filter.connect(gain);
              gain.connect(this.ctx.destination);
              src.start(now + off, Math.random() * 0.8, dur + 0.01);
            });
          } catch (e) {}
        }

        // Небесный аккорд Вознесения (и усиленный фанфарный на комбо)
        playAscension(isCombo = false) {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const notes = isCombo
              ? [261.63, 329.63, 392.0, 493.88, 587.33, 783.99, 1046.5]
              : [261.63, 329.63, 392.0, 493.88, 587.33];
            notes.forEach((f, i) => {
              const osc = this.ctx.createOscillator();
              const gain = this.ctx.createGain();
              osc.type = i % 2 === 0 ? 'sine' : 'triangle';
              const t0 = now + i * (isCombo ? 0.04 : 0.032);
              osc.frequency.setValueAtTime(f, t0);
              osc.frequency.exponentialRampToValueAtTime(f * 1.008, t0 + 1.4);

              gain.gain.setValueAtTime(0.001, t0);
              gain.gain.linearRampToValueAtTime(isCombo ? 0.08 : 0.06, t0 + 0.1);
              gain.gain.exponentialRampToValueAtTime(0.001, t0 + 1.6);

              osc.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(t0);
              osc.stop(t0 + 1.65);
            });
          } catch (e) {}
        }

        // Кашель / разряд при 100% газов в нос
        playCoughStun() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            [0, 0.2, 0.4].forEach((off, i) => {
              const osc = this.ctx.createOscillator();
              const gain = this.ctx.createGain();
              osc.type = 'sawtooth';
              osc.frequency.setValueAtTime(220 - i * 28, now + off);
              osc.frequency.exponentialRampToValueAtTime(72, now + off + 0.15);
              gain.gain.setValueAtTime(0.14, now + off);
              gain.gain.exponentialRampToValueAtTime(0.001, now + off + 0.16);
              osc.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(now + off);
              osc.stop(now + off + 0.17);
            });
          } catch (e) {}
        }

        // Низкий гул морозилки
        startFreezerHum() {
          if (this.muted || !this.ctx || this.isHumming) return;
          try {
            const osc = this.ctx.createOscillator();
            const gain = this.ctx.createGain();
            osc.type = 'triangle';
            osc.frequency.value = 62;
            gain.gain.setValueAtTime(0.001, this.ctx.currentTime);
            gain.gain.linearRampToValueAtTime(0.05, this.ctx.currentTime + 0.12);
            osc.connect(gain);
            gain.connect(this.ctx.destination);
            osc.start();
            this.humOsc = osc;
            this.humGain = gain;
            this.isHumming = true;
          } catch (e) {}
        }

        stopFreezerHum() {
          if (!this.isHumming) return;
          try {
            if (this.ctx && this.humGain) {
              this.humGain.gain.setTargetAtTime(0.0001, this.ctx.currentTime, 0.04);
            }
            const ref = this.humOsc;
            setTimeout(() => { try { if (ref) ref.stop(); } catch (e) {} }, 100);
          } catch (e) {}
          this.humOsc = null;
          this.humGain = null;
          this.isHumming = false;
        }

        // Раскол льда
        playIceShatter() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            [1800, 2600, 1250].forEach((f, i) => {
              const osc = this.ctx.createOscillator();
              const gain = this.ctx.createGain();
              osc.type = 'triangle';
              osc.frequency.setValueAtTime(f, now + i * 0.025);
              osc.frequency.exponentialRampToValueAtTime(240, now + i * 0.025 + 0.09);
              gain.gain.setValueAtTime(0.14, now + i * 0.025);
              gain.gain.exponentialRampToValueAtTime(0.001, now + i * 0.025 + 0.1);
              osc.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(now + i * 0.025);
              osc.stop(now + i * 0.025 + 0.11);
            });
          } catch (e) {}
        }

        // ПШШШ крышки
        playPssh(loud = false) {
          if (this.muted || !this.ctx || !this.noiseBuf) return;
          try {
            const now = this.ctx.currentTime;
            const src = this.ctx.createBufferSource();
            src.buffer = this.noiseBuf;
            const filter = this.ctx.createBiquadFilter();
            filter.type = 'highpass';
            filter.frequency.value = loud ? 1050 : 2800;
            const gain = this.ctx.createGain();
            const dur = loud ? 0.55 : 0.16;
            gain.gain.setValueAtTime(loud ? 0.34 : 0.12, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + dur);
            src.connect(filter);
            filter.connect(gain);
            gain.connect(this.ctx.destination);
            src.start(now);
            src.stop(now + dur + 0.02);
          } catch (e) {}
        }

        // Тяжёлые шаги Бати по коридору
        playFootstep() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const osc = this.ctx.createOscillator();
            const gain = this.ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(110, now);
            osc.frequency.exponentialRampToValueAtTime(36, now + 0.18);
            gain.gain.setValueAtTime(0.28, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.22);
            osc.connect(gain);
            gain.connect(this.ctx.destination);
            osc.start(now);
            osc.stop(now + 0.24);
          } catch (e) {}
        }

        // Сердцебиение от Энергоса (тук-тук)
        playHeartbeat() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            [0, 0.16].forEach((off, idx) => {
              const osc = this.ctx.createOscillator();
              const gain = this.ctx.createGain();
              osc.type = 'sine';
              osc.frequency.setValueAtTime(idx === 0 ? 85 : 68, now + off);
              osc.frequency.exponentialRampToValueAtTime(32, now + off + 0.12);
              gain.gain.setValueAtTime(idx === 0 ? 0.24 : 0.17, now + off);
              gain.gain.exponentialRampToValueAtTime(0.001, now + off + 0.14);
              osc.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(now + off);
              osc.stop(now + off + 0.15);
            });
          } catch (e) {}
        }

        // Грустный тромбон (тёплая выдохшаяся / поражение)
        playSadTrombone() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const notes = [
              { f: 293.66, t: 0.0, d: 0.26 },
              { f: 277.18, t: 0.28, d: 0.26 },
              { f: 261.63, t: 0.56, d: 0.26 },
              { f: 246.94, t: 0.84, d: 0.58 }
            ];
            notes.forEach((n, idx) => {
              const osc = this.ctx.createOscillator();
              const filter = this.ctx.createBiquadFilter();
              const gain = this.ctx.createGain();
              osc.type = 'sawtooth';
              osc.frequency.setValueAtTime(n.f, now + n.t);
              if (idx === 3) osc.frequency.linearRampToValueAtTime(n.f * 0.92, now + n.t + n.d);
              filter.type = 'lowpass';
              filter.frequency.value = 800;
              gain.gain.setValueAtTime(0.001, now + n.t);
              gain.gain.linearRampToValueAtTime(0.11, now + n.t + 0.04);
              gain.gain.exponentialRampToValueAtTime(0.001, now + n.t + n.d);
              osc.connect(filter);
              filter.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(now + n.t);
              osc.stop(now + n.t + n.d + 0.03);
            });
          } catch (e) {}
        }

        // Победные фанфары
        playVictoryFanfare() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const seq = [
              { f: 392.0, t: 0.0, d: 0.16 },
              { f: 523.25, t: 0.15, d: 0.16 },
              { f: 659.25, t: 0.30, d: 0.18 },
              { f: 783.99, t: 0.48, d: 0.75 }
            ];
            seq.forEach((n) => {
              const osc = this.ctx.createOscillator();
              const gain = this.ctx.createGain();
              osc.type = 'triangle';
              osc.frequency.setValueAtTime(n.f, now + n.t);
              gain.gain.setValueAtTime(0.001, now + n.t);
              gain.gain.linearRampToValueAtTime(0.12, now + n.t + 0.03);
              gain.gain.exponentialRampToValueAtTime(0.001, now + n.t + n.d);
              osc.connect(gain);
              gain.connect(this.ctx.destination);
              osc.start(now + n.t);
              osc.stop(now + n.t + n.d + 0.03);
            });
          } catch (e) {}
        }

        // Лёгкий клик UI
        playClick() {
          if (this.muted || !this.ctx) return;
          try {
            const now = this.ctx.currentTime;
            const osc = this.ctx.createOscillator();
            const gain = this.ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(520, now);
            osc.frequency.exponentialRampToValueAtTime(280, now + 0.05);
            gain.gain.setValueAtTime(0.07, now);
            gain.gain.exponentialRampToValueAtTime(0.001, now + 0.06);
            osc.connect(gain);
            gain.connect(this.ctx.destination);
            osc.start(now);
            osc.stop(now + 0.07);
          } catch (e) {}
        }
      }

      export const sound = new SoundEngine();
