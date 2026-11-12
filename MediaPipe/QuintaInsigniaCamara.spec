# -*- mode: python ; coding: utf-8 -*-
from PyInstaller.utils.hooks import collect_data_files
from PyInstaller.utils.hooks import collect_dynamic_libs

datas = []
binaries = []
datas += collect_data_files('mediapipe')
binaries += collect_dynamic_libs('mediapipe')


a = Analysis(
    ['send_pose_udp.py'],
    pathex=[],
    binaries=binaries,
    datas=datas,
    hiddenimports=['mediapipe.python.solutions.pose', 'mediapipe.python.solutions.hands'],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=['tkinter', 'IPython', 'jupyter'],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='QuintaInsigniaCamara',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name='QuintaInsigniaCamara',
)
