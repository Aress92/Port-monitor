"""Generate app.ico for Port Monitor - network port monitoring tool."""
from PIL import Image, ImageDraw
import math

SIZES = [16, 24, 32, 48, 64, 128, 256]


def draw_icon(size: int) -> Image.Image:
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    cx, cy = size / 2, size / 2
    margin = size * 0.06
    r = size / 2 - margin

    # Background circle - dark navy blue
    draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(25, 70, 140, 255))

    # Inner gradient ring
    inner_r = r * 0.88
    draw.ellipse(
        [cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r],
        fill=(30, 85, 165, 255),
    )

    # Connection lines + outer nodes (representing ports)
    angles = [30, 90, 150, 210, 270, 330]
    line_len = size * 0.28
    outer_r = max(2, size * 0.065)
    line_w = max(1, round(size * 0.035))
    node_color = (255, 255, 255, 240)
    line_color = (100, 210, 255, 220)

    for angle_deg in angles:
        angle = math.radians(angle_deg)
        ex = cx + line_len * math.cos(angle)
        ey = cy - line_len * math.sin(angle)

        # Connection line
        draw.line(
            [(round(cx), round(cy)), (round(ex), round(ey))],
            fill=line_color,
            width=line_w,
        )

        # Outer node dot
        draw.ellipse(
            [ex - outer_r, ey - outer_r, ex + outer_r, ey + outer_r],
            fill=node_color,
        )

    # Central node (bigger, brighter)
    center_r = max(2, size * 0.11)
    draw.ellipse(
        [cx - center_r, cy - center_r, cx + center_r, cy + center_r],
        fill=(80, 200, 255, 255),
    )

    # Outer ring
    ring_w = max(1, round(size * 0.025))
    ring_r = r * 0.92
    draw.ellipse(
        [cx - ring_r, cy - ring_r, cx + ring_r, cy + ring_r],
        outline=(80, 180, 255, 120),
        width=ring_w,
    )

    return img


# Generate each size separately and save as multi-size ICO
images = [draw_icon(s) for s in SIZES]

# Save using the correct approach for multi-size ICO
output_path = r"I:\Port-monitor\src\PortMonitor\Resources\app.ico"
images[-1].save(
    output_path,
    format="ICO",
    sizes=[(s, s) for s in SIZES],
    append_images=images[:-1],
)

# Verify
import os
file_size = os.path.getsize(output_path)
print(f"Icon created: {output_path}")
print(f"File size: {file_size:,} bytes")
print(f"Sizes: {SIZES}")
