import os
import cdsapi
import xarray as xr
import pandas as pd
import numpy as np
import zipfile

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
os.environ["CDSAPI_RC"] = os.path.join(BASE_DIR, ".cdsapirc")

# Файли
surf_file = os.path.join(BASE_DIR, "era5_surface_2025_01_01_test.nc")
upper_file = os.path.join(BASE_DIR, "era5_upper_2025_01_01_test.nc")

# Локація
LOCATION = {"name": "Ternopil", "lat": 49.55, "lon": 25.60}

# Параметри
YEAR = 2025
MONTH = "01"
DAY = "01"
HOURS = ["00:00", "01:00", "02:00"]

# === 1. Завантаження ERA5 ===
def download_era5_surface():
    c = cdsapi.Client()
    print(f"Downloading surface data for {YEAR}-{MONTH}-{DAY} hours {HOURS} ...")
    c.retrieve(
        "reanalysis-era5-single-levels",
        {
            "variable": [
                "2m_temperature",
                "10m_u_component_of_wind",
                "10m_v_component_of_wind",
                "mean_sea_level_pressure",
                "total_cloud_cover",
                "surface_solar_radiation_downwards",
                "total_precipitation",
            ],
            "product_type": "reanalysis",
            "year": str(YEAR),
            "month": MONTH,
            "day": [DAY],
            "time": HOURS,
            "area": [49.75, 25.25, 49.25, 26.00],
            "format": "netcdf",
        },
        surf_file
    )
    print("Surface data downloaded!")

def download_era5_upper():
    c = cdsapi.Client()
    print(f"Downloading upper-air data for {YEAR}-{MONTH}-{DAY} hours {HOURS} ...")
    c.retrieve(
        "reanalysis-era5-pressure-levels",
        {
            "variable": [
                "temperature",
                "u_component_of_wind",
                "v_component_of_wind",
                "geopotential",
            ],
            "pressure_level": ["1000", "925", "850"],
            "product_type": "reanalysis",
            "year": str(YEAR),
            "month": MONTH,
            "day": [DAY],
            "time": HOURS,
            "area": [49.75, 25.25, 49.25, 26.00],
            "format": "netcdf",
        },
        upper_file
    )
    print("Upper-air data downloaded!")

# === 2. Перевірка і розпакування ZIP файлу ===
def extract_if_zip(file_path):
    if zipfile.is_zipfile(file_path):
        with zipfile.ZipFile(file_path, 'r') as zip_ref:
            zip_ref.extractall(BASE_DIR)
            extracted_files = zip_ref.namelist()
            print(f"{file_path} is a ZIP. Extracted: {extracted_files}")
            for f in extracted_files:
                if f.endswith(".nc"):
                    return os.path.join(BASE_DIR, f)
        return None
    else:
        return file_path
def parse_surface(file_path):
    file_path = extract_if_zip(file_path)
    if not file_path or not os.path.exists(file_path):
        print(f"Файл {file_path} не знайдено або не NetCDF.")
        return None

    ds = xr.open_dataset(file_path)
    lat = float(ds.latitude.sel(latitude=LOCATION["lat"], method="nearest"))
    lon = float(ds.longitude.sel(longitude=LOCATION["lon"], method="nearest"))

    df = ds.sel(latitude=lat, longitude=lon).to_dataframe().reset_index()
    df["datetime"] = pd.to_datetime(df["valid_time"])
    df["temperature_2m_C"] = df["t2m"] - 273.15
    df["wind_speed_10m"] = np.sqrt(df["u10"]**2 + df["v10"]**2)
    df["wind_dir_10m"] = (180 + np.degrees(np.arctan2(df["u10"], df["v10"]))) % 360
    df["pressure_hPa"] = df["msl"] / 100.0

    print("\n=== SURFACE DATA SAMPLE ===")
    # Виводимо лише наявні змінні
    columns_to_show = ["datetime","temperature_2m_C","wind_speed_10m","wind_dir_10m","pressure_hPa"]
    if "tcc" in df.columns:
        columns_to_show.append("tcc")
    print(df[columns_to_show])
    return df

def parse_upper(file_path):
    file_path = extract_if_zip(file_path)
    if not file_path or not os.path.exists(file_path):
        print(f"Файл {file_path} не знайдено або не NetCDF.")
        return None

    ds = xr.open_dataset(file_path)
    lat = float(ds.latitude.sel(latitude=LOCATION["lat"], method="nearest"))
    lon = float(ds.longitude.sel(longitude=LOCATION["lon"], method="nearest"))

    df = ds.sel(latitude=lat, longitude=lon).to_dataframe().reset_index()
    df["datetime"] = pd.to_datetime(df["valid_time"])
    df["temperature_C"] = df["t"] - 273.15
    df["wind_speed"] = np.sqrt(df["u"]**2 + df["v"]**2)
    df["wind_dir"] = (180 + np.degrees(np.arctan2(df["u"], df["v"]))) % 360
    df["height_m"] = df["z"] / 9.80665

    print("\n=== UPPER-AIR DATA SAMPLE ===")
    print(df[["datetime","pressure_level","temperature_C","wind_speed","wind_dir","height_m"]])
    return df

# === 4. Перевірка файлів ===
def check_files():
    for f in [surf_file, upper_file]:
        exists = os.path.exists(f)
        size = os.path.getsize(f) if exists else 0
        valid = False
        extracted = extract_if_zip(f)
        if extracted and os.path.exists(extracted):
            try:
                xr.open_dataset(extracted).close()
                valid = True
            except:
                valid = False
        print(f"{f} -> exists: {exists}, size: {size} bytes, valid NetCDF: {valid}")

# === 5. Меню ===
def main():
    while True:
        print("\n=== MENU ===")
        print("1. Завантажити ERA5 файли")
        print("2. Пропарсити локальні файли")
        print("3. Перевірити наявність і валідність файлів")
        print("0. Вихід")
        choice = input("Виберіть опцію: ")

        if choice == "1":
            download_era5_surface()
            download_era5_upper()
        elif choice == "2":
            parse_surface(surf_file)
            parse_upper(upper_file)
        elif choice == "3":
            check_files()
        elif choice == "0":
            break
        else:
            print("Невірний вибір. Спробуйте ще раз.")

if __name__ == "__main__":
    main()
