import os
import zipfile
import xarray as xr

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
surf_file = os.path.join(BASE_DIR, "era5_surface_2025_01_01_test.nc")
upper_file = os.path.join(BASE_DIR, "era5_upper_2025_01_01_test.nc")

def extract_if_zip(file_path):
    """Якщо файл ZIP, розпакувати та повернути шлях до NetCDF"""
    if zipfile.is_zipfile(file_path):
        with zipfile.ZipFile(file_path, 'r') as zip_ref:
            zip_ref.extractall(BASE_DIR)
            for f in zip_ref.namelist():
                if f.endswith(".nc"):
                    extracted_file = os.path.join(BASE_DIR, f)
                    print(f"Extracted {extracted_file}")
                    return extracted_file
    return file_path

def inspect_file(file_path):
    file_path = extract_if_zip(file_path)
    if not os.path.exists(file_path):
        print(f"Файл {file_path} не знайдено")
        return
    ds = xr.open_dataset(file_path)
    print(f"\n=== INSPECTING {file_path} ===")
    print(ds)
    print("\nVariables:")
    for var in ds.variables:
        print(f" - {var}: dims={ds[var].dims}, shape={ds[var].shape}")

def main():
    inspect_file(surf_file)
    inspect_file(upper_file)

if __name__ == "__main__":
    main()
