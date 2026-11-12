"""Música del nivel 1 (Arma tu carpa): tema alegre de día, Sol mayor, 104 ppm.
Síntesis propia (marimba, guitarra Karplus-Strong, flauta, bajo, shaker), sin muestras externas.
Sale como bucle sin cortes (la cola de reverberación se suma al inicio)."""
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

# ---------------------------------------------------------------- partitura
bpm = 104; beat = 60/bpm; c8 = beat/2
ACORDES = {'G':('G2',['G3','B3','D4','G4']), 'C':('C3',['G3','C4','E4','G4']), 'D':('D3',['A3','D4','F#4','A4']),
           'Em':('E2',['G3','B3','E4','G4']), 'Am':('A2',['A3','C4','E4','A4']), 'D7':('D3',['A3','C4','F#4','A4'])}
A = ['G','C','G','D', 'G','C','Am','D', 'Em','C','G','D', 'C','D','G','G']
B = ['C','D','G','Em', 'C','D','G','G', 'Am','D7','G','Em', 'C','D7','G','G']
prog = A + B
# melodía (nota, inicio en tiempos, duración en tiempos) por compás
melA = [[('D5',0,1),('B4',1,.5),('D5',1.5,.5),('G5',2,1.5),('F#5',3.5,.5)], [('E5',0,1),('G5',1,1),('E5',2,1),('C5',3,1)],
        [('D5',0,1.5),('B4',1.5,.5),('G4',2,1),('B4',3,1)], [('A4',0,3),('D5',3,1)],
        [('D5',0,1),('B4',1,.5),('D5',1.5,.5),('G5',2,1.5),('A5',3.5,.5)], [('G5',0,1),('E5',1,1),('C5',2,1),('E5',3,1)],
        [('C5',0,1),('E5',1,1),('A5',2,1),('G5',3,1)], [('F#5',0,3),('A4',3,1)],
        [('B4',0,1),('E5',1,1),('G5',2,1.5),('E5',3.5,.5)], [('E5',0,1),('C5',1,1),('E5',2,1),('G5',3,1)],
        [('D5',0,1),('G5',1,1),('B5',2,1),('A5',3,1)], [('A5',0,2),('F#5',2,1),('D5',3,1)],
        [('E5',0,1),('G5',1,1),('C6',2,1),('B5',3,.5),('A5',3.5,.5)], [('A5',0,1),('F#5',1,1),('D5',2,1),('F#5',3,1)],
        [('G5',0,4)], []]
melB = [[('E5',0,.5),('F#5',.5,.5),('G5',1,1),('E5',2,1),('C5',3,1)], [('D5',0,.5),('E5',.5,.5),('F#5',1,1),('A5',2,2)],
        [('G5',0,1),('D5',1,1),('B4',2,1),('D5',3,1)], [('E5',0,3),('B4',3,1)],
        [('C5',0,.5),('D5',.5,.5),('E5',1,1),('G5',2,1),('E5',3,1)], [('F#5',0,1),('A5',1,1),('F#5',2,1),('D5',3,1)],
        [('G5',0,2),('B5',2,2)], [('A5',0,2),('G5',2,2)],
        [('A5',0,1),('E5',1,1),('C5',2,1),('E5',3,1)], [('F#5',0,1),('A5',1,1),('C6',2,1),('A5',3,1)],
        [('B5',0,1),('G5',1,1),('D5',2,1),('G5',3,1)], [('E5',0,1),('G5',1,1),('B5',2,2)],
        [('C6',0,1),('B5',1,1),('A5',2,1),('G5',3,1)], [('F#5',0,1),('E5',1,1),('D5',2,1),('F#5',3,1)],
        [('G5',0,3),('D5',3,1)], [('G5',0,2)]]
mel = melA + melB

largo = len(prog)*4*beat
buf = np.zeros((int((largo+3)*SR), 2))
patron = [0,2,1,3,2,1,3,2]   # arpegio de guitarra en corcheas
for b, ac in enumerate(prog):
    t0 = b*4*beat
    raiz, voces = ACORDES[ac]
    # bajo: tiempo 1 y 3 (+ quinta)
    poner(buf, bajo(hz(m(raiz)), 1.6*beat, 0.55), t0, -0.05)
    poner(buf, bajo(hz(m(raiz)+7 if m(raiz)+7 < 55 else m(raiz)-5), 1.6*beat, 0.45), t0+2*beat, -0.05)
    # guitarra arpegiada
    for i, idx in enumerate(patron):
        v = 0.30 if i % 2 == 0 else 0.22
        poner(buf, guitarra(hz(m(voces[idx])), c8*2.5, v), t0+i*c8, -0.35)
    # marimba: acordes en contratiempo (2 y 4)
    for k in (1, 3):
        for nn in voces[1:3]:
            poner(buf, marimba(hz(m(nn)+12), 0.3, 0.13), t0+k*beat, 0.3)
    # percusión suave
    for i in range(8):
        poner(buf, shaker(0.07 if i % 2 else 0.045), t0+i*c8, 0.45)
    poner(buf, bombo(0.33), t0); poner(buf, bombo(0.22), t0+2*beat)
    # melodía: primera vuelta marimba, segunda flauta (con marimba suave doblando)
    for nota, ini, dur in mel[b]:
        f = hz(m(nota))
        if b < 16:
            poner(buf, marimba(f, dur*beat, 0.42), t0+ini*beat, 0.1)
        else:
            poner(buf, flauta(f, dur*beat*0.92, 0.26), t0+ini*beat, 0.1)
            poner(buf, marimba(f, dur*beat, 0.14), t0+ini*beat, 0.15)

buf = reverb(buf)
n = int(largo*SR); out = buf[:n].copy(); cola = buf[n:]; out[:len(cola)] += cola
out = np.tanh(out/(np.abs(out).max()+1e-9)*1.3)/np.tanh(1.3)*0.72
sf.write(sys.argv[1], out, SR, subtype='PCM_16')
print(round(len(out)/SR,1), 's')
