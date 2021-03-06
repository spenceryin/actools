﻿using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using JetBrains.Annotations;

namespace AcTools.Utils {
    public static partial class ImageUtils {
        private static bool? _isMagickSupported;

        public static bool IsMagickSupported => _isMagickSupported ?? (_isMagickSupported = CheckIfMagickSupported()).Value;

        private static bool CheckIfMagickSupported() {
            try {
                return TestImageMagick() != null;
            } catch (Exception) {
                return false;
            }
        }

        private static string _imageMagickAssemblyFilename;

        public static bool IsMagickAsseblyLoaded => _imageMagickAssemblyFilename != null;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string TestImageMagick() {
            return "loaded, version: " + MagickNET.Version;
        }

        public static void LoadImageMagickAssembly(string dllFilename) {
            if (IsMagickAsseblyLoaded) return;
            _isMagickSupported = null;

            try {
                _imageMagickAssemblyFilename = dllFilename;
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            } catch (Exception) {
                _imageMagickAssemblyFilename = null;
                throw;
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            return args.Name.Contains("Magick.NET") && _imageMagickAssemblyFilename != null ? Assembly.LoadFrom(_imageMagickAssemblyFilename) : null;
        }

        public static void UnloadImageMagickAssembly() {
            if (!IsMagickAsseblyLoaded) return;

            _isMagickSupported = null;
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        // Temporary solution?
        public static Func<Func<object>, object> SafeMagickWrapper { get; set; }

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static Image LoadFromFileAsImage(string filename, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                return SafeMagickWrapper(() => LoadFromFileAsImage(filename, false)) as Image;
            }
            
            using (var image = new MagickImage(filename)) {
                return image.ToBitmap();
            }
        }

        private const MagickFormat CommonFormat = MagickFormat.Bmp;

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadFromFileAsConventionalBuffer(string filename, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                return SafeMagickWrapper(() => LoadFromFileAsConventionalBuffer(filename, false)) as byte[];
            }
            
            using (var image = new MagickImage(filename)) {
                return image.ToByteArray(CommonFormat);
            }
        }

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadAsConventionalBuffer(byte[] data, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                return SafeMagickWrapper(() => LoadAsConventionalBuffer(data, false)) as byte[];
            }
            
            using (var image = new MagickImage(data)) {
                return image.ToByteArray(CommonFormat);
            }
        }

        [CanBeNull, MethodImpl(MethodImplOptions.Synchronized)]
        public static byte[] LoadAsConventionalBuffer(byte[] data, bool noAlpha, out string formatDescription, bool wrapping = true) {
            if (wrapping && SafeMagickWrapper != null) {
                string description = null;
                var result = SafeMagickWrapper(() => LoadAsConventionalBuffer(data, noAlpha, out description, false)) as byte[];
                formatDescription = description;
                return result;
            }
            
            using (var image = new MagickImage(data)) {
                formatDescription = image.CompressionMethod.ToString();

                if (noAlpha) {
                    image.HasAlpha = false;
                }

                return image.ToByteArray(CommonFormat);
            }
        }

        public static void ApplyPreview(string source, string destination, bool resize) {
            if (resize) {
                ApplyPreview(source, destination, CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight);
            } else {
                ApplyPreview(source, destination);
            }
        }

        public static void ApplyPreviewImageMagick(string source, string destination, double maxWidth = 0d, double maxHeight = 0d) {
            using (var image = new MagickImage(source)) {
                if (maxWidth > 0d || maxHeight > 0d) {
                    var k = Math.Max(maxHeight / image.Height, maxWidth / image.Width);
                    image.Interpolate = PixelInterpolateMethod.Bicubic;
                    image.FilterType = FilterType.Lanczos;
                    image.Sharpen();
                    image.Resize((int)(k * image.Width), (int)(k * image.Height));
                    image.Crop(CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight, Gravity.Center);
                }

                image.Quality = 95;
                image.Density = new MagickGeometry(96, 96);
                if (File.Exists(destination)) {
                    try {
                        File.Delete(destination);
                    } catch (UnauthorizedAccessException) {
                        Thread.Sleep(200);
                        File.Delete(destination);
                    }
                }

                image.Write(destination);
            }
        }

        public static void ApplyPreview(string source, string destination, double maxWidth = 0d, double maxHeight = 0d) {
            if (File.Exists(destination)) {
                File.Delete(destination);
            }

            if (IsMagickAsseblyLoaded) {
                ApplyPreviewImageMagick(source, destination, maxWidth, maxHeight);
            } else {
                var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                var parameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, 100L) } };

                if (maxWidth > 0d || maxHeight > 0d) {
                    using (var bitmap = new Bitmap(CommonAcConsts.PreviewWidth, CommonAcConsts.PreviewHeight))
                    using (var graphics = Graphics.FromImage(bitmap)) {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                        using (var image = Image.FromFile(source)) {
                            var k = Math.Max(maxHeight / image.Height, maxWidth / image.Width);
                            graphics.DrawImage(image, (int)(0.5 * (CommonAcConsts.PreviewWidth - k * image.Width)),
                                               (int)(0.5 * (CommonAcConsts.PreviewHeight - k * image.Height)),
                                               (int)(k * image.Width), (int)(k * image.Height));
                        }

                        bitmap.Save(destination, encoder, parameters);
                    }
                } else {
                    using (var image = Image.FromFile(source)) {
                        image.Save(destination, encoder, parameters);
                    }
                }

                GC.Collect();
            }

            try {
                File.Delete(source);
            } catch (Exception) {
                // ignored
            }
        }

        public static void ApplyPreviews(string acRoot, string carName, string source, bool resize) {
            foreach (var file in Directory.GetFiles(source, "*.bmp")) {
                var skinDirectory = FileUtils.GetCarSkinDirectory(acRoot, carName,
                        Path.GetFileNameWithoutExtension(file));
                if (!Directory.Exists(skinDirectory)) continue;
                ApplyPreview(file, Path.Combine(skinDirectory, "preview.jpg"), resize);
            }
        }

        public static async Task ApplyPreviewsAsync(string acRoot, string carName, string source, bool resize, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var files = Directory.GetFiles(source, "*.bmp");
            for (var i = 0; i < files.Length; i++) {
                var file = files[i];
                var id = Path.GetFileNameWithoutExtension(file);
                var skinDirectory = FileUtils.GetCarSkinDirectory(acRoot, carName, id);
                if (!Directory.Exists(skinDirectory)) continue;

                progress?.Report(new Tuple<string, double?>(id, (double)i / files.Length));
                await Task.Run(() => { ApplyPreview(file, Path.Combine(skinDirectory, "preview.jpg"), resize); }, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }

            try {
                Directory.Delete(source);
            } catch (Exception) {
                // ignored
            }
        }

        public static void ResizeFile(string source, string destination, int width, int height) {
            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap)) {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var image = Image.FromFile(source)) {
                    var k = Math.Min((double)image.Width / width, (double)image.Height / height);
                    var nw = (float)(image.Width / k);
                    var nh = (float)(image.Height / k);
                    graphics.DrawImage(image, (nw - width) / 2f, (nh - height) / 2f, nw, nh);
                }

                bitmap.Save(destination, ImageFormat.Png);
            }
        }

        public static void ConvertImageMagick(string source, string destination, int quality) {
            using (var image = new MagickImage(source)) {
                image.Quality = quality;
                image.Density = new MagickGeometry(96, 96);
                if (File.Exists(destination)) {
                    try {
                        File.Delete(destination);
                    } catch (UnauthorizedAccessException) {
                        Thread.Sleep(200);
                        File.Delete(destination);
                    }
                }

                image.Write(destination);
            }
        }

        public static void Convert(string source, string destination, int quality = 95) {
            if (IsMagickAsseblyLoaded) {
                ConvertImageMagick(source, destination, quality);
            } else {
                var encoder = ImageCodecInfo.GetImageDecoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                var parameters = new EncoderParameters(1) { Param = { [0] = new EncoderParameter(Encoder.Quality, quality > 85 ? 100L : quality) } };
                using (var image = Image.FromFile(source)) {
                    image.Save(destination, encoder, parameters);
                }
            }
        }
    }
}
