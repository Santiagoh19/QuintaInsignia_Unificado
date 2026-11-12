"""Compone y renderiza la música de Quinta Insignia con muestras CC0 (Meta Piano / Meta Nylon Guitar).
Salida: bucles sin cortes (la cola de reverberación se suma al inicio)."""
import numpy as np, soundfile as sf, random
from scipy.signal import fftconvolve, resample_poly

SR = 48000
random.seed(7); np.random.seed(7)
NOTAS = {'C':0,'C#':1,'Db':1,'D':2,'D#':3,'Eb':3,'E':4,'F':5,'F#':6,'Gb':6,'G':7,'G#':8,'Ab':8,'A':9,'A#':10,'Bb':10,'B':11}
def m(n):  # 'F#5' -> midi
    nombre, oct_ = n[:-1], int(n[-1]); return 12 * (oct_ + 1) + NOTAS[nombre]

_cache = {}
def muestra(inst, midi):
    key = (inst, midi)
    if key not in _cache:
        rango = range(36, 89) if inst == 'piano' else range(40, 84)
        cerca = min(rango, key=lambda k: abs(k - midi))
        x, sr = sf.read(f'inst/{"piano" if inst=="piano" else "guitarra"}/{cerca}.ogg', always_2d=True)
        x = x.mean(1) if x.shape[1] > 1 else x[:, 0]
        if sr != SR: x = resample_poly(x, SR, sr)
        if cerca != midi:  # transponer por remuestreo
            r = 2 ** ((midi - cerca) / 12); idx = np.arange(0, len(x) - 1, r)
            x = np.interp(idx, np.arange(len(x)), x)
        _cache[key] = x / (np.abs(x).max() + 1e-9)
    return _cache[key]

def tocar(buf, inst, midi, t, dur, vel, pan=0.0, liberar=0.25):
    x = muestra(inst, midi)
    n = min(len(x), int((dur + liberar) * SR))
    x = x[:n].copy()
    fin = int(dur * SR)
    if fin < n:  # apagar suavemente al soltar
        x[fin:] *= np.linspace(1, 0, n - fin) ** 2
    t += random.uniform(-0.008, 0.008)
    i = max(0, int(t * SR)); x *= vel * random.uniform(0.92, 1.05)
    L = np.cos((pan + 1) * np.pi / 4); R = np.sin((pan + 1) * np.pi / 4)
    j = min(len(buf), i + len(x))
    buf[i:j, 0] += x[:j - i] * L; buf[i:j, 1] += x[:j - i] * R

def reverb(buf, seg=2.4, mezcla=0.28):
    n = int(seg * SR); t = np.arange(n) / SR
    ir = np.random.randn(n, 2) * np.exp(-t / (seg / 6.5))[:, None]
    ir[:int(0.012 * SR)] = 0
    # suavizar (bosque de noche, sin brillo metálico)
    ir = np.apply_along_axis(lambda c: np.convolve(c, np.ones(24) / 24, 'same'), 0, ir)
    ir /= np.sqrt((ir ** 2).sum(0))
    mojado = np.stack([fftconvolve(buf[:, c], ir[:, c])[:len(buf)] for c in range(2)], 1)
    return buf * (1 - mezcla) + mojado * mezcla * 2.2

def cerrar_bucle(buf, largo):
    out = buf[:largo].copy(); cola = buf[largo:]
    out[:len(cola)] += cola
    return out

def normalizar(buf, pico=0.70):
    return buf / (np.abs(buf).max() + 1e-9) * pico

# ------------------------------------------------------------------ 1) Inicio: vals de piano en Re mayor
def tema_inicio():
    bpm = 76; beat = 60 / bpm; compas = 3
    acordes = ['D','Bm','G','A','D','F#m','G','A','Bm','G','D','A','G','A','D','D']
    voces = {'D':('D2',['A3','D4','F#4']), 'Bm':('B1',['F#3','B3','D4']), 'G':('G2',['D4','G4','B3']),
             'A':('A2',['E4','A3','C#4']), 'F#m':('F#2',['C#4','F#3','A3'])}
    mel = [[('F#5',0,2),('E5',2,1)], [('D5',0,2),('B4',2,1)], [('D5',0,1),('E5',1,1),('F#5',2,1)], [('E5',0,3)],
           [('F#5',0,1),('A5',1,1),('F#5',2,1)], [('E5',0,2),('C#5',2,1)], [('D5',0,1),('B4',1,1),('D5',2,1)], [('E5',0,2),('A4',2,1)],
           [('F#5',0,2),('E5',2,1)], [('D5',0,1),('E5',1,1),('D5',2,1)], [('A4',0,2),('D5',2,1)], [('C#5',0,3)],
           [('B4',0,1),('D5',1,1),('G5',2,1)], [('F#5',0,1),('E5',1,1),('C#5',2,1)], [('D5',0,3)], [('A4',2,1)]]
    largo = len(acordes) * compas * beat
    buf = np.zeros((int((largo + 4) * SR), 2))
    for b, ac in enumerate(acordes):
        t0 = b * compas * beat
        bajo, arriba = voces[ac]
        tocar(buf, 'piano', m(bajo) + (12 if m(bajo) < 40 else 0), t0, compas * beat * 0.95, 0.55, pan=-0.15)
        tocar(buf, 'piano', m(arriba[0]), t0 + beat, beat * 0.9, 0.30, pan=-0.05)
        tocar(buf, 'piano', m(arriba[1]), t0 + 1.5 * beat, beat * 0.9, 0.24, pan=0.05)
        tocar(buf, 'piano', m(arriba[2]), t0 + 2 * beat, beat * 0.9, 0.28, pan=0.0)
        for nota, ini, dur in mel[b]:
            tocar(buf, 'piano', m(nota), t0 + ini * beat, dur * beat, 0.62, pan=0.1)
    buf = reverb(buf, 2.8, 0.32)
    return normalizar(cerrar_bucle(buf, int(largo * SR)))

# ------------------------------------------------------------------ 2) Juego: guitarra arpegiada en La menor + piano
def tema_juego():
    bpm = 88; beat = 60 / bpm; corchea = beat / 2
    prog = ['Am','F','C','G','Am','F','C','E','F','G','Em','Am','F','G','Am','E']
    voces = {'Am':(45,[57,60,64]), 'F':(41,[57,60,65]), 'C':(48,[55,60,64]), 'G':(43,[59,62,67]),
             'E':(40,[56,59,64]), 'Em':(40,[55,59,64])}
    patron = [0, 1, 2, 3, 2, 1, 3, 2]
    mel = {8:[('A5',0,1.5),('G5',1.5,0.5),('F5',2,2)], 9:[('G5',0,1),('D5',1,1),('B4',2,2)],
           10:[('E5',0,1.5),('G5',1.5,0.5),('B5',2,2)], 11:[('A5',0,3)],
           12:[('C6',0,1),('A5',1,1),('F5',2,2)], 13:[('B5',0,1),('G5',1,1),('D5',2,2)],
           14:[('C5',0,1),('E5',1,1),('A5',2,2)], 15:[('G#5',0,2),('B5',2,1),('E5',3,1)]}
    largo = len(prog) * 4 * beat
    buf = np.zeros((int((largo + 4) * SR), 2))
    for b, ac in enumerate(prog):
        t0 = b * 4 * beat
        bajo, arriba = voces[ac]
        notas = [bajo] + arriba
        for i, idx in enumerate(patron):
            vel = 0.62 if i == 0 else (0.42 if i % 2 == 0 else 0.34)
            dur = 4 * beat if i == 0 else corchea * 3
            tocar(buf, 'guitarra', notas[idx], t0 + i * corchea, dur, vel, pan=-0.25 + 0.1 * (idx - 1.5))
        for nota, ini, dur in mel.get(b, []):
            tocar(buf, 'piano', m(nota), t0 + ini * beat, dur * beat, 0.42, pan=0.25)
    buf = reverb(buf, 2.2, 0.26)
    return normalizar(cerrar_bucle(buf, int(largo * SR)))

if __name__ == '__main__':
    import os; os.makedirs('salida/Musica', exist_ok=True)
    sf.write('salida/Musica/MusicaInicio.wav', tema_inicio(), SR, subtype='PCM_16')
    sf.write('salida/Musica/MusicaJuego.wav', tema_juego(), SR, subtype='PCM_16')
    for f in ('MusicaInicio', 'MusicaJuego'):
        info = sf.info(f'salida/Musica/{f}.wav'); print(f, round(info.duration, 1), 's')
