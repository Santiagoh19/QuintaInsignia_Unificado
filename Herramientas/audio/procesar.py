"""Convierte los sonidos CC0 descargados a OGG con nombres del juego, recortados y normalizados."""
import subprocess, io, os, numpy as np, soundfile as sf
R = 'raw/'
EF = {  # nombre en el juego: (archivo, ganancia relativa)
 'Paso_1':'meta__FEETHmn_ShoeGrassWalk_OS_01.ogg','Paso_2':'meta__FEETHmn_ShoeGrassWalk_OS_02.ogg','Paso_3':'meta__FEETHmn_ShoeGrassWalk_OS_03.ogg',
 'Paso_4':'meta__FEETHmn_ShoeGrassWalk_OS_04.ogg','Paso_5':'meta__FEETHmn_ShoeGrassWalk_OS_05.ogg',
 'Correr_1':'meta__FEETHmn_ShoeDirtRun_OS_01.ogg','Correr_2':'meta__FEETHmn_ShoeDirtRun_OS_02.ogg','Correr_3':'meta__FEETHmn_ShoeDirtRun_OS_03.ogg',
 'Correr_4':'meta__FEETHmn_ShoeDirtRun_OS_04.ogg','Correr_5':'meta__FEETHmn_ShoeDirtRun_OS_05.ogg',
 'CambioCarril_1':'meta__SWSH_Swoosh_OS_01.ogg','CambioCarril_2':'meta__SWSH_Swoosh_OS_02.ogg',
 'TroncoAparece':'kimp__impact_wood_heavy_002.ogg','TroncoGolpe':('kimp__impact_wood_heavy_001.ogg','kimp__impact_punch_heavy_000.ogg'),
 'CorazonMenos':'meta__UIAlert_CasualMusicalError_OS_01.ogg',
 'Hachazo':'meta__WEAPAxe_WoodCutAxe_OS_01.ogg','Hachazo_2':'krpg__chop.ogg','LenaCae':'meta__WOODImpt_LargeWoodDrop_OS_01.ogg',
 'LenaGolpe':'kimp__impact_wood_light_000.ogg','Punto':'wyz__pop-medium-bubble.wav','Recoger':'meta__OBJBag_KnapsackNylonOpen_OS_01.ogg',
 'Tela':'krpg__cloth_1.ogg','ArmarCana':'meta__MAGSpel_MagicSpell_OS_01.ogg','Destello':'wyz__shimmer-medium-sparkle.wav',
 'Lanzar':'meta__WHSH_WhooshMedium_OS_01.ogg','Chapoteo':'wyz__props-splash.wav','Gota':'wyz__props-drip.wav',
 'Pica':'wyz__notification-attention-clear.wav','Carrete':'wyz__mechanical-ratchet-light.wav',
 'PezAtrapado':'meta__MUSCStngr_VictoryMallet_OS_01.ogg','PezEscapa':'meta__MUSCStngr_LossMallet_OS_01.ogg',
 'MisionCompleta':'meta__MUSCStngr_VictoryOrchestral_OS_01.ogg','Final':'meta__MUSCStngr_VictoryOrchestral_OS_03.ogg',
 'SinVidas':'meta__MUSCStngr_LossOrchestral_OS_01.ogg','Materiales':'meta__MUSCStngr_LevelUpMallet_OS_01.ogg',
 'Boton':'meta__UIClick_CasualMusicalConfirm_OS_01.ogg','Aviso':'meta__UIClick_CasualMusicalOpen_OS_01.ogg',
 'Record':'wyz__notification-success-sparkle.wav','Reaccion':'wyz__notification-success-pluck.wav',
 'Aparecer':'wyz__appear-poof.wav','Alejarse':'wyz__whoosh-slow-soft.wav',
}
AMB = {'NocheBosque':'meta__AMBForst_Night_LOOP_01.ogg','Lago':'meta__AMBLake_LakeShore_LOOP_01.ogg','AguaOrilla':'meta__WATRLap_WaterLapping_LOOP_01.ogg'}

def leer(f):
    wav = subprocess.run(['ffmpeg','-loglevel','error','-i',R+f,'-ar','48000','-f','wav','-'],capture_output=True).stdout
    x, sr = sf.read(io.BytesIO(wav), always_2d=True); return x
def escribir(x, ruta):
    tmp = ruta.replace('.ogg','.wav'); sf.write(tmp, np.clip(x,-1,1), 48000, subtype='PCM_16')
    subprocess.run(['ffmpeg','-loglevel','error','-y','-i',tmp,'-c:a','libvorbis','-q:a','5',ruta]); os.remove(tmp)

os.makedirs('salida/Efectos',exist_ok=True); os.makedirs('salida/Ambientes',exist_ok=True)
for nombre, f in EF.items():
    if isinstance(f, tuple):
        a, b = leer(f[0]), leer(f[1]); n = max(len(a), len(b))
        x = np.zeros((n, 2)); x[:len(a)] += a.mean(1,keepdims=True)*np.ones(2); x[:len(b)] += 0.6*b.mean(1,keepdims=True)*np.ones(2)
    else:
        x = leer(f)
        if x.shape[1] == 1: x = np.repeat(x, 2, 1)
    env = np.abs(x).max(1); umbral = env.max() * 0.02
    ini = max(0, np.argmax(env > umbral) - 240)                    # recorta silencio inicial
    fin = len(env) - np.argmax(env[::-1] > umbral * 0.5)
    x = x[ini:min(len(x), fin + 4800)]
    x = x / (np.abs(x).max() + 1e-9) * 0.89                        # -1 dBFS
    escribir(x, f'salida/Efectos/{nombre}.ogg')
for nombre, f in AMB.items():
    x = leer(f)
    if x.shape[1] == 1: x = np.repeat(x, 2, 1)
    rms = np.sqrt((x**2).mean()); x = x * (10**(-20/20) / rms)       # todos al mismo volumen (-20 dB RMS)
    x = np.clip(x, -0.95, 0.95)
    escribir(x, f'salida/Ambientes/{nombre}.ogg')
for d in ('Efectos','Ambientes','Musica'):
    fs = sorted(os.listdir('salida/'+d)); print(d, len(fs))
    for f in fs: print('  %-18s %5.2f s' % (f, sf.info(f'salida/{d}/{f}').duration))
