from pathlib import Path
import shutil,zipfile,json,hashlib
root=Path.cwd();art=root/'Artifacts/TelltaleGameBuild';name='HELIX-Telltale-Engraving-2026-11-05';release=art/'Release'/name
release.mkdir(parents=True,exist_ok=True)
source=root/'Builds/TelltaleEngraving'
for file in source.rglob('*'):
 if not file.is_file() or any('DoNotShip' in part for part in file.parts) or file.suffix=='.pdb':continue
 dest=release/file.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,dest)
(release/'LEEME.txt').write_text('''HELIX - TELLTALE ENGRAVING | Windows 64 bits | 05/11/2026

Abre HELIX-Telltale.exe dentro de esta carpeta.
El juego arranca en Laboratorio y permite continuar por los pisos de la torre.
Esta edicion aplica Telltale Engraving a escenario, armas y enemigos mediante
materiales 3D: iluminacion por bandas, rayado de tinta y contornos geometricos.
Revision: rayado mas marcado y con tamano coherente entre escenario y modelos.
Conserva los colores, texturas y emisiones. Risograph esta desactivado.
El HUD, los textos y las particulas conservan sus materiales.

CONTROLES
WASD: moverse. Raton: mirar. Clic izquierdo: disparar.
Clic derecho: apuntar. R: recargar. Q: cambiar de arma.
Espacio: saltar. E: interactuar. Escape: menu/liberar el cursor.
Las instrucciones y opciones especificas aparecen en el HUD.
Puedes cerrar la ventana con Alt+F4.

PARA COPIAR O COMPARTIR
Conserva toda la carpeta, incluidos HELIX-Telltale_Data, MonoBleedingEdge
y UnityPlayer.dll. Si usas el ZIP, extrae todo antes de abrir el ejecutable.
No hace falta instalar Unity.

Comprobado: arranque, renderizado y aplicacion de materiales en el ejecutable.
No sustituye una partida completa para valorar rendimiento y legibilidad.
''',encoding='utf-8')
manifest={str(p.relative_to(release)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest() for p in release.rglob('*') if p.is_file()}
(art/'release-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
archive=art/'Release'/(name+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for file in sorted(release.rglob('*')):
  if file.is_file():z.write(file,file.relative_to(release.parent))
with zipfile.ZipFile(archive) as z:
 assert z.testzip() is None
 dest=art/'ExtractedCheck';dest.mkdir(exist_ok=True);z.extractall(dest)
extracted=dest/name
for p,digest in manifest.items():assert hashlib.sha256((extracted/p).read_bytes()).hexdigest()==digest,p
(art/'package-checks.json').write_text(json.dumps({'archive':str(archive),'files':len(manifest),'zip_bytes':archive.stat().st_size,'zip_integrity':True,'independent_extraction_hashes_match':True},indent=2),encoding='utf-8')
print(archive);print('files',len(manifest),'zip bytes',archive.stat().st_size);print('Extraction verified:',extracted)
