import sys
from PIL import Image, ImageDraw, ImageOps
R=sys.argv[1]; OUT=sys.argv[2]
def L(p): return Image.open(R+'/'+p).convert('RGBA')
def trim(im): return im.crop(im.getbbox())
def fitw(im,w,hmax):
    s=min(w/im.width,hmax/im.height); return im.resize((int(im.width*s),int(im.height*s)),Image.LANCZOS)
W,H=630,500; c=Image.new('RGBA',(W,H)); d=ImageDraw.Draw(c)
# left->right gradient: lava orange (prehistory) -> deep space navy
A=(235,120,50); B=(20,25,70)
for x in range(W):
    t=x/W; d.line([(x,0),(x,H)],fill=tuple(int(A[i]+(B[i]-A[i])*t) for i in range(3))+(255,))
for i in range(40):  # stars on the space side
    import random; random.seed(i); x=random.randint(380,W); y=random.randint(0,300); d.ellipse([x,y,x+2,y+2],fill=(255,255,220,200))
ground=H-30; d.rectangle([0,ground,W,H],fill=(40,30,40,255))
slot=W/7
for i in range(7):
    lm=fitw(trim(L(f'건물/building_lm_st{i+1}_1_01.png')),slot-6,150)
    c.alpha_composite(lm,(int(i*slot+(slot-lm.width)/2),ground-lm.height))
fire=trim(L('피격 이펙트/건물화재/frame_03.png'))
for i in (0,2,4):
    f=fitw(fire,60,60); c.alpha_composite(f,(int(i*slot+slot/2-30),ground-115))
dr=ImageOps.mirror(trim(L('드래곤/불 드래곤/4단계/drag4.png')))  # face right = 시대를 따라 전진
dr=fitw(dr,300,230); c.alpha_composite(dr,(20,120))
br=trim(L('스킬 이펙트/P2. 파이어브레스/frame_05.png')); br=br.resize((230,int(br.height*230/br.width)))
mx,my=20+int(dr.width*0.96),120+int(dr.height*0.33)
c.alpha_composite(br,(mx,my-br.height//2))
logo=trim(L('UI&폰트/01_UI에셋/assets/12_타이틀/title_en.png')); lw=300
logo=logo.resize((lw,int(logo.height*lw/logo.width)),Image.LANCZOS); c.alpha_composite(logo,(W-lw-5,-6))
c.convert('RGB').save(OUT)
