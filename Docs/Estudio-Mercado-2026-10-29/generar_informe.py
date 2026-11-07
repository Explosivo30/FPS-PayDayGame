from pathlib import Path
import csv
import html
import json
import re

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.graphics.shapes import Drawing, Rect, Circle, Line, String
from reportlab.graphics import renderSVG
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, PageBreak, Image, KeepTogether
from pypdf import PdfReader

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(__file__).resolve().parent
OUTPUT = ROOT / 'output/pdf/Estudio-Mercado-HELIX-2026-10-29'
OUTPUT.mkdir(parents=True, exist_ok=True)
TMP = ROOT / 'tmp/pdfs/mercado-2026-10-29'
TMP.mkdir(parents=True, exist_ok=True)

groups = {
    2124120: 'Adyacente: extracción', 690210: 'Próximo FPS',
    422970: 'Sin rogue: arena', 1743850: 'Sin rogue: arena',
    1608470: 'Sin rogue: retos', 1229490: 'Adyacente: campaña',
    519860: 'Adyacente: campaña', 232090: 'Adyacente: hordas',
    1430190: 'Adyacente: hordas', 1492070: 'Híbrido de hordas',
    1674270: 'Adyacente: arena', 1285670: 'Híbrido FPS breve',
}
raw_a = json.loads((SOURCE / 'datos-roguelite.json').read_text(encoding='utf-8-sig'))
raw_b = json.loads((SOURCE / 'datos-sin-rogue.json').read_text(encoding='utf-8-sig'))
games = []
for appid, response in ([(r['appid'], r['response']) for r in raw_a['appdetails']] + [(r['AppId'], r['Details']) for r in raw_b]):
    data = response[str(appid)]['data']
    price = data.get('price_overview', {})
    early = any(g.get('id') == '70' for g in data.get('genres', []))
    status = 'Próximamente' if data['release_date']['coming_soon'] else 'Early Access' if early else 'Publicado'
    games.append(dict(appid=appid, name=data['name'], group=groups.get(appid, 'FPS roguelite'), base=price.get('initial', 0)/100, current=price.get('final', 0)/100, currency=price.get('currency'), free=data['is_free'], status=status, date=data['release_date']['date'], url=f'https://store.steampowered.com/app/{appid}/'))

def euro(value):
    return f'{value:.2f}'.replace('.', ',') + ' €'

price_lines = ['| Juego / fuente | Relación | Base ES | Observado ES | Estado |', '|---|---|---:|---:|---|']
for game in games:
    base = 'Sin precio' if game['status'] == 'Próximamente' else 'Gratis' if game['free'] else euro(game['base'])
    current = '-' if game['status'] == 'Próximamente' else 'Gratis' if game['free'] else euro(game['current'])
    price_lines.append(f"| [{game['name']}]({game['url']}) | {game['group']} | {base} | {current} | {game['status']} |")
table_md = '\n'.join(price_lines)

md_path = SOURCE / '00-Estudio-completo.md'
markdown = md_path.read_text(encoding='utf-8')
if '<!-- TABLA_PRECIOS -->' in markdown:
    markdown = markdown.replace('<!-- TABLA_PRECIOS -->', table_md)

# Normalise prose spacing while preserving URLs and source paths verbatim.
def prose_spacing(text):
    text = re.sub(r'(?<=\d)(?=[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])', ' ', text)
    text = re.sub(r'(?<=[A-Za-zÁÉÍÓÚÜÑáéíóúüñ])(?=\d)', ' ', text)
    text = re.sub(r'(?<=\d)€', ' €', text)
    return text

parts = re.split(r'(\[[^\]]+\]\([^\)]+\)|`[^`]+`)', markdown)
markdown = ''.join(piece if i % 2 else prose_spacing(piece) for i, piece in enumerate(parts))
markdown = markdown.replace('Ziggurat2', 'Ziggurat 2')
markdown = markdown.replace('Subinforme03', 'Subinforme 03')
markdown = markdown.replace('informe 03 aporta', 'informe 03 aporta')
md_path.write_text(markdown, encoding='utf-8')

with (SOURCE / 'precios-Steam-ES-2026-10-29.csv').open('w', newline='', encoding='utf-8-sig') as stream:
    writer = csv.DictWriter(stream, fieldnames=list(games[0]))
    writer.writeheader()
    writer.writerows(games)

priced = [g for g in games if g['currency'] == 'EUR' and not g['free']]
priced.sort(key=lambda g: (0 if g['group'] == 'FPS roguelite' else 1, g['base']))
pdfmetrics.registerFont(TTFont('Arial', 'C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('Arial-Bold', 'C:/Windows/Fonts/arialbd.ttf'))
pdfmetrics.registerFont(TTFont('Arial-Italic', 'C:/Windows/Fonts/ariali.ttf'))
pdfmetrics.registerFontFamily('Arial', normal='Arial', bold='Arial-Bold', italic='Arial-Italic', boldItalic='Arial-Bold')

# Vector chart: the PDF keeps text and price markers sharp at any zoom.
chart = Drawing(500, 588)
chart.add(Rect(0, 0, 500, 588, fillColor=colors.HexColor('#f4f6f6'), strokeColor=None))
left, scale = 142, 330/45
for tick in range(0, 41, 10):
    x = left + tick*scale
    chart.add(Line(x, 43, x, 555, strokeColor=colors.HexColor('#dce3e5'), strokeWidth=.5))
    chart.add(String(x, 28, str(tick), fontName='Arial', fontSize=8, textAnchor='middle', fillColor=colors.HexColor('#536574')))
for row, game in enumerate(priced):
    y = 544-row*22.8
    fill = colors.HexColor('#a4cdca' if game['group'] == 'FPS roguelite' else '#b3c3ce')
    chart.add(String(left-8, y-2, game['name'], fontName='Arial', fontSize=8.1, textAnchor='end', fillColor=colors.HexColor('#112b3a')))
    chart.add(Rect(left, y-6, game['base']*scale, 13, fillColor=fill, strokeColor=None))
    chart.add(Circle(left+game['current']*scale, y+.5, 2.7, fillColor=colors.HexColor('#c8572e'), strokeColor=None))
chart.add(Rect(145, 570, 12, 7, fillColor=colors.HexColor('#a4cdca'), strokeColor=None))
chart.add(String(162, 569, 'Base roguelite', fontName='Arial', fontSize=8))
chart.add(Rect(260, 570, 12, 7, fillColor=colors.HexColor('#b3c3ce'), strokeColor=None))
chart.add(String(277, 569, 'Base otros', fontName='Arial', fontSize=8))
chart.add(Circle(363, 574, 2.7, fillColor=colors.HexColor('#c8572e'), strokeColor=None))
chart.add(String(372, 569, 'Observado', fontName='Arial', fontSize=8))
chart.add(String(305, 8, 'EUR - región España - 29/10/2026', fontName='Arial', fontSize=9, textAnchor='middle'))
renderSVG.drawToFile(chart, str(OUTPUT / 'precios-comparables.svg'))
NAVY = colors.HexColor('#112b3a')
TEAL = colors.HexColor('#087f7c')
GREY = colors.HexColor('#536574')
PALE = colors.HexColor('#f1f5f5')
ORANGE = colors.HexColor('#c95e32')
PAGE_W, PAGE_H = A4
WIDTH = PAGE_W - 34*mm
styles = {
    'body': ParagraphStyle('Body', fontName='Arial', fontSize=10.1, leading=14.1, textColor=NAVY, spaceAfter=8),
    'h2': ParagraphStyle('Section', fontName='Arial-Bold', fontSize=15.5, leading=19.3, textColor=TEAL, spaceBefore=18, spaceAfter=9, keepWithNext=True),
    'h3': ParagraphStyle('Subsection', fontName='Arial-Bold', fontSize=11.8, leading=15.2, textColor=NAVY, spaceBefore=9, spaceAfter=6, keepWithNext=True),
    'table': ParagraphStyle('Cell', fontName='Arial', fontSize=8.8, leading=11.6, textColor=NAVY, spaceAfter=0),
    'th': ParagraphStyle('CellHeading', fontName='Arial-Bold', fontSize=8.7, leading=11.4, textColor=colors.white),
    'small': ParagraphStyle('Small', fontName='Arial', fontSize=8.3, leading=11.5, textColor=GREY, spaceAfter=7),
    'cover': ParagraphStyle('Cover', fontName='Arial-Bold', fontSize=35, leading=39, textColor=NAVY, spaceAfter=20),
    'subtitle': ParagraphStyle('Subtitle', fontName='Arial', fontSize=19, leading=25, textColor=TEAL, spaceAfter=22),
    'toc': ParagraphStyle('Index', fontName='Arial', fontSize=10.5, leading=15.5, spaceAfter=5, textColor=NAVY),
}

def inline(text):
    text = text.replace('\u2014', '-').replace('\u2013', '-').replace('\u2011', '-').replace('\u2212', '-')
    tokens = []
    def link(match):
        tokens.append(f'<link href="{html.escape(match.group(2), quote=True)}" color="#087f7c">{html.escape(match.group(1))}</link>')
        return f'@@TOKEN{len(tokens)-1}@@'
    text = re.sub(r'\[([^\]]+)\]\(([^)]+)\)', link, text)
    def code(match):
        tokens.append(f'<font name="Courier" size="8.3">{html.escape(match.group(1))}</font>')
        return f'@@TOKEN{len(tokens)-1}@@'
    text = re.sub(r'`([^`]+)`', code, text)
    text = html.escape(text)
    text = re.sub(r'\*\*(.+?)\*\*', r'<b>\1</b>', text)
    for i, token in enumerate(tokens):
        text = text.replace(f'@@TOKEN{i}@@', token)
    return text

def para(text, style='body'):
    return Paragraph(inline(text), styles[style])

def table(rows):
    count = len(rows[0])
    weights = {3: [.26,.37,.37], 4: [.24,.253,.253,.254], 5: [.26,.22,.13,.14,.25]}.get(count, [1/count]*count)
    cells = [[para(cell, 'th' if n == 0 else 'table') for cell in row] for n, row in enumerate(rows)]
    result = Table(cells, colWidths=[WIDTH*w for w in weights], repeatRows=1, hAlign='LEFT')
    result.setStyle(TableStyle([
        ('BACKGROUND',(0,0),(-1,0),NAVY), ('ROWBACKGROUNDS',(0,1),(-1,-1),[PALE,colors.white]),
        ('VALIGN',(0,0),(-1,-1),'TOP'), ('LEFTPADDING',(0,0),(-1,-1),7),
        ('RIGHTPADDING',(0,0),(-1,-1),7), ('TOPPADDING',(0,0),(-1,-1),7),
        ('BOTTOMPADDING',(0,0),(-1,-1),7), ('LINEBELOW',(0,0),(-1,0),.6,TEAL),
        ('LINEBELOW',(0,1),(-1,-1),.25,colors.HexColor('#d9e3e5')),
    ]))
    return result

sections = re.findall(r'^## (\d+\..+)$', markdown, re.M)
story = [Spacer(1, 14*mm), para('HELIX', 'cover'), para('Roguelite o misión arcade', 'subtitle'),
         para('Estudio de mercado y dirección de producto', 'h3'),
         para('29 de octubre de 2026 · PC / Steam · Investigación con tres agentes', 'small'),
         Spacer(1, 12*mm),
         para('Hay público para el género. El hueco de HELIX todavía debe demostrarse.', 'h2'),
         para('La recomendación provisional es un FPS roguelite de asalto con final y decisiones espaciales visibles. La alternativa seria es una misión con kit elegido: deben compararse con el mismo contenido antes de ampliar producción.'),
         Spacer(1, 8*mm),
         table([['Evidencia', 'Decisión pendiente'], ['24 fichas y respuestas regionales de Steam; revisión del proyecto y del feedback', '¿El jugador quiere otra partida por su build o por dominar una misión?'], ['Tres análisis independientes y contraste de objeciones', 'Una demo compartida para comparar elección variable y plan determinista']]),
         Spacer(1, 10*mm),
         para('Lectura de fuentes públicas y del proyecto. Sin cambios ni ejecución del juego. Precios en EUR / España; reseñas con ámbitos distintos; no se estiman ventas.', 'small'), PageBreak(),
         para('Cómo usar este informe', 'h2'),
         para('Las conclusiones son condicionales. Los datos describen competidores; las hipótesis de diseño, precio y validación no se presentan como resultados ya obtenidos.'), Spacer(1, 3*mm)]
for title in sections:
    number = title.split('.')[0]
    story.append(Paragraph(f'<link href="#s{number}" color="#112b3a">{html.escape(title)}</link>', styles['toc']))
story += [Spacer(1, 6*mm), para('Fuentes enlazadas en el texto. Los tres análisis independientes y los JSON/CSV auditables se conservan junto al documento editable.', 'small'), PageBreak()]

lines = markdown[markdown.index('## 1.') :].splitlines()
index = 0
chart_added = False
while index < len(lines):
    line = lines[index].strip()
    if not line:
        index += 1
        continue
    if line.startswith('## '):
        title = line[3:]
        number = title.split('.')[0]
        if number in ('6', '17'):
            story.append(PageBreak())
        story.append(Paragraph(f'<a name="s{number}"/>' + inline(title), styles['h2']))
    elif line.startswith('### '):
        story.append(para(line[4:], 'h3'))
    elif line.startswith('|'):
        rows = []
        while index < len(lines) and lines[index].strip().startswith('|'):
            row = [c.strip() for c in lines[index].strip().strip('|').split('|')]
            if not all(re.fullmatch(r':?-+:?', c) for c in row):
                rows.append(row)
            index += 1
        drawn_table = table(rows)
        story.append(KeepTogether([drawn_table]) if len(rows) <= 5 else drawn_table)
        if rows[0][0] == 'Juego / fuente' and not chart_added:
            story.append(PageBreak())
            story.append(para('Precios: catálogo y oferta observada', 'h3'))
            chart.scale(WIDTH/500, WIDTH/500)
            chart.width = WIDTH
            chart.height = 588*WIDTH/500
            story.append(chart)
            story.append(para('Fuente: API pública appdetails, región ES, captura 29/10/2026. Verde: FPS roguelite; azul: alternativas y adyacentes. Punto naranja: precio observado. Se omiten gratis y títulos sin precio. No representa ventas, calidad ni tamaño de mercado.', 'small'))
            chart_added = True
        else:
            story.append(Spacer(1, 8))
        continue
    elif line.startswith('- '):
        story.append(Paragraph(inline(line[2:]), styles['body'], bulletText='•'))
    elif re.match(r'^\d+\. ', line):
        match = re.match(r'^(\d+)\. (.+)', line)
        story.append(Paragraph('<b>' + match.group(1) + '.</b> ' + inline(match.group(2)), styles['body']))
    elif not line.startswith('<!--'):
        text = line
        while index+1 < len(lines) and lines[index+1].strip() and not re.match(r'^(#|\||- |\d+\. )', lines[index+1].strip()):
            index += 1
            text += ' ' + lines[index].strip()
        story.append(para(text))
    index += 1

def page_frame(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(TEAL)
    canvas.setLineWidth(.7)
    canvas.line(17*mm, PAGE_H-13*mm, PAGE_W-17*mm, PAGE_H-13*mm)
    canvas.setFont('Arial-Bold', 8)
    canvas.setFillColor(TEAL)
    canvas.drawString(17*mm, PAGE_H-10.5*mm, 'HELIX / ESTUDIO DE MERCADO')
    canvas.setFillColor(GREY)
    canvas.setFont('Arial', 7.8)
    canvas.drawString(17*mm, 10*mm, '29/10/2026 - Hechos, inferencias y propuestas separados')
    canvas.drawRightString(PAGE_W-17*mm, 10*mm, str(doc.page))
    canvas.restoreState()

pdf_path = OUTPUT / 'HELIX-Estudio-de-mercado-Roguelite-vs-Misiones.pdf'
doc = SimpleDocTemplate(str(pdf_path), pagesize=A4, rightMargin=17*mm, leftMargin=17*mm,
                        topMargin=20*mm, bottomMargin=18*mm,
                        title='HELIX: roguelite frente a misión arcade', author='Estudio de producto y mercado',
                        subject='Competidores, posicionamiento, viabilidad y validación - 29/10/2026')
doc.build(story, onFirstPage=page_frame, onLaterPages=page_frame)
reader = PdfReader(str(pdf_path))
extracted = '\n'.join(page.extract_text() for page in reader.pages)
assert len(reader.pages) >= 12
assert all(len(page.extract_text()) > 400 for page in reader.pages), 'Unexpected blank or nearly empty page'
assert 'TABLA_PRECIOS' not in extracted
assert all(title[:15] in extracted for title in sections)
assert '1674270' not in extracted  # Source URLs remain clickable, not raw in prose.
(TMP / 'texto-extraido.txt').write_text(extracted, encoding='utf-8')
print(json.dumps({'pdf': str(pdf_path), 'pages': len(reader.pages), 'games': len(games), 'words_markdown': len(markdown.split()), 'sources': len(re.findall(r'https?://', markdown))}, ensure_ascii=False))
