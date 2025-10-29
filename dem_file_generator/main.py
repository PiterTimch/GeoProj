import numpy as np
import matplotlib.pyplot as plt

# Параметри DEM
width = 100
height = 100
pixel_size = 1.0
slope_deg = 20

# Центр горбка
cx, cy = width / 2, height / 2
radius = min(width, height) / 2
slope_rad = np.deg2rad(slope_deg)

# Генерація DEM
y, x = np.meshgrid(np.arange(height), np.arange(width))
dist = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
dem = np.tan(slope_rad) * (radius - dist)
dem[dem < 0] = 0

# Зберігання у AERMAP ASCII DEM
dem_file = "NEWARK-E.DEM"
nrows, ncols = dem.shape
xllcorner = 0
yllcorner = 0
nodata = -9999

with open(dem_file, "w", newline="\r\n") as f:  # важливо для DOS line endings
    f.write(f"{ncols} {nrows}\r\n")
    f.write(f"{xllcorner} {yllcorner}\r\n")
    f.write(f"{pixel_size}\r\n")
    f.write(f"{nodata}\r\n")
    for row in dem[::-1]:
        f.write(" ".join(str(int(v)) for v in row) + "\r\n")  # цілі числа

print(f"AERMAP DEM готовий: {dem_file}")

# Візуалізація
plt.imshow(dem, cmap='terrain')
plt.colorbar(label='Висота')
plt.title('DEM з горбком 20°')
plt.show()
