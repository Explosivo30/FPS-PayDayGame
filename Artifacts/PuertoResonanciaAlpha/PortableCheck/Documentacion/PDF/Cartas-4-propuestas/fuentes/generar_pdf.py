"""Shared editorial layout for four independent agent-authored design proposals."""
import json, sys, os, re, math
from pathlib import Path
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
from pypdf import PdfReader

FONT_ROOT = Path(r'C:\Windows\Fonts')
pdfmetrics.registerFont(TTFont('Body', str(FONT_ROOT/'segoeui.ttf')))
pdfmetrics.registerFont(TTFont('Strong', str(FONT_ROOT/'segoeuib.ttf')))
W,H=A4
INK=colors.HexColor('#142733');MUTED=colors.HexColor('#4D616C');PAPER=colors.HexColor('#F5F3ED')
LINE=colors.HexColor('#CFD8DA');WHITE=colors.white

def txt(s):
    return escape(str(s)).replace('\n','<br/>').replace('–','-').replace('—','-').replace('‑','-')

class PDF:
    def __init__(self,data,path):
        self.d=data;self.path=Path(path);self.path.parent.mkdir(parents=True,exist_ok=True)
        self.c=canvas.Canvas(str(self.path),pagesize=A4)
        self.c.setTitle(data['title']+' | Propuesta '+str(data['number']))
        self.c.setAuthor('Agente '+str(data['number'])+' - propuestas para Edgar')
        self.c.setSubject('Diseño propuesto de cartas para RobotTower. No implementado.')
        self.accent=colors.HexColor(data['accent']);self.page=0;self.bounds=[]
    def para(self,s,x,y,width,size=10.2,leading=None,bold=False,color=INK,maxh=None,footer=False):
        st=ParagraphStyle('p',fontName='Strong' if bold else 'Body',fontSize=size,leading=leading or size*1.40,textColor=color)
        p=Paragraph(txt(s),st);_,height=p.wrap(width,H)
        if maxh is not None and height>maxh+.2:
            raise ValueError(f"Page {self.page}: text exceeds allocated height ({height:.1f}>{maxh:.1f}): {s[:95]}")
        if not footer and y-height<49:raise ValueError(f'Page {self.page}: footer collision: {s[:90]}')
        p.drawOn(self.c,x,y-height);self.bounds.append([self.page,x,y,width,height]);return y-height
    def begin(self,section):
        if self.page:self.c.showPage()
        self.page+=1;c=self.c;c.setFillColor(PAPER);c.rect(0,0,W,H,fill=1,stroke=0)
        c.setFillColor(self.accent);c.rect(0,H-9,W,9,fill=1,stroke=0)
        self.para('ROBOT TOWER / LABORATORIO DE CARTAS',40,H-29,W-80,8,bold=True,color=MUTED)
        c.setStrokeColor(LINE);c.line(40,44,W-40,44)
        self.para('PROPUESTA '+str(self.d['number'])+' / '+self.d['short'],40,32,W-110,7.8,color=MUTED,footer=True)
        c.setFillColor(MUTED);c.setFont('Strong',8);c.drawRightString(W-40,22,f'{self.page} / 5')
        y=self.para(section,40,H-66,W-80,23,leading=28,bold=True,maxh=62)
        return y-18
    def label(self,s,x,y,width):
        return self.para(s.upper(),x,y,width,8.3,bold=True,color=self.accent)-7
    def block(self,title,body,y,color=None):
        y=self.label(title,40,y,W-80)
        return self.para(body,40,y,W-80,10.2)-16
    def cover(self):
        y=self.begin(self.d['title'])
        y=self.para(self.d['tagline'],40,y,W-80,13,color=self.accent,leading=18,maxh=58)-20
        y=self.label('01 / Lo que pidió el tester',40,y,W-80)
        self.c.setFillColor(WHITE);self.c.roundRect(40,y-81,W-80,87,8,fill=1,stroke=0)
        self.para('La base engancha, pero las mejoras parecen "jugar al Excel". Pide cartas que permitan hacer algo nuevo y sentirse muy poderoso. Empezar con todas las armas reduce la evolución de la partida.',53,y-7,W-106,10.0,maxh=72)
        y-=99
        y=self.block('02 / Qué propone este agente',self.d['thesis'],y)
        y=self.block('03 / Hoy y propuesta',self.d['today_proposal'],y)
        y=self.block('04 / Un momento de juego',self.d['moment'],y)
        y=self.block('Elige esta dirección si...',self.d['choose_if'],y)
        self.para('Documento de ideas. No se ha cambiado el juego. Valores orientativos para prototipar; requieren playtest. Las estadísticas pequeñas pueden seguir en la tienda.',40,y,W-80,8.3,color=MUTED,maxh=47)
    def cardpage(self,cards,start):
        y=self.begin('Ocho cartas para cambiar la partida' if start==0 else 'Más piezas para construir tu estilo')
        self.para('Texto de diseño propuesto. Cada carta debe poder explicarse por su acción y su resultado visible.',40,y,W-80,9.4,color=MUTED,maxh=30)
        top=y-43;gap=14;bw=(W-80-gap)/2;bh=277
        for n,card in enumerate(cards):
            col=n%2;row=n//2;x=40+col*(bw+gap);ytop=top-row*(bh+16);c=self.c
            c.setFillColor(WHITE);c.setStrokeColor(LINE);c.roundRect(x,ytop-bh,bw,bh,8,fill=1,stroke=1)
            c.setFillColor(self.accent);c.roundRect(x+12,ytop-30,26,18,4,fill=1,stroke=0)
            self.para(f'{start+n+1:02d}',x+17,ytop-16,18,8.4,bold=True,color=WHITE,maxh=15)
            yy=self.para(card['name'],x+47,ytop-13,bw-60,12,bold=True,leading=15,maxh=34)-12
            for key,label in [('trigger','ACTIVA'),('effect','CAMBIA'),('signal','SE VE / SE OYE'),('combo','COMBINA'),('cap','LÍMITE')]:
                yy=self.para(label,x+13,yy,bw-26,7.4,bold=True,color=self.accent)-3
                yy=self.para(card[key],x+13,yy,bw-26,8.7,leading=11.2)-8
            if yy<ytop-bh+7:raise ValueError(f"Card too long: {card['name']}, overflow {ytop-bh+7-yy:.1f}")
    def runs(self):
        y=self.begin('Cómo se construye una partida')
        y=self.block('Adquisición y ofertas',self.d['flow'],y)
        y=self.block('Cartas y tienda tienen funciones distintas',self.d['shop'],y)
        for i,b in enumerate(self.d['builds']):
            y=self.label('BUILD '+str(i+1)+' / '+b['name'],40,y,W-80)
            y=self.para(' + '.join(b['cards']),40,y,W-80,11,bold=True,color=INK)-8
            y=self.para(b['sequence'],40,y,W-80,9.7)-7
            y=self.para('Decisión y respuesta: '+b['decision'],40,y,W-80,9.1,color=MUTED)-17
        y=self.block('La elección que cambia tu plan',self.d['choice'],y)
    def prototype(self):
        y=self.begin('Qué probar primero y cómo elegir')
        y=self.block('Prototipo mínimo: tres cartas',self.d['prototype'],y)
        y=self.block('Coste relativo y dependencia real',self.d['effort'],y)
        y=self.block('Potencia, límites y riesgos',self.d['risks'],y)
        y=self.label('PRUEBA CON EL TESTER',40,y,W-80)
        for i,test in enumerate(self.d['tests']):
            y=self.para(str(i+1)+'. '+test,40,y,W-80,9.6)-8
        y-=5
        y=self.block('Qué hay que aceptar al elegir esta propuesta',self.d['tradeoff'],y)
        self.para('Base revisada: feedback de Edgar y su tester; cartas y flujo actuales en RewardCards.md, RewardCard.cs, RewardEffects.cs y RunRewards.cs. La afinidad ofensiva actual usa el arma equipada, no la última baja. El arsenal progresivo es una dirección elegida, pendiente de implementar. Fiabilidad de oleadas primero; IA y mapas quedan fuera de estas propuestas.',40,y,W-80,7.8,color=MUTED,maxh=68)
    def save(self):
        if len(self.d['cards'])!=8 or len(self.d['builds'])!=2 or len(self.d['tests'])!=4:raise ValueError('Expected 8 cards, 2 builds and 4 tester questions')
        self.cover();self.cardpage(self.d['cards'][:4],0);self.cardpage(self.d['cards'][4:],4);self.runs();self.prototype();self.c.save()
        reader=PdfReader(str(self.path));assert len(reader.pages)==5
        extracted='\n'.join(p.extract_text() for p in reader.pages)
        assert all(c['name'] in extracted for c in self.d['cards'])
        self.path.with_suffix('.qa.json').write_text(json.dumps({'pages':5,'words':len(extracted.split()),'bounds':self.bounds},ensure_ascii=False),encoding='utf-8')
        print(json.dumps({'pdf':str(self.path),'pages':5,'words':len(extracted.split()),'bytes':self.path.stat().st_size},ensure_ascii=False))

if __name__=='__main__':
    source,output=sys.argv[1:3]
    PDF(json.loads(Path(source).read_text(encoding='utf-8-sig')),output).save()
