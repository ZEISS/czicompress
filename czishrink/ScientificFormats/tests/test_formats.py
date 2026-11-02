# SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
# SPDX-License-Identifier: GPL-3.0-or-later
import contextlib
import io
import os
import stat
from pathlib import Path
import sys
import tempfile
import unittest
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import h5py
import numpy as np
import tifffile
import scientific_worker as worker


class Formats(unittest.TestCase):
    def test_hdf5_checksum_only_datasets_are_compressed(self):
        source = self.root / 'checksum.h5'
        with h5py.File(source, 'w') as handle:
            handle.create_dataset('pixels', data=np.zeros(20000), fletcher32=True)
        output, _ = self.run_file(source, 'CompressUncompressed')
        with h5py.File(output, 'r') as handle:
            self.assertEqual(handle['pixels'].compression, 'gzip')
            self.assertTrue(handle['pixels'].fletcher32)

    def test_tiff_unrelocatable_pointer_datatypes_are_rejected(self):
        for datatype, bigtiff in ((13, False), (18, True)):
            with self.subTest(datatype=datatype):
                source = self.root / f'pointer-{datatype}.tif'
                tifffile.imwrite(source, np.zeros((128, 128), dtype='uint16'), bigtiff=bigtiff,
                                 metadata=None, extratags=[(65001, datatype, 1, 123456, False)])
                before = worker.file_hash(source)
                destination = self.root / f'pointer-{datatype}-output.tif'
                with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(ValueError, 'IFD/IFD8'):
                    worker.process(source, destination, 'CompressAll', 4)
                self.assertFalse(destination.exists())
                self.assertEqual(worker.file_hash(source), before)

    def test_tiff_calibration_changes_fail_verification(self):
        source, changed = self.root / 'calibrated.tif', self.root / 'changed.tif'
        pixels = np.zeros((64, 64), dtype='uint16')
        tifffile.imwrite(source, pixels, metadata=None, resolution=((200, 3), (400, 7)), resolutionunit='CENTIMETER')
        for resolution, unit in ((((201, 3), (400, 7)), 'CENTIMETER'),
                                  (((200, 3), (401, 7)), 'CENTIMETER'),
                                  (((200, 3), (400, 7)), 'INCH')):
            with self.subTest(resolution=resolution, unit=unit):
                tifffile.imwrite(changed, pixels, metadata=None, resolution=resolution, resolutionunit=unit)
                with self.assertRaisesRegex(ValueError, 'metadata tag changed'):
                    worker.verify_tiff(source, changed)

    def test_tiff_mixed_pages_are_explicitly_unsupported_in_uncompressed_modes(self):
        source = self.root / 'mixed.tif'
        with tifffile.TiffWriter(source) as writer:
            writer.write(np.zeros((256, 256), dtype='uint16'), metadata=None)
            writer.write(np.zeros((64, 64), dtype='uint16'), compression='deflate', metadata=None)
        for mode in ('CompressUncompressed', 'CompressUncompressedAndZstd'):
            destination = self.root / (mode + '.tif')
            with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(ValueError, 'Mixed compressed'):
                worker.process(source, destination, mode, 4)
            self.assertFalse(destination.exists())
        output, result = self.run_file(source, 'CompressAll')
        self.assertTrue(result['verified'])
        worker.verify_tiff(source, output)

    def test_tiff_already_compressed_is_copied_in_uncompressed_mode(self):
        source = self.root / 'compressed.tif'
        tifffile.imwrite(source, np.zeros((256, 256), dtype='uint16'), compression='deflate', metadata=None)
        output, result = self.run_file(source, 'CompressUncompressed')
        self.assertTrue(result['unchanged'])
        self.assertEqual(worker.file_hash(source), worker.file_hash(output))

    def test_tiff_pixel_verification_detects_float_payload_changes(self):
        source, changed = self.root / 'bits.tif', self.root / 'changed-bits.tif'
        bits = np.zeros((64, 64), dtype='uint32')
        bits[0, 0] = 0x7FC00001
        tifffile.imwrite(source, bits.view('float32'), metadata=None)
        bits[0, 0] = 0x7FC00002
        tifffile.imwrite(changed, bits.view('float32'), metadata=None)
        with self.assertRaisesRegex(ValueError, 'pixel verification'):
            worker.verify_tiff(source, changed)

    def test_tiff_large_pages_use_disk_mapping_for_decode_and_verification(self):
        source = self.root / 'large.tif'
        tifffile.imwrite(source, np.zeros((4096, 4096), dtype='uint16'), rowsperstrip=32, metadata=None)
        original = tifffile.TiffPage.asarray
        calls = []
        def bounded(page, **kwargs):
            self.assertIsInstance(kwargs.get('out'), np.memmap)
            self.assertEqual(kwargs.get('maxworkers'), 1)
            calls.append(page.shape)
            return original(page, **kwargs)
        with mock.patch.object(tifffile.TiffPage, 'asarray', bounded):
            output, result = self.run_file(source)
        self.assertTrue(result['verified'])
        self.assertGreaterEqual(len(calls), 6)
        self.assertLess(output.stat().st_size, source.stat().st_size)

    def test_tiff_oversized_codec_segment_fails_without_partial_output(self):
        source, destination = self.root / 'segment.tif', self.root / 'segment-output.tif'
        tifffile.imwrite(source, np.zeros((256, 256), dtype='uint16'), compression='deflate', metadata=None)
        with mock.patch.object(worker, 'TIFF_MAX_SEGMENT_BYTES', 1024):
            with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(ValueError, 'bounded-memory'):
                worker.process(source, destination, 'CompressAll', 4)
        self.assertFalse(destination.exists())

    def test_tiff_planar_rgb_and_bilevel_tiles_preserve_pixels(self):
        planar = self.root / 'planar.tif'
        pixels = np.arange(3 * 301 * 277, dtype='uint16').reshape(3, 301, 277)
        tifffile.imwrite(planar, pixels, photometric='rgb', planarconfig='separate', metadata=None)
        output, _ = self.run_file(planar)
        worker.verify_tiff(planar, output)
        bilevel = self.root / 'bilevel.tif'
        tifffile.imwrite(bilevel, np.zeros((301, 277), dtype='bool'), metadata=None)
        output, _ = self.run_file(bilevel)
        worker.verify_tiff(bilevel, output)

    def test_read_only_sources_keep_data_and_permissions(self):
        hdf5 = self.root / 'readonly.h5'
        tiff = self.root / 'readonly.tiff'
        values = np.arange(65536, dtype='uint16').reshape(256, 256)
        with h5py.File(hdf5, 'w') as handle:
            handle.create_dataset('pixels', data=values)
        tifffile.imwrite(tiff, values, metadata=None)
        for source in (hdf5, tiff):
            before = worker.file_hash(source)
            os.chmod(source, 0o444)
            permissions = source.stat().st_mode
            try:
                output, _ = self.run_file(source)
                self.assertEqual(worker.file_hash(source), before)
                self.assertEqual(source.stat().st_mode, permissions)
                if os.name != 'nt':
                    self.assertEqual(output.stat().st_mode & stat.S_IWOTH, permissions & stat.S_IWOTH)
            finally:
                os.chmod(source, 0o666)

    def setUp(self):
        self.folder = tempfile.TemporaryDirectory()
        self.root = Path(self.folder.name)

    def tearDown(self):
        self.folder.cleanup()

    def run_file(self, source, mode='CompressAll'):
        destination = source.with_name(source.stem + '-converted' + source.suffix)
        with contextlib.redirect_stdout(io.StringIO()):
            result = worker.process(source, destination, mode, 4)
        self.assertTrue(destination.is_file())
        self.assertEqual(result['input_sha256'], worker.file_hash(source))
        self.assertEqual(result['output_sha256'], worker.file_hash(destination))
        if source.suffix.lower() in ('.tif', '.tiff') and mode != 'Decompress':
            roundtrip = source.with_name(source.stem + '-roundtrip' + source.suffix)
            with contextlib.redirect_stdout(io.StringIO()):
                worker.process(destination, roundtrip, 'Decompress', 4)
            worker.verify_tiff(source, roundtrip)
        return destination, result

    def test_hdf5_data_attributes_hard_soft_and_object_references(self):
        source = self.root / 'recording.h5'
        with h5py.File(source, 'w') as handle:
            data = handle.create_dataset('recording/samples', data=np.arange(200000, dtype='uint16').reshape(1000, 200))
            data.attrs['units'] = 'microvolts'
            handle.attrs['sampling_rate'] = 20000
            handle['same_samples'] = data
            handle['soft'] = h5py.SoftLink('/recording/samples')
            refs = handle.create_dataset('references', (1,), dtype=h5py.ref_dtype)
            refs[0] = data.ref
            handle.create_dataset('scalar', data=42)
            handle.create_dataset('empty', shape=(0,), dtype='float64')
        output, result = self.run_file(source)
        self.assertTrue(result['verified'])
        with h5py.File(source, 'r') as before, h5py.File(output, 'r') as after:
            np.testing.assert_array_equal(before['recording/samples'][:], after['recording/samples'][:])
            self.assertEqual(after['recording/samples'].compression, 'gzip')
            self.assertTrue(after['recording/samples'].shuffle)
            self.assertEqual(after['recording/samples'].attrs['units'], 'microvolts')
            self.assertEqual(after['same_samples'].id, after['recording/samples'].id)
            np.testing.assert_array_equal(after[after['references'][0]][:], after['recording/samples'][:])
        decompressed, _ = self.run_file(output, 'Decompress')
        with h5py.File(decompressed, 'r') as handle:
            self.assertIsNone(handle['recording/samples'].compression)

    def test_hdf5_already_compressed_is_unchanged_in_default_mode(self):
        source = self.root / 'compressed.h5'
        with h5py.File(source, 'w') as handle:
            handle.create_dataset('pixels', data=np.zeros(20000), compression='gzip')
        output, result = self.run_file(source, 'CompressUncompressed')
        self.assertTrue(result['unchanged'])
        self.assertEqual(source.read_bytes(), output.read_bytes())

    def test_hdf5_float_payloads_and_signed_zero_are_bit_exact(self):
        source = self.root / 'floats.h5'
        bits = np.zeros(20000, dtype='uint32')
        bits[:4] = [0x7FC00001, 0x7F800000, 0xFF800000, 0x80000000]
        with h5py.File(source, 'w') as handle:
            handle.create_dataset('values', data=bits.view('float32'))
        output, _ = self.run_file(source)
        restored, _ = self.run_file(output, 'Decompress')
        worker.verify_hdf5(source, restored)
        with h5py.File(restored, 'r') as handle:
            self.assertEqual(handle['values'][:].tobytes(), bits.tobytes())

    def test_hdf5_long_dataset_list_uses_options_file(self):
        source = self.root / 'many-datasets.h5'
        with h5py.File(source, 'w') as handle:
            for index in range(450):
                handle.create_dataset(f'group/long_recording_dataset_name_{index:04d}', data=np.zeros(8192, dtype='uint16'))
        output, result = self.run_file(source)
        self.assertTrue(result['verified'])
        self.assertEqual(result['datasets'], 450)
        with h5py.File(output, 'r') as handle:
            self.assertEqual(handle['group/long_recording_dataset_name_0000'].compression, 'gzip')

    def test_hdf5_external_links_fail_without_output(self):
        source = self.root / 'external.h5'
        with h5py.File(source, 'w') as handle:
            handle['outside'] = h5py.ExternalLink('missing.h5', '/pixels')
        output = self.root / 'failed.h5'
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(ValueError, 'external links'):
            worker.process(source, output, 'CompressAll', 1)
        self.assertFalse(output.exists())

    def test_hdf5_user_block_is_not_silently_discarded(self):
        source = self.root / 'user-block.h5'
        with h5py.File(source, 'w', userblock_size=512) as handle:
            handle.create_dataset('pixels', data=np.zeros(2000))
        with source.open('r+b') as stream:
            stream.write(b'important vendor preamble')
        output = self.root / 'failed.h5'
        with contextlib.redirect_stdout(io.StringIO()), self.assertRaisesRegex(ValueError, 'user-block'):
            worker.process(source, output, 'CompressAll', 1)
        self.assertFalse(output.exists())

    def test_tiff_pixels_and_private_metadata(self):
        source = self.root / 'image.tif'
        pixels = np.arange(512 * 512, dtype='uint16').reshape(512, 512)
        tifffile.imwrite(source, pixels, metadata=None, description='private experiment metadata',
                         resolution=((200, 3), (400, 7)), resolutionunit='CENTIMETER',
                         extratags=[(65000, 'B', 7, b'private', False)])
        output, result = self.run_file(source)
        self.assertTrue(result['verified'])
        np.testing.assert_array_equal(tifffile.imread(output), pixels)
        with tifffile.TiffFile(output) as handle:
            self.assertEqual(handle.pages[0].tags[65000].value, b'private')
            self.assertEqual(handle.pages[0].description, 'private experiment metadata')
            self.assertNotEqual(int(handle.pages[0].compression), 1)
        decompressed, _ = self.run_file(output, 'Decompress')
        with tifffile.TiffFile(decompressed) as handle:
            self.assertEqual(int(handle.pages[0].compression), 1)

    def test_tiff_rgb_big_endian_bigtiff(self):
        source = self.root / 'rgb.tiff'
        pixels = np.zeros((128, 128, 3), dtype='uint16')
        pixels[:, :, 1] = 1234
        tifffile.imwrite(source, pixels, photometric='rgb', byteorder='>', bigtiff=True, metadata=None)
        output, _ = self.run_file(source)
        with tifffile.TiffFile(output) as handle:
            self.assertTrue(handle.is_bigtiff)
            self.assertEqual(handle.byteorder, '>')
            np.testing.assert_array_equal(handle.asarray(), pixels)

    def test_tiff_multipage_and_ome_metadata(self):
        source = self.root / 'stack.ome.tif'
        pixels = np.arange(4 * 128 * 128, dtype='uint16').reshape(4, 128, 128)
        tifffile.imwrite(source, pixels, ome=True, metadata={'axes': 'ZYX'})
        output, _ = self.run_file(source)
        worker.verify_tiff(source, output)
        with tifffile.TiffFile(source) as first, tifffile.TiffFile(output) as second:
            self.assertEqual(first.ome_metadata, second.ome_metadata)

    def test_tiff_pyramid_subifds(self):
        source = self.root / 'pyramid.tif'
        pixels = np.arange(256 * 256, dtype='uint16').reshape(256, 256)
        with tifffile.TiffWriter(source) as writer:
            writer.write(pixels, subifds=1, metadata=None)
            writer.write(pixels[::2, ::2], subfiletype=1, metadata=None)
        output, _ = self.run_file(source)
        worker.verify_tiff(source, output)
        with tifffile.TiffFile(output) as handle:
            self.assertEqual(len(handle.pages[0].pages), 1)

    def test_tiff_float_nan_bits_preserved(self):
        source = self.root / 'floats.tif'
        pixels = np.zeros((128, 128), dtype='float32')
        pixels[0, :4] = [np.nan, np.inf, -np.inf, -0.0]
        tifffile.imwrite(source, pixels, metadata=None)
        output, _ = self.run_file(source)
        self.assertEqual(tifffile.imread(output).tobytes(), pixels.tobytes())

    def test_tiff_palette(self):
        source = self.root / 'palette.tif'
        pixels = np.zeros((256, 256), dtype='uint8')
        palette = np.tile(np.arange(256, dtype='uint16') * 257, (3, 1))
        tifffile.imwrite(source, pixels, photometric='palette', colormap=palette, metadata=None)
        output, _ = self.run_file(source)
        worker.verify_tiff(source, output)

    def test_existing_destination_and_same_file_rejected(self):
        source = self.root / 'source.tif'
        tifffile.imwrite(source, np.zeros((8, 8), dtype='uint8'))
        original = source.read_bytes()
        with self.assertRaises(ValueError):
            worker.process(source, source, 'CompressAll', 1)
        self.assertEqual(source.read_bytes(), original)
        destination = self.root / 'keep.tif'
        destination.write_bytes(b'keep me')
        with self.assertRaises(ValueError):
            worker.process(source, destination, 'CompressAll', 1)
        self.assertEqual(destination.read_bytes(), b'keep me')


if __name__ == '__main__':
    unittest.main()
