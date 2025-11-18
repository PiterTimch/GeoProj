import os
import xarray as xr
import pandas as pd
import numpy as np
import zipfile

BASE_DIR = os.path.dirname(os.path.abspath(__file__))

SURF_NC = os.path.join(BASE_DIR, "era5_surface_2025_01_01_test.nc")
UPPER_NC = os.path.join(BASE_DIR, "era5_upper_2025_01_01_test.nc")

SITE_ID = "999999"
LAT, LON, ELEV = 49.55, 25.60, 324
START_DATE = "2025/01/01"
END_DATE = "2025/01/02"

def open_nc_safe(path):
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path, 'r') as z:
            nc_files = [f for f in z.namelist() if f.endswith('.nc')]
            z.extract(nc_files[0], BASE_DIR)
            path = os.path.join(BASE_DIR, nc_files[0])
    for engine in ['netcdf4', 'h5netcdf', None]:
        try:
            return xr.open_dataset(path, engine=engine) if engine else xr.open_dataset(path)
        except:
            continue
    raise ValueError(f"Cannot open: {path}")

def make_surface_file(path):
    """CD144 формат: фіксовані ширини полів"""
    ds = open_nc_safe(path)
    lat = float(ds.latitude.sel(latitude=LAT, method='nearest'))
    lon = float(ds.longitude.sel(longitude=LON, method='nearest'))
    ds = ds.sel(latitude=lat, longitude=lon)
    df = ds.to_dataframe().reset_index()
    
    df["datetime"] = pd.to_datetime(df.get("valid_time", df.get("time")))
    df["TEMP_F"] = ((df["t2m"] - 273.15) * 9/5 + 32).round(0).astype(int)  # °F
    df["DEWPT_F"] = (df["TEMP_F"] - 10).astype(int)  # спрощена оцінка
    df["WS_KT"] = (np.sqrt(df["u10"]**2 + df["v10"]**2) * 1.94384).round(0).astype(int)  # knots
    df["WD"] = ((180 + np.degrees(np.arctan2(df["u10"], df["v10"]))) % 360).round(0).astype(int)
    df = df.replace([np.inf, -np.inf], np.nan).fillna(0)
    
    out_file = os.path.join(BASE_DIR, "ternopil_surface.dat")
    with open(out_file, "w") as f:
        for _, row in df.iterrows():
            # CD144: WBAN DATE TIME TEMP DEWPT WD WS SKY VIS WEATHER
            line = (
                f"{SITE_ID:5s} "
                f"{row['datetime']:%y%m%d} "
                f"{row['datetime']:%H%M} "
                f"{int(row['TEMP_F']):3d} "
                f"{int(row['DEWPT_F']):3d} "
                f"{int(row['WD']):3d} "
                f"{int(row['WS_KT']):3d} "
                f"5 10 00\n"
            )
            f.write(line)
    
    print(f"[OK] Surface: {out_file} ({len(df)} records)")
    return out_file

def make_upper_file(path):
    """FSL формат: 3-рядковий header + дані"""
    ds = open_nc_safe(path)
    lat = float(ds.latitude.sel(latitude=LAT, method='nearest'))
    lon = float(ds.longitude.sel(longitude=LON, method='nearest'))
    ds = ds.sel(latitude=lat, longitude=lon)
    df = ds.to_dataframe().reset_index()
    
    df["datetime"] = pd.to_datetime(df.get("valid_time", df.get("time")))
    
    # Групуємо по часу - беремо всі доступні часові зрізи
    out_file = os.path.join(BASE_DIR, "ternopil_upper.fsl")
    
    level_col = next((c for c in ['level', 'pressure_level', 'isobaricInhPa'] if c in df.columns), None)
    if not level_col:
        raise ValueError(f"No level column")
    
    # Беремо всі унікальні часи
    times = df["datetime"].unique()
    
    with open(out_file, "w") as f:
        for time in times:
            df_time = df[df["datetime"] == time]
            df_time = df_time[df_time[level_col].isin([1000, 925, 850, 700, 500])]
            
            if df_time.empty:
                continue
            
            # FSL HEADER (3 рядки)
            f.write(f"  {SITE_ID}  TERNOPIL\n")
            f.write(f"  {pd.to_datetime(time):%Y %m %d %H}\n")
            f.write(f"  {LAT:6.2f} {LON:6.2f}\n")
            
            # Дані по рівнях
            for _, row in df_time.iterrows():
                temp_c = (row["t"] - 273.15)
                ws = np.sqrt(row["u"]**2 + row["v"]**2)
                wd = ((180 + np.degrees(np.arctan2(row["u"], row["v"]))) % 360)
                hght = row["z"] / 9.80665
                
                line = (
                    f"{int(row[level_col]):7d} "
                    f"{int(hght):7d} "
                    f"{temp_c:7.1f} "
                    f"{int(wd):6d} "
                    f"{ws:6.1f}\n"
                )
                f.write(line)
    
    print(f"[OK] Upper: {out_file} ({len(times)} soundings)")
    return out_file

def make_onsite_file():
    """Onsite формат: дата/час + дані"""
    out_file = os.path.join(BASE_DIR, "ternopil_onsite.dat")
    
    start = pd.to_datetime(START_DATE.replace('/', '-'))
    dates = pd.date_range(start, periods=48, freq='H')
    
    with open(out_file, "w") as f:
        for dt in dates:
            # Формат: YR YYYY MM DD HH HT01 WD01 WS01 SA01 TT01
            line = (
                f"  {dt.year-2000:2d} "
                f"{dt.year:4d} "
                f"{dt.month:4d} "
                f"{dt.day:4d} "
                f"{dt.hour:4d}"
                f"    10.0000"  # HT01 - висота 10м
                f"   180.0000"  # WD01 - напрямок 180°
                f"     5.0000"  # WS01 - швидкість 5 м/с
                f"   180.0000"  # SA01 - стандартне відхилення
                f"    15.0000"  # TT01 - температура 15°C
                f"\n"
            )
            f.write(line)
    
    print(f"[OK] Onsite: {out_file} ({len(dates)} records)")
    return out_file

def make_aermet_inp(surf_file, upper_file, onsite_file):
    """AERMET Stage 1 input"""
    inp_path = os.path.join(BASE_DIR, "aermet_stage1.inp")
    
    with open(inp_path, "w") as f:
        f.write("job\n")
        f.write("  messages  aermet_st1.msg\n")
        f.write("  report    aermet_st1.rpt\n")
        f.write("  \n")
        
        f.write("upperair\n")
        f.write("**          Upper air data for Ternopil from ERA5\n")
        f.write(f"  data      {os.path.basename(upper_file)}  fsl\n")
        f.write(f"  extract   ternopil_upper.iqa\n")
        f.write(f"  location  {SITE_ID}  {LAT:.2f}n  {LON:.2f}e  5 {ELEV:.1f}\n")
        f.write(f"  xdates    {START_DATE} to {END_DATE}\n")
        f.write(f"  qaout     ternopil_upper.oqa\n")
        f.write("\n")
        
        f.write("surface\n")
        f.write("**           Surface data for Ternopil from ERA5\n")
        f.write(f"   data      {os.path.basename(surf_file)}  CD144\n")
        f.write(f"   extract   ternopil_surf.iqa\n")
        f.write(f"   qaout     ternopil_surf.oqa\n")
        f.write(f"   location  {SITE_ID} {LAT:.2f}N {LON:.2f}E 0\n")
        f.write(f"   xdates    {START_DATE} TO {END_DATE}\n")
        f.write("\n")
        
        f.write("onsite\n")
        f.write(f"  data      {os.path.basename(onsite_file)}\n")
        f.write("\n")
        f.write(f"  location  000001   {LAT:.2f}n  {LON:.2f}e  0\n")
        f.write("\n")
        f.write(f"  xdates    {START_DATE}  {END_DATE}\n")
        f.write(f"  qaout     ternopil_onsite.oqa\n")
        f.write("  read      1  osyr  osmo  osdy  oshr\n")
        f.write("  read      2  HT01  WD01  WS01  SA01  TT01\n")
        f.write("                         \n")
        f.write("  format    1  ( 2X,I2,I4,I4,I4 )\n")
        f.write("  format    2  ( 5F10.4 )\n")
        f.write("\n")
        f.write("  threshold 0.3\n")
        f.write("\n")
        f.write("  range     tt    -30 <=  35  999\n")
        f.write("  range     ws      0 <   50  999\n")
        f.write("  range     wd      0 <= 360  999\n")
        f.write("  range     sa      0 <= 360  999\n")
        f.write("\n")
        f.write("  audit     sa\n")
        f.write("\n")
        
        f.write("end\n")
    
    print(f"[OK] INP: {inp_path}")
    return inp_path

def main():
    print("="*70)
    print("ERA5 → AERMET (ФІНАЛЬНА ВЕРСІЯ)")
    print("="*70)
    
    try:
        print("\n[1/4] Surface CD144...")
        surf = make_surface_file(SURF_NC)
        
        print("\n[2/4] Upper FSL...")
        upper = make_upper_file(UPPER_NC)
        
        print("\n[3/4] Onsite...")
        onsite = make_onsite_file()
        
        print("\n[4/4] AERMET INP...")
        make_aermet_inp(surf, upper, onsite)
        
        print("\n" + "="*70)
        print("SUCCESS! Запустіть: aermet aermet_stage1.inp")
        print("="*70)
    except Exception as e:
        print(f"\n[ERROR] {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    main()
