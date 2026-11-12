"""Bucle de tronco rodando (2.4 s): retumbe grave + golpes de madera por vuelta + tierra. Construido en forma circular."""
import subprocess, io, numpy as np, soundfile as sf
from scipy.signal import butter, sosfiltfilt, resample_poly
SR = 48000; L = int(2.4 * SR); rng = np.random.default_rng(3)
def leer(f):
    w = subprocess.run(['ffmpeg','-loglevel','error','-i','raw/'+f,'-ar','48000','-ac','1','-f','wav','-'],capture_output=True).stdout
    x, _ = sf.read(io.BytesIO(w)); return x
def filtro(x, lo=None, hi=None):
    if lo: x = sosfiltfilt(butter(2, lo, 'high', fs=SR, output='sos'), x)
    if hi: x = sosfiltfilt(butter(4, hi, 'low', fs=SR, output='sos'), x)
    return x
def poner(buf, x, t, g):
    i = int(t * SR) % L
    for j in range(len(x)):               # suma circular
        pass
    idx = (i + np.arange(len(x))) % L
    np.add.at(buf, idx, x * g)

vuelta = 0.4                                   # una vuelta del tronco cada 0.4 s (6 vueltas = bucle)
t = np.arange(L) / SR
# 1) retumbe: ruido marrón en banda 45-320 Hz (FFT = circular), modulado por la vuelta
ruido = np.cumsum(rng.standard_normal(L)); ruido -= np.linspace(ruido[0], ruido[-1], L)
F = np.fft.rfft(ruido); f = np.fft.rfftfreq(L, 1/SR)
F[(f < 45) | (f > 320)] = 0; F[(f >= 45) & (f <= 320)] *= np.hanning(((f >= 45) & (f <= 320)).sum())
retumbe = np.fft.irfft(F, L); retumbe /= np.abs(retumbe).max()
retumbe *= 0.75 + 0.25 * np.sin(2 * np.pi * t / vuelta)
buf = retumbe * 0.55

# 2) golpes de madera huecos, más graves (una vez por vuelta + uno suave a media vuelta)
golpes = [leer(f'kimp__impact_wood_heavy_00{i}.ogg') for i in range(3)] + [leer(f'kimp__impact_wood_light_00{i}.ogg') for i in range(2)]
golpes = [filtro(resample_poly(g, 10, 7), hi=1600) for g in golpes]        # bajar tono ~30 %
golpes = [g / np.abs(g).max() for g in golpes]
for k in range(6):
    poner(buf, golpes[k % 3], k * vuelta + rng.uniform(-0.015, 0.015), rng.uniform(0.55, 0.8))
    poner(buf, golpes[3 + k % 2], k * vuelta + vuelta / 2 + rng.uniform(-0.02, 0.02), rng.uniform(0.18, 0.28))

# 3) tierra crujiendo bajo el tronco
tierra = [filtro(leer(f'meta__FEETHmn_ShoeDirtRun_OS_0{i}.ogg'), lo=150, hi=2800) for i in range(1, 6)]
tierra = [g / np.abs(g).max() for g in tierra]
for k in range(12):
    poner(buf, tierra[k % 5], k * vuelta / 2 + 0.07 + rng.uniform(-0.03, 0.03), rng.uniform(0.12, 0.2))

buf = filtro(np.concatenate([buf[-SR:], buf, buf[:SR]]), lo=35, hi=5000)[SR:SR + L]   # filtrado circular
buf = buf / np.sqrt((buf ** 2).mean()) * 10 ** (-17 / 20)
buf = np.clip(buf, -0.85, 0.85)
est = np.stack([buf, np.roll(buf, 90)], 1)                                           # leve ancho estéreo
sf.write('/tmp/tr.wav', est, SR, subtype='PCM_16')
subprocess.run(['ffmpeg','-loglevel','error','-y','-i','/tmp/tr.wav','-c:a','libvorbis','-q:a','5','salida/Efectos/TroncoRodando.ogg'])
sp = np.abs(np.fft.rfft(buf)); print('RMS -17 dB, pico %.2f, centroide %d Hz, empalme %.4f, dur %.2f s' % (np.abs(buf).max(), (sp*f).sum()/sp.sum(), abs(buf[-1]-buf[0]), L/SR))
