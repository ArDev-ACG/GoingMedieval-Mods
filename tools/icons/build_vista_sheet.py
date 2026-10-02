"""Rehace ASSESTS/Vistas/iconos.png con los iconos de edificio actuales."""
import os
from PIL import Image, ImageDraw, ImageFont

SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ASSESTS", "Iconos", "actuales")
DST = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "ASSESTS", "Vistas", "iconos.png")
ORDER = [("blood_altar","blood_altar"),("blood_ritual_circle","blood_circle"),
         ("count_coffin","count_coffin"),("court_banner","court_banner"),
         ("court_banner_wall","court_banner_wall"),
         ("count_throne","count_throne"),("crimson_candle","crimson_candle"),
         ("blood_brazier","blood_brazier"),("veiled_mirror","veiled_mirror"),
         ("court_reliquary","court_reliquary"),
         ("count_crypt","count_crypt"),("blood_well","blood_well"),
         ("vigil_table","vigil_table"),("impaled_stake","impaled_stake"),
         ("cat_statue","cat_statue"),
         ("mass_grave","mass_grave")]
CX=[14,156,298,440,582]; CY=[14,178,342,506]
found={}
for root,_,files in os.walk(SRC):
    if os.path.basename(root)=="vista": continue
    for f in files: found[f]=os.path.join(root,f)
sheet=Image.new("RGB",(724,670),(34,32,30))
d=ImageDraw.Draw(sheet)
try: font=ImageFont.truetype("segoeui.ttf",12)
except Exception: font=ImageFont.load_default()
miss=[]
for i,(label,name) in enumerate(ORDER):
    x=CX[i%5]; y=CY[i//5]
    p=found.get(f"aldrich_icon_{name}.png")
    if not p: miss.append(name); continue
    sheet.paste(Image.open(p).convert("RGB"),(x,y))
    d.text((x,y+134),label,fill=(214,212,208),font=font)
sheet.save(DST)
print("faltan:",miss or "ninguno","->",DST)
