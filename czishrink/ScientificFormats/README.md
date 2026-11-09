# Optional HDF5 and TIFF processing

Contributor: **Amin Khosrozadeh, PhD** — Prof. Vassiliki Nikoletopoulou lab,
DNF, University of Lausanne (UNIL).
[Lab](https://dnf-unil.ch/group/autophagic-mechanisms-of-protein-homeostasis-in-neurons/member/nikoletopoulou-vassiliki-nikoletopoulou)

Enable **Process HDF5** and/or **Process TIFF / OME-TIFF** in advanced options.
Both default to off. CZI retains its native engine. Compressed results keep their
original extensions and can be read by software supporting the chosen codec.
**Decompress all data** removes supported compression in the same native formats.
Levels are clamped to 1–9 for these formats; CZI retains its original range.

HDF5 uses official HDF5 1.14.6 tools with gzip and shuffle. `h5diff` checks decoded
data and attributes; Python additionally checks object/region reference targets
and selections in datasets and attributes. Nested reference containers and
oversized reference attributes/selections are rejected when repacking.
Fixed-width dataset values are additionally
compared bit-for-bit in bounded blocks, including floating-point NaN payloads and
signed zeros. External links/storage, virtual datasets and user-block metadata
are rejected. Long filter lists use an options file; very long lists containing
whitespace in dataset names are unsupported. Default compression modes leave
already-compressed datasets alone; checksum/shuffle-only datasets remain eligible.
**Compress all data** recompresses supported data.

TIFF uses `tifffile`/`imagecodecs` for lossless Deflate and suitable predictors.
Tests cover ordinary TIFF, BigTIFF, OME, multiple pages, SubIFD pyramids, private
tags, palette/RGB data and floating-point bits. Every page's pixels and metadata
are checked, including resolution and calibration units. Storage tags and offsets
are regenerated; unknown IFD/IFD8 pointers are rejected. Pixel processing uses
private disk-backed mappings, bounded comparison blocks and 256-pixel output
tiles. BigTIFF selection accounts for tile padding, metadata and compression
growth in both compression and decompression modes. Compressed strips/tiles
exceeding the 64 MiB decode limit are rejected; scratch disk space is needed for
decoded pages. Unsupported proprietary layouts,
EXIF/GPS pointers, volumetric pages and implicit ImageJ hyperstacks fail explicitly.
In **Compress uncompressed**, fully compressed TIFFs are copied unchanged before
rewrite-only metadata checks, including TIFFs with EXIF metadata. Their bytes are
verified by SHA-256. Rewriting such metadata still requires a dedicated converter.
Mixed-compression TIFFs report unsupported; use **Compress all** for mixed files.

Inputs are opened read-only. Private destination staging space prevents overwrites
and preserves existing files. Dry Run bypasses staging. Cancellation kills and
waits for the worker and its child tools. Observer failures also stop and wait
for the worker before releasing scratch space. Cleanup failures are traced without
replacing an earlier processing error; locked scratch files may remain. CSV writer
failures are traced and available through `CsvLogFileWriter.LastError`; close is
always attempted. A larger compression result is replaced
with a verified original-file copy. Per-file reports append method, verification,
SHA-256 hashes and destination while retaining the original eight CSV columns.
Decoded data equivalence does not promise byte-identical container restoration.
CZI reports explicitly state that its pixels are not independently verified.

HDF5-based BigDataViewer and OME-TIFF occur in microscopy workflows supported by
[ZEISS arivis Pro](https://www.zeiss.com/microscopy/en/products/software/arivis-pro.html?hsLang=en).
[BigDataViewer documents XML/HDF5 and a ZEISS Lightsheet Z.1 example](https://imagej.net/plugins/bdv/).
For paired datasets, keep companion XML files and relative paths together; copying
accompanying files is available through **Copy other files and folders**.
Enabled HDF5/TIFF processing takes priority over copying; unselected formats are
copied unchanged when that option is enabled. Generic HDF5/TIFF
checks do not establish compatibility with every vendor-specific reader.
ZEISS viewer validation remains a manual follow-up.

## Windows build and tests

Use the SDK in `../global.json` (8.0.427). Run `./prepare-windows-tools.ps1` from
PowerShell. It prepares an isolated Python 3.13.17 runtime, four pinned packages,
and official HDF5 tools under `../../artifacts/hdf5-tiff-tools` without a system
installation or administrator access. Dependencies retain their bundled licenses.
Source locations: [Python](https://www.python.org/downloads/release/python-31317/),
[HDF5](https://github.com/HDFGroup/hdf5/releases/tag/hdf5_1.14.6), and each pinned
package's source distribution and upstream project links on PyPI.

From `czishrink`, publish:

```powershell
dotnet publish netczicompress.Desktop/netczicompress.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../artifacts/publish
```

The desktop project bundles the prepared tools and worker into `FormatTools`.
Keep the complete published folder together. Override `ScientificToolsDirectory`
at build time or `CZISHRINK_FORMAT_TOOLS` at runtime when using a different tools
directory. Missing runtimes produce explicit errors; CZI works without Python.

```powershell
$env:CZISHRINK_FORMAT_TOOLS = (Resolve-Path ../artifacts/hdf5-tiff-tools).Path
& "$env:CZISHRINK_FORMAT_TOOLS/python/python.exe" -B ScientificFormats/tests/test_formats.py -v
```

For the Windows process-lifetime regression tests, also set
`CZISHRINK_TEST_PYTHON` to that local `python.exe` before running `dotnet test`.
These tests use isolated synthetic workers to check cancellation, callback errors,
child-process shutdown and locked scratch files. They explicitly skip without
that Windows runtime. Other .NET tests cover enumeration, disposal, progress and
CSV failure precedence.

Linux developers can prepare a compatible Python environment at
`$CZISHRINK_FORMAT_TOOLS/python/bin/python3`, install the pinned requirements,
and install `h5repack`/`h5diff` on PATH. Automatic portable-runtime packaging is
currently configured for Windows; Linux codec/runtime validation is pending.
All development tests use synthetic files or existing local copies. NAS is
read-only and is never an output or cleanup target.

The local integration harness creates synthetic fixtures with
`tests/create_integration_fixtures.py <new-input-directory>`. Restore
`IntegrationChecks/IntegrationChecks.csproj` with `dotnet restore --locked-mode`,
then run it with `dotnet run --project IntegrationChecks/IntegrationChecks.csproj
-c Release --no-restore -- <input-directory> <new-output-directory>`, keeping
`CZISHRINK_FORMAT_TOOLS` set. It checks native CZI/HDF5/TIFF processing, both
report directions, verification evidence, cancellation and missing runtimes.
