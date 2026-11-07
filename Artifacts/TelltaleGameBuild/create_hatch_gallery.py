from pathlib import Path
import base64
root=Path(__file__).parent
folder=root/'MarkedVerifiedSmoke'
def data(name):return 'data:image/png;base64,'+base64.b64encode((folder/name).read_bytes()).decode()
before=data('rayado-antes.png');after=data('rayado-ahora.png')
html=f'''<!doctype html><html lang="es"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>HELIX — Rayado Telltale</title>
<style>body{{margin:0;background:#101922;color:#edf0eb;font:16px/1.5 system-ui}}main{{max-width:1500px;margin:auto;padding:28px}}h1{{margin-bottom:6px}}p{{color:#c0cbd1;max-width:900px}}.pair{{display:grid;grid-template-columns:1fr 1fr;gap:18px;margin:22px 0}}figure{{margin:0;background:#1a2733;border:1px solid #344554;border-radius:8px;overflow:hidden}}figcaption{{padding:12px 16px;font-weight:650}}img{{display:block;width:100%;height:auto}}.detail{{aspect-ratio:1.65;background-size:250%;background-repeat:no-repeat}}.column{{background-position:0% 30%}}.weapon{{background-position:70% 100%}}h2{{margin-top:36px}}@media(max-width:700px){{.pair{{grid-template-columns:1fr}}}}</style>
<main><h1>El rayado, más visible</h1><p>Capturas reales del ejecutable con la misma cámara y simulación detenida durante la comparación. Se conserva la iluminación, los colores, el contorno y el HUD. Cambian la fuerza y anchura de la tinta, su presencia en sombras medias y la escala del patrón.</p>
<div class="pair"><figure><figcaption>Antes · rayado suave</figcaption><img alt="Preset anterior en el juego" src="{before}"></figure><figure><figcaption>Ahora · rayado marcado</figcaption><img alt="Rayado reforzado en el juego" src="{after}"></figure></div>
<h2>Detalle del escenario</h2><div class="pair"><figure><figcaption>Antes</figcaption><div class="detail column" style="background-image:url('{before}')"></div></figure><figure><figcaption>Ahora</figcaption><div class="detail column" style="background-image:url('{after}')"></div></figure></div>
<h2>Detalle del arma</h2><div class="pair"><figure><figcaption>Antes</figcaption><div class="detail weapon" style="background-image:url('{before}')"></div></figure><figure><figcaption>Ahora</figcaption><div class="detail weapon" style="background-image:url('{after}')"></div></figure></div>
<p>Los trazos siguen el objeto 3D. El filtro conserva antialias y desvanece las líneas demasiado pequeñas a distancia. Esta revisión verifica el renderizado; no constituye un playtest completo de legibilidad o rendimiento.</p></main></html>'''
(root/'comparacion-rayado.html').write_text(html,encoding='utf-8')
print('Comparacion creada:',root/'comparacion-rayado.html')
