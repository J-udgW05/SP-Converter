<p align="center">
  <img src="src/Assets/Logo.png" alt="" width="128" />
</p>
<h1 align="center">Simple Photo Converter</h1>
<p align="center">
SP Converter is a Windows application for fast, multithreaded image format conversion. It converts single files or whole folders and makes effective use of multi-core processors.
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10.0-512BD4" />
  <a href="LICENSE"><img alt="AGPL-3.0 licence" src="https://img.shields.io/badge/license-AGPL--3.0-blue" /></a>
  <img alt="Windows x64" src="https://img.shields.io/badge/Windows-10%2F11%20-0078D4" />
</p>

---

SP Converter converts images between formats - a single file or a whole folder, including
subfolders. Files are processed in parallel on all processor cores, and the program works
offline.

It never overwrites anything: when a name is taken, the new file gets a `_1`, `_2` suffix, and
results already in the destination folder are not picked up again. Deleting originals is off by
default; when enabled, a source file goes to the Recycle Bin, and only after all of its results
have been written.

Images are rotated by their EXIF orientation, transparency is replaced with white for formats
that cannot store it, and CMYK is converted to sRGB for screen formats. Animation frames,
PDF and TIFF pages and icon sizes can be extracted as separate images. PDF pages are rendered
at 300 DPI with the bundled Ghostscript.

## Supported formats

Read and write: JPG, PNG, WEBP, AVIF, JXL, GIF, TIFF, BMP, TGA, ICO, PSD, DDS, EXR, PDF, SVG,
PPM, PGM, PBM.
Read only: HEIC, HEIF and RAW photos (CR2, CR3, NEF, ARW, DNG).

## Download

The installer and the portable build are on the [Releases](https://github.com/J-udgW05/SP-Converter/releases/latest)
page and on the [project website](https://j-udgw05.github.io/SP-Converter/). Windows 10 or 11, 64-bit;
no .NET installation required.

> [!WARNING]
> The build is not signed with a code-signing certificate. Windows SmartScreen may show
> "Windows protected your PC" with "Unknown publisher", and some antivirus programs may warn about
> the file. To run it, choose "More info" and then "Run anyway".

> [!NOTE]
> The build includes [Ghostscript](https://www.ghostscript.com/) (© Artifex Software), licensed
> under AGPL-3.0, for reading PDF files. Details are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Building from source

```bash
dotnet build SPConverter.slnx -c Release
dotnet run --project src -c Release
```

Installer and portable ZIP (PowerShell 7; Inno Setup is installed automatically if missing):

```powershell
.\build-release.ps1
```

Publishing a release (raise `<Version>` in `src/SPConverter.csproj` first; needs `gh auth login`):

```powershell
.\publish-release.ps1
```

## Licence

[AGPL-3.0](LICENSE). The build includes Ghostscript (AGPL-3.0) for reading PDF files; other
components and their licences are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

---

<h3 align="center">Support the project</h3>

<p align="center">
  SP Converter is free and developed by one person in their spare time.<br />
  If this helps you save time or is useful to you, please consider giving the repository a star.<br />
  It helps other people find the app and keeps the project going.
</p>

<p align="center">
  <a href="https://github.com/J-udgW05/SP-Converter/stargazers"><img alt="Star on GitHub" src="https://img.shields.io/github/stars/J-udgW05/SP-Converter?style=social" /></a>
</p>
