"""Golpes sordos de tronco rodando (uno por vuelta) y el 'toc' al aparecer. Madera pesada sobre tierra."""
import subprocess, io, os, numpy as np, soundfile as sf
from scipy.signal import butter, sosfiltfilt, resample_poly
SR = 48000; rng = np.random.default_rng(11)
def leer(f):
    w = subprocess.run(['ffmpeg','-loglevel','error','-i','raw/'+f,'-ar','48000','-ac','1','-f','wav','-'],capture_output=True).stdout
    return sf.read(io.BytesIO(w))[0]
def filt(x, lo=None, hi=None):
    if lo: x = sosfiltfilt(butter(2, lo, 'high', fs=SR, output='sos'), x)
    if hi: x = sosfiltfilt(butter(4, hi, 'low', fs=SR, output='sos'), x)
    return x
def recortar(x, seg):
    n = int(seg * SR); x = x[:n].copy()
    if len(x) < n: x = np.pad(x, (0, n - len(x)))
    f = int(0.08 * SR); x[-f:] *= np.linspace(1, 0, f) ** 2; return x
def guardar(x, nombre, pico=0.85):
    x = x / np.abs(x).max() * pico
    sf.write('/tmp/g.wav', np.stack([x, x], 1), SR, subtype='PCM_16')
    subprocess.run(['ffmpeg','-loglevel','error','-y','-i','/tmp/g.wav','-c:a','libvorbis','-q:a','5',f'salida/Efectos/{nombre}.ogg'])
def bajar(x, factor): return resample_poly(x, int(100 * factor), 100)   # factor>1 = más grave y largo

pesados = [leer(f'kimp__impact_wood_heavy_00{i}.ogg') for i in range(3)]
tierra = [leer(f'meta__FEETHmn_ShoeDirtRun_OS_0{i}.ogg') for i in range(1, 6)]
for k in range(4):
    g = filt(bajar(pesados[k % 3], 1.3 + 0.08 * k), lo=120, hi=1800)        # madera grave y sorda
    g = g / np.abs(g).max()
    t = filt(tierra[k], lo=250, hi=2400); t = t / np.abs(t).max() * 0.3   # tierra aplastada
    n = max(len(g), len(t)); x = np.zeros(n); x[:len(g)] += g; x[:len(t)] += t
    # cuerpo grave corto (el peso del tronco)
    tt = np.arange(int(0.18 * SR)) / SR
    cuerpo = np.sin(2 * np.pi * (150 - 50 * tt / 0.18) * tt) * np.exp(-tt / 0.04) * 0.35
    x[:len(cuerpo)] += cuerpo
    x = sosfiltfilt(butter(4, 130, 'high', fs=SR, output='sos'), x)   # sin subgraves: se oye en parlantes de portátil
    guardar(recortar(x, 0.34), f'TroncoGira_{k+1}')
# 'toc' al aparecer: madera clara, corta y suave (se oye lejos)
toc = filt(bajar(leer('kimp__impact_wood_light_001.ogg'), 1.15), lo=150, hi=2500)
guardar(recortar(toc, 0.3), 'TroncoAparece', pico=0.7)
for f in sorted(os.listdir('salida/Efectos')):
    if f.startswith('Tronco'): print(f, round(sf.info('salida/Efectos/' + f).duration, 2))
