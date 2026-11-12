"""Música de los niveles 4 (Pinzas de fuego) y 5 (Sombras en la carpa). Síntesis propia, sin muestras.
Uso: python musica_niveles.py <carpeta_salida>   →  MusicaFogata.wav, MusicaSombras.wav"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from instrumentos import *

def fogata():
    """Noche junto al fuego: guitarra arpegiada en 6/8, flauta tranquila, Re mayor, 66 ppm (negra con punto)."""
    bpm = 66; pulso = 60/bpm; c8 = pulso/3          # compás de 6/8: dos pulsos de tres corcheas
    AC = {'D':('D2',['D3','A3','D4','F#4','A4']), 'G':('G2',['D3','G3','B3','D4','G4']), 'A':('A2',['E3','A3','C#4','E4','A4']),
          'Bm':('B2',['F#3','B3','D4','F#4','B4']), 'Em':('E2',['E3','B3','E4','G4','B4']), 'F#m':('F#2',['C#3','F#3','A3','C#4','F#4'])}
    prog = ['D','D','G','D','Bm','G','A','A', 'D','F#m','G','D','Em','A','D','D'] * 2
    mel = {2:[('B4',0,2),('A4',2,1),('G4',3,3)], 3:[('F#4',0,3),('A4',3,3)], 4:[('B4',0,2),('D5',2,1),('F#5',3,3)],
           5:[('E5',0,3),('D5',3,2),('B4',5,1)], 6:[('C#5',0,3),('E5',3,3)], 7:[('A4',0,6)],
           10:[('B4',0,2),('D5',2,1),('G5',3,3)], 11:[('F#5',0,3),('D5',3,3)], 12:[('E5',0,2),('G5',2,1),('B5',3,3)],
           13:[('A5',0,3),('C#5',3,3)], 14:[('D5',0,6)],
           18:[('D5',0,2),('B4',2,1),('G4',3,3)], 19:[('A4',0,3),('F#4',3,3)], 20:[('F#4',0,2),('B4',2,1),('D5',3,3)],
           21:[('B4',0,3),('G4',3,3)], 22:[('E4',0,3),('A4',3,3)], 23:[('C#5',0,6)],
           26:[('G5',0,2),('F#5',2,1),('E5',3,3)], 27:[('D5',0,3),('A4',3,3)], 28:[('G4',0,2),('B4',2,1),('E5',3,3)],
           29:[('C#5',0,3),('E5',3,3)], 30:[('D5',0,6)]}
    patron = [0,2,3,4,3,2]
    largo = len(prog)*6*c8
    buf = np.zeros((int((largo+3)*SR), 2))
    for b, ac in enumerate(prog):
        t0 = b*6*c8; raiz, v = AC[ac]
        poner(buf, bajo(hz(m(raiz)), 5.5*c8, 0.42), t0, -0.05)
        for i, idx in enumerate(patron):
            poner(buf, guitarra(hz(m(v[idx])), c8*4, 0.30 if i == 0 else 0.20), t0+i*c8, -0.3)
        if b % 2 == 1: poner(buf, shaker(0.035), t0+3*c8, 0.4)
        for nota, ini, dur in mel.get(b, []):
            poner(buf, flauta(hz(m(nota)), dur*c8*0.95, 0.24), t0+ini*c8, 0.15)
    return cerrar(reverb(buf, 2.4, 0.28), largo)

def sombras():
    """Sombras en la carpa: colchón suave y kalimba en Do pentatónico (las mismas notas de las señas,
    así nunca chocan), 60 ppm, sin percusión, con mucho espacio para escuchar las sílabas."""
    bpm = 60; beat = 60/bpm
    AC = {'C':['C3','G3','E4'], 'Am':['A2','E3','C4'], 'F':['F2','C3','A3'], 'G':['G2','D3','B3'], 'Em':['E2','B2','G3']}
    prog = ['C','Am','F','C','C','Em','F','G'] * 2
    figuras = [[('E5',0),('G5',1),('A5',2.5)], [('C5',0),('E5',1.5)], [('A4',0),('C5',1),('D5',2)], [('G4',0),('E5',2)],
               [('G5',0),('E5',1),('D5',2)], [('E5',0),('G4',1.5)], [('A4',0),('C5',1),('A5',3)], [('G5',0),('D5',1),('A4',2)]]
    largo = len(prog)*4*beat
    buf = np.zeros((int((largo+4)*SR), 2))
    for b, ac in enumerate(prog):
        t0 = b*4*beat
        for k, nn in enumerate(AC[ac]):
            poner(buf, pad(hz(m(nn)), 4*beat, 0.16 if k == 0 else 0.10), t0, -0.2+0.2*k)
        fig = figuras[b % 8]
        if b >= 8: fig = [(n, i+0.5) for n, i in fig[:2]]   # segunda vuelta más quieta
        for nota, ini in fig:
            poner(buf, kalimba(hz(m(nota)), 1.5, 0.22), t0+ini*beat, 0.25)
    return cerrar(reverb(buf, 3.2, 0.36), largo, 0.6)

if __name__ == '__main__':
    out = sys.argv[1] if len(sys.argv) > 1 else '.'
    for nombre, f in (('MusicaFogata', fogata), ('MusicaSombras', sombras)):
        x = f(); sf.write(os.path.join(out, nombre+'.wav'), x, SR, subtype='PCM_16'); print(nombre, round(len(x)/SR, 1), 's')
