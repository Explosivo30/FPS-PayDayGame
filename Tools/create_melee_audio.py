import math,random,wave,struct,pathlib
root=pathlib.Path('Assets/Art/MeleeRobot');root.mkdir(parents=True,exist_ok=True)
rate=44100
for kind,length in [('Warning',.48),('Swing',.18),('Contact',.19),('Step',.13)]:
    rng=random.Random(820);data=[];low=0
    for i in range(int(rate*length)):
        t=i/rate;n=rng.uniform(-1,1);low+=(n-low)*.14
        if kind=='Warning':
            phase=2*math.pi*(370*t+350*t*t)
            gate=.35+.65*max(0,math.sin(t*math.pi*9))
            s=(math.sin(phase)*.45+math.sin(phase*2.03)*.15+low*.12)*gate
        elif kind=='Swing':
            env=math.sin(math.pi*t/length)**1.4
            s=(n*.25+low*.7+math.sin(2*math.pi*(180*t-260*t*t))*.16)*env
        else:
            s=(math.sin(2*math.pi*95*t)*.65+low*.65+n*.25)*math.exp(-t*(28 if kind=='Contact' else 42))
            s+=math.sin(2*math.pi*640*t)*.2*math.exp(-t*55)
        s*=min(1,t*500)*min(1,(length-t)*120)
        data.append(max(-1,min(1,s))*.82)
    with wave.open(str(root/(kind+'.wav')),'wb') as w:
        w.setparams((1,2,rate,0,'NONE','not compressed'))
        w.writeframes(b''.join(struct.pack('<h',int(s*32767)) for s in data))
print('4 original melee audio cues generated')
