"""Bind a current, evidence-labelled map brief and all four original card PDFs."""
from pathlib import Path
from xml.sax.saxutils import escape
import hashlib
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Image, PageBreak, Table, TableStyle
from reportlab.lib import colors
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader, PdfWriter

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'output/pdf/FundicionReactiva'
TMP = ROOT / 'tmp/pdfs/foundry'
OUT.mkdir(parents=True, exist_ok=True)
TMP.mkdir(parents=True, exist_ok=True)
styles = getSampleStyleSheet()
styles.add(ParagraphStyle(name='Deck', fontName='Helvetica', fontSize=12, leading=17, textColor=colors.HexColor('#334654'), spaceAfter=12))
styles['Normal'].fontSize=10
styles['Normal'].leading=14
styles['Normal'].spaceAfter=8
styles['Heading1'].textColor=colors.HexColor('#103e42')
styles['Heading2'].textColor=colors.HexColor('#b94e28')
story=[]
def p(text, style='Normal'): story.append(Paragraph(text,styles[style]))
def title(kicker, heading):
    p(kicker.upper(),'Heading2');p(heading,'Heading1');story.append(Spacer(1,8))
def page(): story.append(PageBreak())
def footer(canvas,doc):
    canvas.setStrokeColor(colors.HexColor('#d1dedc'));canvas.line(42,40,553,40)
    canvas.setFont('Helvetica',8);canvas.setFillColor(colors.HexColor('#526268'))
    canvas.drawString(42,27,'HELIX / Fundicion Reactiva / 22 de octubre de 2026')
    canvas.drawRightString(553,27,str(doc.page))

title('Prototipo jugable / documento de entrega','Fundicion Reactiva')
p('Transformar el espacio para resolver una amenaza.','Deck')
p('Escena independiente con jugador y robots del juego, fractura Voronoi y dos tandas fijas. La destruccion abre un atajo, elimina una cubierta o deja caer una carga que aplasta robots y despues sirve de cobertura.')
shots=ROOT/'Artifacts/FoundryArena'
for candidate in ('arrival.png','overview.png','before.png','foundry-overview.png'):
    image=shots/candidate
    if image.exists():
        from PIL import Image as PILImage
        w,h=PILImage.open(image).size
        story.append(Image(str(image),width=510,height=510*h/w));break
p('Entrar a jugar','Heading2')
p('Unity: Assets/Scenes/FundicionReactiva.unity. Windows: Builds/FoundryArena/FundicionReactiva.exe, conservando toda su carpeta.')
p('WASD y raton: movimiento. Espacio: salto. R: recarga. Q: arma. E o disparo: mecanismo. F5: reiniciar la escena, incluso tras morir. Reserva de municion ilimitada para experimentar.')
p('El acabado es de prototipo. Una build y pruebas automatizadas verifican contratos; la diversion, el equilibrio final y el rendimiento en otros equipos necesitan partidas humanas.')
page()
title('Diseño y debate','Tres decisiones que se ven al jugar')
for heading,body in [
('1. Abrir el atajo oeste','Una barrera Voronoi bloquea el corredor de servicio. Puede perforarse y demolerse. El colapso retira los colliders del portal y su obstaculo de navegacion; el rodeo exterior sigue disponible. Abrir un agujero visual no se anuncia automaticamente como paso transitable.'),
('2. Conservar o romper una cubierta','Las pantallas opacas protegen frente a salvas y laser. Romperlas descubre tiro y tambien elimina proteccion. Los proyectiles de Centinela y el golpe de Ariete reconocen el mismo estado estructural del mapa.'),
('3. Derribar la carga','Dos anclajes marcados liberan una carga suspendida. Hay aviso y caida. Los robots en la zona reciben daño; la carga aterrizada bloquea vision y aporta cobertura. Si el jugador ocupa el volumen peligroso, espera hasta que salga. Comparte el presupuesto de ataques principales.'),
('El mecanismo movil','Un petalo reutiliza el giro, aviso y comprobacion de ocupantes ya implementados. Cambia una linea de tiro sin rotar el collider sobre un personaje.')]:
    p(heading,'Heading2');p(body)
p('Discusion entre los tres participantes','Heading2')
p('Se comparo una nueva galeria de destruccion con un encuentro real. Se eligieron robots reales y dos tandas para que destruir tenga una consecuencia de combate. Frente a reconstruir navegacion arbitraria, se eligio suelo estable, portal preparado y obstaculos locales. La proteccion de la grua espera solo al jugador: esperar tambien a los robots anularia su utilidad ofensiva.')
page()
title('Fuentes y alcance','Que se integra y que sigue siendo propuesta')
rows=[['Fuente','Aplicacion en esta escena / estado'],
['Estudio de mercado (18/10)','Leer amenazas y transformar la arena. Hipotesis de producto; no prueba ventas ni demanda.'],
['Plan del juego','Dos rutas, roles distintos y decisiones de cobertura. Se acota a un encuentro, no una nueva campaña.'],
['Tienda y balance','Conservado como propuesta. No se declara equilibrada ni se modifican precios o cartas por construir este mapa.'],
['Entrega jugabilidad (21/10)','Reutiliza Centinela, Aguja, Ariete, presupuesto de peligros y petalo. Los roles mantienen sus señales.'],
['Patio Voronoi','Reutiliza fractura real y materiales compatibles; añade consecuencias de combate y navegacion.'],
['Cuatro PDF de cartas','Inspiracion de acciones deliberadas, expresion del arma, preparacion y decisiones espaciales. No se suman sus 32 cartas al juego.']]
table=Table([[Paragraph(escape(c),styles['Normal']) for c in row] for row in rows],colWidths=[145,365],hAlign='LEFT')
table.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#deebe8')),('VALIGN',(0,0),(-1,-1),'TOP'),('BOTTOMPADDING',(0,0),(-1,-1),9),('TOPPADDING',(0,0),(-1,-1),9),('LINEBELOW',(0,0),(-1,-1),.4,colors.HexColor('#cfdbd9'))]))
story.append(table)
p('Los PDF originales son alternativas de diseño, no decisiones canonicas ni funciones verificadas. Sus paginas se anexan completas e intactas para reunir el material sin perder el contexto original.')
page()
title('Validacion y reproduccion','Evidencia de esta entrega')
result=shots/'results.txt'
if result.exists():
    lines=result.read_text(encoding='utf-8-sig').splitlines()
    passes=sum(s.startswith('PASS ') for s in lines);fails=sum(s.startswith('FAIL ') for s in lines)
    p(f'Informe automatizado actual: {passes} comprobaciones PASS, {fails} FAIL.','Deck')
    for line in lines:
        if line.startswith(('PASS ','FAIL ')):p(escape(line))
else:p('La validacion de la build esta pendiente. No interpretar este documento como evidencia de una ejecucion completa.')
p('Limites de la prueba','Heading2')
p('El informe detalla los recorridos y contratos efectivamente ejercitados. Invocar la misma ruta de disparo, interaccion o reinicio por codigo no prueba la pulsacion fisica de teclado/raton. Colocar un robot bajo la grua valida su efecto y no garantiza que una persona consiga ese resultado en cada combate. No se afirma balance final ni 60 FPS.')
page()
title('Archivo de propuestas','Los cuatro PDF anteriores, reunidos')
p('Se incluyen como anexos los originales encontrados en el proyecto: cuatro documentos de cinco paginas cada uno, veinte paginas en total. La fecha y el estado originales de cada propuesta se conservan.')
originals=sorted((ROOT/'output/pdf/Cartas-4-propuestas').glob('*.pdf'))
for file in originals:
    p(file.stem.replace('-',' '),'Heading2')
    p(f'{len(PdfReader(str(file)).pages)} paginas. SHA-256: {hashlib.sha256(file.read_bytes()).hexdigest()[:24]}...')
p('Referencias complementarias del proyecto','Heading2')
references=['Docs/Plan-Maestro-2026-10-18/01-Estudio-de-mercado.md','Docs/Plan-Maestro-2026-10-18/02-Plan-del-juego.md','Docs/Plan-Maestro-2026-10-18/03-Tienda-y-balance.md','Docs/Gameplay-2026-10-21/LEEME.md','Docs/FundicionReactiva-2026-10-22/LEEME.md']
for path in references:
    p(path)
p('Estas cinco fuentes de texto completas se incluyen tambien como archivos adjuntos dentro de este PDF, con sus fechas y limites originales.')
p('Docs/MeleeRobot.md menciona un GDD externo (Shooter Robots.pdf) que no se encontro entre los PDF locales del proyecto. No se ha inventado ni reconstruido su contenido.')
brief=TMP/'brief.pdf'
SimpleDocTemplate(str(brief),pagesize=A4,rightMargin=42,leftMargin=42,topMargin=40,bottomMargin=53,title='Fundicion Reactiva - diseño, pruebas y propuestas reunidas',author='Proyecto HELIX').build(story,onFirstPage=footer,onLaterPages=footer)
writer=PdfWriter();writer.append(str(brief),outline_item='Fundicion Reactiva: diseño y evidencia')
for file in originals:writer.append(str(file),outline_item=file.stem)
for relative in references:
    file=ROOT/relative
    writer.add_attachment(relative.replace('/','__'),file.read_bytes())
writer.add_metadata({'/Title':'Fundicion Reactiva - dossier completo','/Subject':'Prototipo y anexos de propuestas; consultar estados de validacion','/Author':'Proyecto HELIX'})
destination=OUT/'FundicionReactiva-Dossier-completo.pdf'
with destination.open('wb') as stream:writer.write(stream)
print(destination)
print('Pages:',len(PdfReader(str(destination)).pages))
