import numpy as np, soundfile as sf, sys
from scipy.signal import fftconvolve, butter, lfilter

SR = 44100
rng = np.random.default_rng(11)
NOT = {'C':0,'C#':1,'D':2,'D#':3,'E':4,'F':5,'F#':6,'G':7,'G#':8,'A':9,'A#':10,'B':11}
def m(n): return 12*(int(n[-1])+1)+NOT[n[:-1]]
def hz(midi): return 440*2**((midi-69)/12)

def marimba(f, dur, vel):
    n = int((dur+0.6)*SR); t = np.arange(n)/SR
    x = (np.sin(2*np.pi*f*t)*np.exp(-t*5.5) + 0.35*np.sin(2*np.pi*f*3.93*t)*np.exp(-t*16)
         + 0.12*np.sin(2*np.pi*f*9.2*t)*np.exp(-t*40))
    x[:int(0.002*SR)] *= np.linspace(0,1,int(0.002*SR))
    return x*vel

def guitarra(f, dur, vel):
    n = int((dur+0.5)*SR); p = int(SR/f)
    buf = rng.uniform(-1,1,p); buf = lfilter([0.5,0.5],[1],buf)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % p]
        buf[i % p] = 0.996*0.5*(buf[i % p]+buf[(i+1) % p])
    fin = int(dur*SR)
    if fin < n: out[fin:] *= np.linspace(1,0,n-fin)**2
    return out*vel

def flauta(f, dur, vel):
    n = int((dur+0.15)*SR); t = np.arange(n)/SR
    vib = 1+0.004*np.sin(2*np.pi*5.2*t)*np.clip((t-0.15)*4,0,1)
    fase = 2*np.pi*f*np.cumsum(vib)/SR
    x = np.sin(fase)+0.18*np.sin(2*fase)+0.06*np.sin(3*fase)
    b,a = butter(2, 3000/(SR/2), 'low'); x += 0.03*lfilter(b,a,rng.standard_normal(n))
    env = np.minimum(1, t/0.05)*np.where(t>dur, np.exp(-(t-dur)*25), 1)
    return x*env*vel

def bajo(f, dur, vel):
    n = int((dur+0.1)*SR); t = np.arange(n)/SR
    x = np.sin(2*np.pi*f*t)+0.25*np.sin(4*np.pi*f*t)
    env = np.minimum(1,t/0.01)*np.exp(-t*2.2)*np.where(t>dur, np.exp(-(t-dur)*30),1)
    return x*env*vel

def shaker(vel):
    n = int(0.09*SR); t = np.arange(n)/SR
    b,a = butter(2, [5000/(SR/2), 12000/(SR/2)], 'band')
    return lfilter(b,a,rng.standard_normal(n))*np.exp(-t*55)*vel

def bombo(vel):
    n = int(0.25*SR); t = np.arange(n)/SR
    f = 110*np.exp(-t*18)+48
    return np.sin(2*np.pi*np.cumsum(f)/SR)*np.exp(-t*14)*vel

def poner(buf, x, t, pan=0.0):
    i = int(max(0,t+rng.uniform(-0.004,0.004))*SR); j = min(len(buf), i+len(x))
    L = np.cos((pan+1)*np.pi/4); R = np.sin((pan+1)*np.pi/4)
    buf[i:j,0] += x[:j-i]*L; buf[i:j,1] += x[:j-i]*R

def reverb(buf, seg=1.8, mezcla=0.22):
    n = int(seg*SR); t = np.arange(n)/SR
    ir = rng.standard_normal((n,2))*np.exp(-t/(seg/6.5))[:,None]; ir[:int(0.01*SR)] = 0
    ir = np.apply_along_axis(lambda c: np.convolve(c, np.ones(16)/16, 'same'), 0, ir)
    ir /= np.sqrt((ir**2).sum(0))
    mojado = np.stack([fftconvolve(buf[:,c], ir[:,c])[:len(buf)] for c in range(2)], 1)
    return buf*(1-mezcla)+mojado*mezcla*2.0


def pad(f, dur, vel):
    """Colchón suave (varias sinusoides levemente desafinadas), entrada y salida lentas."""
    n = int((dur+1.0)*SR); t = np.arange(n)/SR
    x = sum(np.sin(2*np.pi*f*d*t + rng.uniform(0, 6)) for d in (0.997, 1.0, 1.003)) / 3
    x += 0.25*np.sin(2*np.pi*f*2*t)
    env = np.minimum(1, t/0.8)*np.where(t > dur, np.exp(-(t-dur)*3), 1)
    return x*env*vel

def kalimba(f, dur, vel):
    n = int((dur+1.2)*SR); t = np.arange(n)/SR
    x = np.sin(2*np.pi*f*t)*np.exp(-t*3.2) + 0.25*np.sin(2*np.pi*f*5.4*t)*np.exp(-t*22)
    x[:int(0.003*SR)] *= np.linspace(0, 1, int(0.003*SR))
    return x*vel

def cerrar(buf, largo, pico=0.72):
    n = int(largo*SR); out = buf[:n].copy(); cola = buf[n:]; out[:len(cola)] += cola
    return np.tanh(out/(np.abs(out).max()+1e-9)*1.3)/np.tanh(1.3)*pico
