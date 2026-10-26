#!/usr/bin/env python3
"""Thunderstore icon: a quad drone seen from above over a meadow on a dark tile with a brass edge. Own art."""
import sys, math
from PIL import Image, ImageDraw, ImageFilter
S = 4; W = 256 * S
img = Image.new("RGBA", (W, W), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
d.rounded_rectangle([8*S, 8*S, 248*S, 248*S], radius=36*S, fill=(60, 96, 52, 255), outline=(201, 160, 86, 255), width=6*S)
d.rounded_rectangle([20*S, 20*S, 236*S, 236*S], radius=28*S, fill=(84, 124, 66, 255))
for k in range(5): d.line([(30*S, (60 + k * 40)*S), (226*S, (40 + k * 40)*S)], fill=(98, 140, 76, 255), width=8*S)
c = 128
for a in (45, 135, 225, 315):
    x, y = c + 62 * math.cos(math.radians(a)), c + 62 * math.sin(math.radians(a))
    d.line([(c*S, c*S), (x*S, y*S)], fill=(28, 28, 32, 255), width=12*S)
    d.ellipse([(x - 30)*S, (y - 30)*S, (x + 30)*S, (y + 30)*S], fill=(220, 230, 240, 90), outline=(240, 240, 250, 200), width=3*S)
    d.ellipse([(x - 7)*S, (y - 7)*S, (x + 7)*S, (y + 7)*S], fill=(40, 40, 46, 255))
d.rounded_rectangle([(c - 24)*S, (c - 30)*S, (c + 24)*S, (c + 30)*S], radius=8*S, fill=(36, 36, 42, 255), outline=(220, 170, 70, 255), width=3*S)
d.ellipse([(c - 7)*S, (c - 26)*S, (c + 7)*S, (c - 12)*S], fill=(110, 200, 255, 255))
img.resize((256, 256), Image.LANCZOS).save(sys.argv[1])
