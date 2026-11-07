from pathlib import Path
from PIL import Image
import math,random
random.seed(72030);n=512
# Periodic distance-like strokes: R controls ink coverage, G breakup, B paper grain.
im=Image.new('RGB',(n,n));px=im.load()
phases=[random.random()*math.tau for _ in range(8)]
for y in range(n):
 v=y/n
 for x in range(n):
  u=x/n;stroke=int(u*8)%8
  warp=.10*math.sin(math.tau*v*2)+.05*math.sin(math.tau*v*5)
  phase=(u*8+warp)%1
  distance=abs(phase-.5)*2
  width=.85+.15*math.sin(math.tau*v*3+phases[stroke])
  r=min(1,distance/width)
  g=.7+.3*math.sin(math.tau*(v*8+u*3))**2
  grain=random.random()
  px[x,y]=(int(r*255),int(g*255),int(grain*255))
p=Path('Assets/Rendering/TelltaleEngraving/Textures/HatchAtlas.png');im.save(p)
