"""An original background track for the AUTOVENT tutorial.

Composed here, in code, from sine waves and filtered noise - no samples,
no loops, nothing downloaded - so it belongs to nobody else and needs no
licence or attribution.

    python music.py <seconds> <out.wav>

D major, 92 bpm, the I-V-vi-IV progression, two bars a chord:

    bars  0-8    pad alone, fading in
    bars  8-16   + a plucked arpeggio with a ping-pong echo
    bars 16-..   + bass, a soft kick and a shaker
    middle       the drums drop out for eight bars, for some air
    last 8 bars  drums out again, and the whole thing fades

Deliberately quiet and unhurried: it sits under text people are
reading, and the worst thing a tutorial bed can do is compete with it.
"""

import sys
import wave

import numpy as np

RATE = 44100
BPM = 92.0
BEAT = 60.0 / BPM
BAR = 4 * BEAT

# I - V - vi - IV in D: pad voicing, arpeggio notes, bass root (MIDI)
CHORDS = [
    ([50, 62, 66, 69], [74, 78, 81, 86], 38),     # D
    ([45, 61, 64, 69], [73, 76, 81, 85], 33),     # A
    ([47, 62, 66, 71], [74, 78, 83, 86], 35),     # Bm
    ([43, 62, 67, 71], [74, 79, 83, 86], 31),     # G
]


def hz(midi):
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def env_adsr(n, attack, release):
    """A smooth rise and fall over n samples."""
    e = np.ones(n)
    a = min(int(attack * RATE), n // 2)
    r = min(int(release * RATE), n // 2)

    if a > 0:
        e[:a] = 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, a))

    if r > 0:
        e[-r:] = 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, r))

    return e


def add(bus, start, mono, pan=0.0):
    """Mix a mono sound into the stereo bus at a time in seconds."""
    i = int(start * RATE)

    if i >= bus.shape[0]:
        return

    mono = mono[: bus.shape[0] - i]

    left = np.cos((pan + 1) * np.pi / 4)
    right = np.sin((pan + 1) * np.pi / 4)

    bus[i:i + len(mono), 0] += mono * left
    bus[i:i + len(mono), 1] += mono * right


def pad(notes, seconds):
    """Soft, slightly detuned, a few harmonics falling away fast."""
    n = int(seconds * RATE)
    t = np.arange(n) / RATE

    left = np.zeros(n)
    right = np.zeros(n)

    for m in notes:
        f = hz(m)

        for k, weight in ((1, 1.0), (2, 0.25), (3, 0.08)):
            # A few cents apart left and right is what gives it width
            left += weight * np.sin(2 * np.pi * f * k * 0.9985 * t)
            right += weight * np.sin(2 * np.pi * f * k * 1.0015 * t)

    e = env_adsr(n, 1.2, 1.4)

    # A slow swell, so a long chord is not a flat wall of sound
    swell = 0.85 + 0.15 * np.sin(2 * np.pi * t / seconds * 1.0 - np.pi / 2)

    scale = 0.05 / len(notes)

    return left * e * swell * scale, right * e * swell * scale


def pluck(midi, seconds=0.9):
    n = int(seconds * RATE)
    t = np.arange(n) / RATE
    f = hz(midi)

    tone = (np.sin(2 * np.pi * f * t)
            + 0.30 * np.sin(2 * np.pi * 2 * f * t)
            + 0.08 * np.sin(2 * np.pi * 3 * f * t))

    decay = np.exp(-t / 0.22)
    attack = np.minimum(1.0, t / 0.004)

    return 0.050 * tone * decay * attack


def bass(midi, seconds):
    n = int(seconds * RATE)
    t = np.arange(n) / RATE
    f = hz(midi)

    # The octave above keeps it audible on laptop speakers, which will
    # not reproduce a 45 Hz fundamental at all
    tone = np.sin(2 * np.pi * f * t) + 0.45 * np.sin(2 * np.pi * 2 * f * t)

    return 0.075 * tone * env_adsr(n, 0.02, 0.25)


def kick():
    n = int(0.35 * RATE)
    t = np.arange(n) / RATE

    # A falling pitch is what makes a sine read as a drum
    freq = 48 + 62 * np.exp(-t / 0.045)
    phase = 2 * np.pi * np.cumsum(freq) / RATE

    return 0.11 * np.sin(phase) * np.exp(-t / 0.13)


def shaker(rng):
    n = int(0.07 * RATE)
    t = np.arange(n) / RATE

    noise = rng.standard_normal(n)

    # Differencing throws away the low end - a cheap high-pass
    noise = np.diff(np.concatenate([[0.0], noise]))

    return 0.012 * noise * np.exp(-t / 0.018)


def echo(bus, delay, feedback, repeats):
    """Ping-pong: each repeat on the opposite side, and quieter."""
    d = int(delay * RATE)
    out = bus.copy()

    gain = 1.0
    source = bus

    for r in range(1, repeats + 1):
        gain *= feedback

        shifted = np.zeros_like(bus)
        shifted[d * r:] = source[:bus.shape[0] - d * r]

        if r % 2 == 1:
            shifted = shifted[:, ::-1]

        out += gain * shifted

    return out


def compose(seconds):
    total = int(seconds * RATE)

    pads = np.zeros((total, 2))
    arps = np.zeros((total, 2))
    low = np.zeros((total, 2))

    rng = np.random.default_rng(7)

    bars = int(np.ceil(seconds / BAR))

    # The eight-bar breather in the middle, and the tail
    quiet_from = 8 * ((bars // 2) // 8)
    tail_from = bars - 8

    for bar in range(0, bars, 2):
        notes, arp, root = CHORDS[(bar // 2) % len(CHORDS)]

        start = bar * BAR

        # Two bars, plus overlap so the chords cross-fade
        l, r = pad(notes, 2 * BAR + 1.2)

        n = min(len(l), total - int(start * RATE))

        if n > 0:
            i = int(start * RATE)
            pads[i:i + n, 0] += l[:n]
            pads[i:i + n, 1] += r[:n]

        for step in range(16):
            b = bar + step // 8

            if b < 8 or b >= bars:
                continue

            # Up the chord and back, eighth notes
            pattern = [0, 1, 2, 3, 2, 1, 2, 1]
            note = arp[pattern[step % 8]]

            add(arps, start + step * BEAT / 2, pluck(note),
                pan=-0.35 if step % 2 == 0 else 0.35)

        for beat in (0, 2, 4, 6):
            b = bar + beat // 4

            if b < 16 or b >= bars:
                continue

            add(low, start + beat * BEAT, bass(root, 2 * BEAT))

            drums = not (quiet_from <= b < quiet_from + 8 or b >= tail_from)

            if drums:
                add(low, start + beat * BEAT, kick())

        for eighth in range(16):
            b = bar + eighth // 8

            if (b < 16 or b >= bars or
                    quiet_from <= b < quiet_from + 8 or b >= tail_from):
                continue

            if eighth % 2 == 1:
                add(low, start + eighth * BEAT / 2, shaker(rng),
                    pan=0.25)

    arps = echo(arps, 0.75 * BEAT, 0.38, 4)

    mix = pads + arps + low

    # Fade in and out over the whole piece
    t = np.arange(total) / RATE
    fade = np.minimum(1.0, t / 2.5) * np.minimum(1.0, (seconds - t) / 5.0)
    mix *= np.clip(fade, 0, 1)[:, None]

    # Gentle saturation, then a level that sits under reading
    mix = np.tanh(mix * 1.6)
    mix *= 0.62 / max(1e-9, np.max(np.abs(mix)))

    return mix


def main():
    seconds = float(sys.argv[1])
    out = sys.argv[2]

    mix = compose(seconds)

    pcm = (np.clip(mix, -1, 1) * 32767).astype(np.int16)

    with wave.open(out, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.ravel().tobytes())

    peak = np.max(np.abs(mix))
    rms = np.sqrt(np.mean(mix ** 2))

    print('%.2f s, peak %.1f dBFS, rms %.1f dBFS, dc %.5f, clipped %d' % (
        len(mix) / RATE,
        20 * np.log10(peak),
        20 * np.log10(rms),
        float(np.mean(mix)),
        int(np.sum(np.abs(mix) >= 0.999))))


if __name__ == '__main__':
    main()
