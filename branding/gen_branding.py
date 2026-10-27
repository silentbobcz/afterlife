"""AFTERLIFE branding: logo, Nexus Mods header and gallery cover, GitHub social preview.

Same geometry and colours as the app (src/Afterlife.App/App.xaml: LogoDrawing, BrandImage, palette)
and the same wordmark cuts as Controls/Ambient.cs GlitchTitle. Renders SVG with cairosvg (fonts:
Chakra Petch and JetBrains Mono from src/Afterlife.App/Assets/Fonts must be installed).
Run: python3 branding/gen_branding.py   -> branding/out/*.svg + *.png
"""
import os, random
import cairosvg

OUT = os.path.join(os.path.dirname(__file__), "out")
os.makedirs(OUT, exist_ok=True)

BG, BG2, BG3 = "#050508", "#0A0C12", "#11141D"
Y, C, M, T, T2 = "#FCEE0A", "#00F0FF", "#FF2A6D", "#E8E6D9", "#8B8F9C"
MARKS = "M20 22H40V28H26V42H20Z M100 22H80V28H94V42H100Z M20 102H40V96H26V82H20Z M100 102H80V96H94V82H100Z M46 40L82 62L46 84Z M20 60H34V64H20Z M86 60H100V64H86Z"
SHAPE = "M0 0H92L120 28V120H28L0 92Z"


def defs():
    return f"""
  <pattern id="raster" width="12" height="12" patternUnits="userSpaceOnUse">
    <rect width="12" height="4.2" fill="{BG}" fill-opacity="0.55"/>
  </pattern>
  <pattern id="grid" width="48" height="48" patternUnits="userSpaceOnUse">
    <path d="M48 0H0V48" fill="none" stroke="{C}" stroke-opacity="0.045" stroke-width="1"/>
  </pattern>
  <radialGradient id="glowC"><stop offset="0" stop-color="{C}" stop-opacity="0.16"/><stop offset="1" stop-color="{C}" stop-opacity="0"/></radialGradient>
  <radialGradient id="glowM"><stop offset="0" stop-color="{M}" stop-opacity="0.12"/><stop offset="1" stop-color="{M}" stop-opacity="0"/></radialGradient>
  <filter id="soft" x="-20%" y="-60%" width="140%" height="220%"><feGaussianBlur stdDeviation="9"/></filter>
  <linearGradient id="ruleC" x1="0" x2="1"><stop offset="0" stop-color="{C}" stop-opacity="0.30"/><stop offset="0.5" stop-color="{M}" stop-opacity="0.14"/><stop offset="1" stop-color="{C}" stop-opacity="0"/></linearGradient>
"""


def logo(x, y, size, raster=False):
    """The AFTERLIFE mark (120 x 120 units) at x, y with the given pixel size."""
    s = size / 120.0
    r = f'<path d="{SHAPE}" fill="url(#raster)"/>' if raster else ""
    return f"""<g transform="translate({x:.2f},{y:.2f}) scale({s:.5f})">
  <path d="{SHAPE}" fill="{Y}"/>
  <path d="{MARKS}" fill="{C}" transform="translate(-2.4,0)"/>
  <path d="{MARKS}" fill="{M}" transform="translate(2.4,0)"/>
  <path d="{MARKS}" fill="{BG}"/>
  <path d="M38 92H82V95H38Z M9 9H18V18H9Z" fill="{M}"/>
  <path d="M108 100V108H100" fill="none" stroke="{BG}" stroke-width="3"/>
  {r}
</g>"""


def brand(x, y, h):
    """Big mark + three smaller rastered copies, as BrandImage in App.xaml (height h)."""
    u = h / 64.0  # BrandImage is 64 units high
    return (logo(x, y, 64 * u)
            + logo(x + 70.4 * u, y, 21.76 * u, True)
            + logo(x + 70.4 * u, y + 24.96 * u, 18.56 * u, True)
            + logo(x + 70.4 * u, y + 46.72 * u, 15.36 * u, True))


def brand_width(h):
    return (70.4 + 21.76) * h / 64.0


def wordmark(x, y, size, uid, spacing=0.11, glow=True, anchor="start"):
    """AFTERLIFE in Chakra Petch Bold with the two raster cuts of GlitchTitle (y = baseline)."""
    cap = size * 0.70  # cap height of Chakra Petch
    top = y - cap
    cut1y, cut1h = top + cap * 0.238, max(1.5, cap * 0.071)
    cut2y, cut2h = top + cap * 0.524, max(1.0, cap * 0.048)
    ls = size * spacing
    text = f'<text x="{x}" y="{y}" text-anchor="{anchor}" font-family="Chakra Petch" font-weight="700" font-size="{size}" letter-spacing="{ls:.1f}">AFTERLIFE</text>'
    bottom = y + size * 0.35
    clip = (f'<clipPath id="cut{uid}">'
            f'<rect x="0" y="{top - size:.1f}" width="4000" height="{cut1y - (top - size):.1f}"/>'
            f'<rect x="0" y="{cut1y + cut1h:.1f}" width="4000" height="{cut2y - cut1y - cut1h:.1f}"/>'
            f'<rect x="0" y="{cut2y + cut2h:.1f}" width="4000" height="{bottom - cut2y - cut2h:.1f}"/></clipPath>')
    g = f'<g fill="{Y}" filter="url(#soft)" opacity="0.40">{text}</g>' if glow else ""
    return clip + g + f'<g fill="{Y}" clip-path="url(#cut{uid})">{text}</g>'


def text_width(size, spacing=0.11):
    # Chakra Petch Bold, "AFTERLIFE": ~0.62 em per letter on average plus letter spacing
    return 9 * size * (0.64 + spacing)


def traces(w, h, seed, ystart, yend, count=10, keep_clear=None):
    rnd = random.Random(seed)
    out = []
    for i in range(count):
        y = rnd.uniform(ystart, yend)
        if keep_clear and keep_clear[0] < y < keep_clear[1]:
            continue  # no line through the lettering
        x0 = rnd.uniform(0, w * 0.85)
        ln = rnd.uniform(80, 320)
        col = C if rnd.random() < 0.65 else M
        op = rnd.uniform(0.18, 0.45)
        out.append(f'<rect x="{x0:.0f}" y="{y:.0f}" width="{ln:.0f}" height="1" fill="{col}" opacity="{op:.2f}"/>')
        out.append(f'<rect x="{x0-3:.0f}" y="{y-1.5:.1f}" width="3" height="3" fill="{col}" opacity="{min(1, op+0.25):.2f}"/>')
        if rnd.random() < 0.5:  # a packet
            px = x0 + ln * rnd.uniform(0.3, 0.9)
            out.append(f'<rect x="{px-34:.0f}" y="{y:.0f}" width="34" height="1" fill="{col}" opacity="0.6"/><rect x="{px:.0f}" y="{y-1:.0f}" width="2" height="2" fill="{col}"/>')
    return "\n".join(out)


def background(w, h, seed, logo_cx, logo_cy, name_cx, clear=None):
    stripes = "".join(f'<rect x="0" y="{y}" width="{w}" height="1" fill="#000" opacity="0.22"/>' for y in range(0, h, 3))
    return f"""<rect width="{w}" height="{h}" fill="{BG}"/>
<rect width="{w}" height="{h}" fill="url(#grid)"/>
<ellipse cx="{logo_cx}" cy="{logo_cy}" rx="{w*0.22:.0f}" ry="{h*0.45:.0f}" fill="url(#glowC)"/>
<ellipse cx="{name_cx}" cy="{logo_cy}" rx="{w*0.30:.0f}" ry="{h*0.35:.0f}" fill="url(#glowM)"/>
{traces(w, h, seed, h*0.08, h*0.92, 18, clear)}
<rect x="0" y="{h-1.5}" width="{w}" height="1" fill="url(#ruleC)"/>
<rect x="{w*0.18:.0f}" y="{h-2.5}" width="150" height="2" fill="{C}" opacity="0.55"/>
{stripes}"""


def hud_corners(w, h, pad, col=C, l=26):
    return f"""<path d="M{pad} {pad+l}V{pad}H{pad+l}" fill="none" stroke="{col}" stroke-width="2" opacity="0.7"/>
<path d="M{w-pad-l} {h-pad}H{w-pad}V{h-pad-l}" fill="none" stroke="{col}" stroke-width="2" opacity="0.7"/>"""


def svg(w, h, body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" width="{w}" height="{h}" viewBox="0 0 {w} {h}"><defs>{defs()}</defs>{body}</svg>'


def lockup(cx, cy, logo_h, name_size, uid, sub_size=None, sub="MOD MANAGER FOR CYBERPUNK 2077"):
    """Logo + wordmark + subtitle with the blinking-cursor block, centred at cx, cy."""
    gap = logo_h * 0.30
    bw = brand_width(logo_h)
    tw = text_width(name_size)
    total = bw + gap + tw
    x0 = cx - total / 2
    ly = cy - logo_h / 2
    tx = x0 + bw + gap
    base = cy + name_size * 0.18
    sub_size = sub_size or name_size * 0.24
    sy = base + sub_size * 1.9
    cursor_x = tx + 2 + len(sub) * sub_size * (0.60 + 0.12) + sub_size * 0.3
    return (brand(x0, ly, logo_h) + wordmark(tx, base, name_size, uid)
            + f'<text x="{tx+2:.1f}" y="{sy:.1f}" font-family="JetBrains Mono" font-size="{sub_size:.1f}" letter-spacing="{sub_size*0.12:.1f}" fill="{T2}">{sub}</text>'
            + f'<rect x="{cursor_x:.1f}" y="{sy - sub_size*0.85:.1f}" width="{sub_size*0.62:.1f}" height="{sub_size*1.05:.1f}" fill="{C}"/>')


def chip(x, y, label, col, fs=22):
    pad, hgt, cut = fs * 0.9, fs * 2.0, fs * 0.55
    w = pad + len(label) * fs * 0.60 + pad
    return (f'<path d="M{x} {y}H{x+w-cut}L{x+w} {y+cut}V{y+hgt}H{x+cut}L{x} {y+hgt-cut}Z" fill="{col}" fill-opacity="0.08" stroke="{col}" stroke-opacity="0.75" stroke-width="1.5"/>'
            f'<text x="{x+pad}" y="{y+hgt*0.66:.1f}" font-family="JetBrains Mono" font-size="{fs}" font-weight="700" fill="{col}">{label}</text>', w)


def chips_row(cx, y, items):
    parts, widths = [], []
    for label, col in items:
        p, w = chip(0, 0, label, col)
        widths.append(w)
    total = sum(widths) + 18 * (len(items) - 1)
    x = cx - total / 2
    for (label, col), w in zip(items, widths):
        p, _ = chip(x, y, label, col)
        parts.append(p)
        x += w + 18
    return "".join(parts)


def save(name, w, h, body):
    s = svg(w, h, body)
    p = os.path.join(OUT, name)
    open(p + ".svg", "w").write(s)
    cairosvg.svg2png(bytestring=s.encode(), write_to=p + ".png", output_width=w, output_height=h)


FEATURES = [("SAFE DEPLOY", C), ("PROFILES", Y), ("COLLECTIONS", C), ("CONFLICTS", "#FF9F1C"), ("REDMOD + FOMOD", Y), ("UNDO", "#39FF8A")]

# 1) logo mark, square, dark background (Nexus API Access page, GitHub avatar, app icon source)
save("afterlife-logo-512", 512, 512,
     f'<rect width="512" height="512" fill="{BG}"/><rect width="512" height="512" fill="url(#grid)"/>'
     f'<ellipse cx="256" cy="256" rx="240" ry="240" fill="url(#glowC)"/>' + logo(76, 76, 360))
save("afterlife-logo-transparent-512", 512, 512, logo(16, 16, 480))

# 2) Nexus Mods page header 1400 x 400 (Nexus puts the mod title over the lower left; the lockup sits right of centre)
W, H = 1400, 400
save("nexus-header-1400x400", W, H,
     background(W, H, 7, 520, 190, 860, (120, 270)) + lockup(760, 190, 118, 104, "h") + hud_corners(W, H, 18))

# 3) Nexus Mods primary image / gallery cover 1920 x 1080 (16:9, shown as the thumbnail in search)
W, H = 1920, 1080
save("nexus-cover-1920x1080", W, H,
     background(W, H, 11, 660, 430, 1180, (300, 900)) + lockup(960, 430, 190, 168, "c")
     + chips_row(960, 680, FEATURES)
     + f'<text x="960" y="832" text-anchor="middle" font-family="Chakra Petch" font-weight="600" font-size="34" fill="{T}">Your mods stay safe in the library. The game only gets what you load to the deck.</text>'
     + f'<text x="960" y="884" text-anchor="middle" font-family="JetBrains Mono" font-size="20" fill="{T2}">free · open source · MIT · Windows</text>'
     + hud_corners(W, H, 28, C, 44))

# 4) GitHub social preview 1280 x 640
W, H = 1280, 640
save("github-social-1280x640", W, H,
     background(W, H, 5, 440, 270, 780, (180, 540)) + lockup(640, 270, 132, 116, "g")
     + f'<text x="640" y="470" text-anchor="middle" font-family="Chakra Petch" font-weight="600" font-size="28" fill="{T}">Safe deploy · profiles · Nexus collections · conflicts · undo</text>'
     + f'<text x="640" y="516" text-anchor="middle" font-family="JetBrains Mono" font-size="18" fill="{T2}">github.com/silentbobcz/afterlife</text>'
     + hud_corners(W, H, 20))

# 5) small square thumbnail 400 x 400 (forums, socials)
W, H = 400, 400
save("afterlife-avatar-400", W, H,
     f'<rect width="{W}" height="{H}" fill="{BG}"/><rect width="{W}" height="{H}" fill="url(#grid)"/>'
     f'<ellipse cx="200" cy="170" rx="190" ry="160" fill="url(#glowC)"/>' + logo(110, 52, 180)
     + wordmark(203, 322, 54, "a", glow=True, anchor="middle") + hud_corners(W, H, 12, C, 18))
print("ok", sorted(os.listdir(OUT)))
