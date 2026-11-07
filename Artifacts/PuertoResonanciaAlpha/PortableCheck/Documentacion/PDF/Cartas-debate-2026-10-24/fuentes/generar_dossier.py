"""Render the debate after all independent proposals and votes are recorded."""
import json,sys,re
from pathlib import Path
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph,Table,TableStyle
from reportlab.lib.styles import ParagraphStyle
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parent
OUT=ROOT.parent/'Cartas-nuevas-debate-y-eleccion.pdf'
pdfmetrics.registerFont(TTFont('Body',r'C:\Windows\Fonts\segoeui.ttf'))
pdfmetrics.registerFont(TTFont('Strong',r'C:\Windows\Fonts\segoeuib.ttf'))
W,H=A4;M=40;WIDTH=W-2*M
INK=colors.HexColor('#17313C');MUTED=colors.HexColor('#526871')
PAPER=colors.HexColor('#F6F4EE');LINE=colors.HexColor('#D1DBDD')
TEAL=colors.HexColor('#087B80');ORANGE=colors.HexColor('#B14C28')
ACCENTS={'A':ORANGE,'B':TEAL,'C':colors.HexColor('#675699')}

def clean(s):return str(s).replace('–','-').replace('—','-').replace('‑','-')
def text(s):return escape(clean(s)).replace('\n','<br/>')
def read(name):return json.loads((ROOT/name).read_text(encoding='utf-8-sig'))

class Dossier:
 def __init__(self):
  self.s=read('sintesis.json');self.old=read('catalogo-anterior.json')
  self.groups=[read('A-armas.json'),read('B-impulso.json'),read('C-ritmo.json')]
  self.all={c['id']:c for c in self.old}
  for group in self.groups:self.all.update({c['id']:c for c in group['cards']})
  assert len(self.all)==50
  self.c=canvas.Canvas(str(OUT),pagesize=A4);self.page=0;self.checks=[]
  self.c.setTitle('Cartas nuevas: debate y elección - Robot Tower')
  self.c.setAuthor('Tres agentes de diseño y coordinación editorial')
  self.c.setSubject('Propuestas y debate del 24 de octubre de 2026. Sin implementación ni playtest humano.')
 def p(self,s,x,y,w=WIDTH,size=10,leading=None,bold=False,color=INK,footer=False):
  st=ParagraphStyle('p',fontName='Strong' if bold else 'Body',fontSize=size,leading=leading or size*1.38,textColor=color)
  obj=Paragraph(text(s),st);_,h=obj.wrap(w,H)
  if not footer and y-h<52:raise ValueError(f'Page {self.page}: footer collision {y-h:.1f}, {str(s)[:100]}')
  obj.drawOn(self.c,x,y-h);self.checks.append([self.page,x,y,w,h]);return y-h
 def begin(self,title,subtitle=None,accent=TEAL):
  if self.page:self.c.showPage()
  self.page+=1;c=self.c;c.setFillColor(PAPER);c.rect(0,0,W,H,fill=1,stroke=0)
  c.setFillColor(accent);c.rect(0,H-8,W,8,fill=1,stroke=0)
  self.p('ROBOT TOWER / CARTAS / SEGUNDA RONDA',M,H-28,size=8,bold=True,color=MUTED)
  self.p('24 OCT 2026 / PROPUESTAS SIN IMPLEMENTAR',M,31,w=WIDTH-60,size=7.5,color=MUTED,footer=True)
  c.setFillColor(MUTED);c.setFont('Strong',8);c.drawRightString(W-M,21,f'{self.page} / 12')
  c.setStrokeColor(LINE);c.line(M,44,W-M,44)
  y=self.p(title,M,H-65,size=23,leading=28,bold=True)-14
  if subtitle:y=self.p(subtitle,M,y,size=10,color=MUTED)-19
  return y
 def block(self,title,body,y,size=10):
  y=self.p(title.upper(),M,y,size=8.6,bold=True,color=TEAL)-7
  return self.p(body,M,y,size=size)-17
 def table(self,headers,rows,widths,y,size=8.8,pad=7):
  def cell(s,bold=False):return Paragraph(text(s),ParagraphStyle('cell',fontName='Strong' if bold else 'Body',fontSize=size,leading=size*1.32,textColor=INK))
  values=[[cell(h,True) for h in headers]]+[[cell(v) for v in row] for row in rows]
  t=Table(values,colWidths=widths,hAlign='LEFT')
  t.setStyle(TableStyle([('BACKGROUND',(0,0),(-1,0),colors.HexColor('#E0E9E8')),('ROWBACKGROUNDS',(0,1),(-1,-1),[colors.white,PAPER]),('VALIGN',(0,0),(-1,-1),'TOP'),('LEFTPADDING',(0,0),(-1,-1),8),('RIGHTPADDING',(0,0),(-1,-1),8),('TOPPADDING',(0,0),(-1,-1),pad),('BOTTOMPADDING',(0,0),(-1,-1),pad),('LINEBELOW',(0,0),(-1,0),.7,LINE),('LINEBELOW',(0,1),(-1,-1),.3,LINE)]))
  _,h=t.wrap(WIDTH,H)
  if y-h<54:raise ValueError(f'Table overflow page {self.page}: {y-h:.1f}')
  t.drawOn(self.c,M,y-h);return y-h-17
 def verdict(self):
  y=self.begin('Qué elegirían los agentes','Tu preferencia: sobre todo los PDF 1 y 2. Se comparan 32 cartas anteriores y 18 nuevas.')
  y=self.p(self.s['winner_title'],M,y,size=19,bold=True,color=ORANGE)-12
  y=self.p(self.s['verdict'],M,y,size=11.2)-20
  y=self.block('La decisión en una frase',self.s['one_line'],y,11)
  y=self.p('TRES CARTAS PARA EL PRIMER PROTOTIPO',M,y,size=8.6,bold=True,color=TEAL)-13
  for item in self.s['prototype']:
   c=self.all[item['id']]
   y=self.p(item['id']+' / '+c['name'],M,y,size=12,bold=True)-5
   y=self.p(item['why'],M,y,size=10)-15
  y=self.block('Qué se ha acordado y qué no',self.s['agreement_scope'],y)
  self.p('Es una recomendación de diseño, no una prueba de diversión ni una aprobación para implementarla. El juego no se ha modificado en esta ronda.',M,y,size=8.4,color=MUTED)
 def comparison(self):
  y=self.begin('Siete direcciones, una prioridad','Criterios: cambio jugable, claridad/control, combinaciones y encaje contigo. Coste y fiabilidad sirven para desempatar.')
  rows=[[r['name'],r['judgment'],r['reason']] for r in self.s['directions']]
  y=self.table(['Dirección','Decisión','Por qué'],rows,[132,79,WIDTH-211],y,9)
  y=self.block('No gana por tener más efectos',self.s['comparison_note'],y)
  y=self.block('La mejor carta nueva para probar',self.s['best_new'],y)
  self.p('Las etiquetas del cuadro son un juicio de los agentes. No se asignan puntuaciones numéricas ni resultados de usuarios que todavía no existen.',M,y,size=8.4,color=MUTED)
 def debate(self):
  y=self.begin('El debate que cambió la selección','Se conservan objeciones, respuestas y discrepancias. Las propuestas y votos originales están en la carpeta fuentes.')
  for d in self.s['debate']:
   y=self.p(d['topic'],M,y,size=11.3,bold=True,color=ORANGE)-7
   for key,label in [('objection','Objeción'),('answer','Respuesta'),('result','Consecuencia')]:
    y=self.p(label+': '+d[key],M,y,size=9.3)-6
   y-=10
  rows=[[v['agent'],v['direction'],v['cards'],v['reservation']] for v in self.s['votes']]
  self.table(['Agente','Dirección votada','Prototipo','Reserva'],rows,[77,115,154,WIDTH-346],y,8.1)
 def new_cards(self):
  for group in self.groups:
   accent=ACCENTS[group['id']]
   for start in [0,3]:
    y=self.begin(group['title'],group['thesis'] if start==0 else 'Continuación / Ideas nuevas del agente '+group['id'],accent)
    if start==0:y=self.p('AGENTE '+group['id']+' / '+group['agent'],M,y,size=8.1,bold=True,color=accent)-15
    for card in group['cards'][start:start+3]:
     top=y
     y=self.p(card['id']+' / '+card['name'],M+12,y-10,w=WIDTH-24,size=12,bold=True,color=accent)-7
     for key,label in [('trigger','Acción'),('effect','Cambio'),('signal','Señal'),('limit','Límite'),('synergy','Combina'),('distinct_from','Diferencia'),('tradeoff','Coste / riesgo')]:
      y=self.p(label+': '+card[key],M+12,y,w=WIDTH-24,size=9.1,leading=12.1)-3
     y-=10
     self.c.setStrokeColor(LINE);self.c.roundRect(M,y,WIDTH,top-y,7,fill=0,stroke=1)
     y-=13
 def prototype(self):
  y=self.begin('Primero: una combinación pequeña','Tres capacidades para observar decisiones nuevas. La potencia no se valida contando solo daño o bajas.')
  y=self.block('Reglas del experimento',self.s['prototype_rules'],y)
  for i,step in enumerate(self.s['encounter'],1):y=self.block(str(i)+' / '+step['title'],step['body'],y)
  y=self.block('Oferta y progresión',self.s['offer_rules'],y)
  y=self.p('QUÉ OBSERVAR EN EL TESTER',M,y,size=8.6,bold=True,color=TEAL)-10
  for t in self.s['tests']:y=self.p('- '+t,M,y,size=9.6)-7
  self.p('Sin tiempos de producción inventados. No se rehacen mapas ni se añade otra familia de enemigos para justificar estas cartas.',M,y-5,size=8.3,color=MUTED)
 def previous(self):
  lookup={c['id']:c for c in self.old}
  assert len(self.s['old_triage'])==32
  for start in [0,16]:
   y=self.begin('Qué queda de las cartas anteriores',('PDF 1 y 2 / Armas y movimiento' if start==0 else 'PDF 3 y 4 / Reacciones y artefactos')+' / Criba editorial para el primer prototipo, no voto carta por carta.')
   rows=[]
   for item in self.s['old_triage'][start:start+16]:
    c=lookup[item['id']];rows.append([item['id']+'\n'+c['name'],item['status'],item['reason']])
   y=self.table(['Carta anterior','Situación','Motivo'],rows,[156,83,WIDTH-239],y,8.4,5)
   if start==16:
    self.p('Fuentes: los cuatro JSON originales (8 cartas por documento), tres propuestas nuevas y sus mensajes/votos reales. Feedback del tester facilitado por Edgar. Preferencia por PDF 1 y 2 expresada el 24/10/2026. Todos los valores son hipótesis de prototipo.',M,y,size=8.1,color=MUTED)
 def save(self):
  self.verdict();self.comparison();self.debate();self.new_cards();self.prototype();self.previous()
  assert self.page==12,self.page
  self.c.save();r=PdfReader(OUT);assert len(r.pages)==12
  content='\n'.join(p.extract_text() for p in r.pages)
  assert all(c['name'] in content for c in self.all.values())
  assert '\ufffd' not in content
  (ROOT/'revision-maquetacion.json').write_text(json.dumps({'pages':12,'words':len(content.split()),'text_bounds':self.checks},ensure_ascii=False),encoding='utf-8')
  print(json.dumps({'path':str(OUT),'pages':12,'words':len(content.split()),'bytes':OUT.stat().st_size},ensure_ascii=True))

if __name__=='__main__':Dossier().save()
