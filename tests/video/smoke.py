"""Three seconds through mp4enc, to prove the pipe before any real work.

The frame has TOP written in its top-left corner - an upside-down
encode is the classic Media Foundation RGB32 mistake, and this makes it
obvious at a glance - and a square that moves, so a stuck or repeated
frame shows too. The sound is a plain 440 Hz tone.
"""

import os
import struct
import subprocess
import wave

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ENC = os.path.join(HERE, 'mp4enc', 'bin', 'Release', 'net8.0-windows',
                   'mp4enc.exe')

W, H, FPS, SECONDS = 1920, 1080, 25, 3
FRAMES = FPS * SECONDS
RATE = 44100


def tone(path):
    t = np.arange(RATE * SECONDS) / RATE
    s = (0.3 * np.sin(2 * np.pi * 440 * t) * 32767).astype(np.int16)
    stereo = np.column_stack([s, s]).ravel()

    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(stereo.tobytes())


def main():
    wav = os.path.join(HERE, 'smoke.wav')
    out = os.path.join(HERE, 'smoke.mp4')

    tone(wav)

    font = ImageFont.truetype('segoeuib.ttf', 120)

    proc = subprocess.Popen(
        [ENC, out, str(W), str(H), str(FPS), str(FRAMES), wav],
        stdin=subprocess.PIPE)

    for i in range(FRAMES):
        img = Image.new('RGB', (W, H), (30, 60, 110))
        d = ImageDraw.Draw(img)

        d.text((40, 20), 'TOP', font=font, fill=(255, 255, 255))

        x = 200 + int(1400 * i / (FRAMES - 1))
        d.rectangle((x, 600, x + 160, 760), fill=(230, 120, 40))

        rgba = np.asarray(img.convert('RGBA'))
        bgra = rgba[..., [2, 1, 0, 3]]

        proc.stdin.write(np.ascontiguousarray(bgra).tobytes())

    proc.stdin.close()
    code = proc.wait()

    print('exit', code, 'size', os.path.getsize(out) if os.path.exists(out) else 0)


if __name__ == '__main__':
    main()
