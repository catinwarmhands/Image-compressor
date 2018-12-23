using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace Image_compressor
{
    static class ImageUtils
    {
        //здесь и дальше - функции для перевода одного пиксела в другую цветовую схему
        public static void RGB_to_YCbCr(float r, float g, float b, out float y, out float cb, out float cr)
        {
            y = (float)(0+(0.229*r)+(0.587*g)+(0.114*b));
            cb = (float)(128-(0.168736*r)-(0.331264*g)+(0.5)*b);
            cr = (float)(128+(0.5*r)-(0.418688*g)-(0.081312*b));
        }

        public static void RGB_to_HSV(float r, float g, float b, out float h, out float s, out float v)
        {
            r /= 255.0f;
            g /= 255.0f;
            b /= 255.0f;

            // h:0-360.0, s:0.0-1.0, v:0.0-1.0

            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));

            v = max;

            if (max == 0.0f)
            {
                s = 0;
                h = 0;
            }
            else if (max - min == 0.0f)
            {
                s = 0;
                h = 0;
            }
            else
            {
                s = (max - min) / max;

                if (max == r)
                {
                    h = 60 * ((g - b) / (max - min)) + 0;
                }
                else if (max == g)
                {
                    h = 60 * ((b - r) / (max - min)) + 120;
                }
                else
                {
                    h = 60 * ((r - g) / (max - min)) + 240;
                }
            }

            if (h < 0) h += 360.0f;

            h /= 2;   // dst_h : 0-180
            s *= 255; // dst_s : 0-255
            v *= 255; // dst_v : 0-255
        }

        public static void YCbCr_to_RGB(float y, float cb, float cr, out float r, out float g, out float b)
        {
            r = (float)(y+1.402*(cr-128));
            g = (float)(y-0.34414*(cb-128)-0.71414*(cr-128));
            b = (float)(y+1.772*(cb-128));
        }

        public static void YCbCr_to_HSV(float y, float cb, float cr, out float h, out float s, out float v)
        {
            float r, g, b;
            YCbCr_to_RGB(y, cb, cr, out r, out g, out b);
            RGB_to_HSV(r, g, b, out h, out s, out v);
        }

        public static void HSV_to_YCbCr(float h, float s, float v, out float y, out float cb, out float cr)
        {
            float r, g, b;
            HSV_to_RGB(h, s, v, out r, out g, out b);
            RGB_to_YCbCr(r, g, b, out y, out cb, out cr);
        }

        public static void HSV_to_RGB(float h, float s, float v, out float r, out float g, out float b)
        {
            h *= 2.0f; // 0-360
            s /= 255.0f; // 0.0-1.0
            v /= 255.0f; // 0.0-1.0
            r = g = b = 0;
            
            int hi = (int)(h / 60.0f) % 6;
            float f = (h / 60.0f) - hi;
            float p = v * (1.0f - s);
            float q = v * (1.0f - s * f);
            float t = v * (1.0f - s * (1.0f - f));

            switch (hi)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                case 5: r = v; g = p; b = q; break;
            }

            r *= 255; // dst_r : 0-255
            g *= 255; // dst_r : 0-255
            b *= 255; // dst_r : 0-255
        }

        //перевести Image в float[], в котором будут лежать RGB данные
        public static float[] Image_to_RGB_values(Image image, float[] buffer = null)
        {
            var bmp = (Bitmap)image;
            int width = image.Width;
            int height = image.Height;
            int channels_amount = Image.GetPixelFormatSize(image.PixelFormat) / 8;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] bgrValues = new byte[bytes];
            int padding = bmpData.Stride - channels_amount * width;
            int paddingSize = default(int);
            if (padding != 0)
                paddingSize = (bgrValues.Length - channels_amount * width * height) / padding / height;
            Marshal.Copy(ptr, bgrValues, 0, bytes);
            bmp.UnlockBits(bmpData);
            float[] values = buffer == null ? new float[image.Width * image.Height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                int i = k / width;
                var b = bgrValues[channels_amount * k + 0 + paddingSize * padding * i];
                var g = bgrValues[channels_amount * k + 1 + paddingSize * padding * i];
                var r = bgrValues[channels_amount * k + 2 + paddingSize * padding * i];

                values[k * 3 + 0] = r;
                values[k * 3 + 1] = g;
                values[k * 3 + 2] = b;
            });
            return values;
        }

        //перевести Image в float[], в котором будут лежать YCbCr данные
        public static float[] Image_to_YCbCr_values(Image image, float[] buffer = null)
        {
            var bmp = (Bitmap)image;
            int width = image.Width;
            int height = image.Height;
            int channels_amount = Image.GetPixelFormatSize(image.PixelFormat) / 8;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] bgrValues = new byte[bytes];
            int padding = bmpData.Stride - channels_amount * width;
            int paddingSize = default(int);
            if (padding != 0)
                paddingSize = (bgrValues.Length - channels_amount * width * height) / padding / height;
            Marshal.Copy(ptr, bgrValues, 0, bytes);
            bmp.UnlockBits(bmpData);
            float[] values = buffer == null ? new float[image.Width * image.Height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                int i = k / width;
                var b = bgrValues[channels_amount * k + 0 + paddingSize * padding * i];
                var g = bgrValues[channels_amount * k + 1 + paddingSize * padding * i];
                var r = bgrValues[channels_amount * k + 2 + paddingSize * padding * i];

                float y, cb, cr;
                RGB_to_YCbCr(r, g, b, out y, out cb, out cr);

                values[k * 3 + 0] = y;
                values[k * 3 + 1] = cb;
                values[k * 3 + 2] = cr;
            });
            return values;
        }

        //перевести Image в float[], в котором будут лежать HSV данные
        public static float[] Image_to_HSV_values(Image image, float[] buffer = null)
        {
            var bmp = (Bitmap)image;
            int width = image.Width;
            int height = image.Height;
            int channels_amount = Image.GetPixelFormatSize(image.PixelFormat) / 8;
            Rectangle rect = new Rectangle(0, 0, width, height);
            BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            int bytes = Math.Abs(bmpData.Stride) * height;
            byte[] bgrValues = new byte[bytes];
            int padding = bmpData.Stride - channels_amount * width;
            int paddingSize = default(int);
            if (padding != 0)
                paddingSize = (bgrValues.Length - channels_amount * width * height) / padding / height;
            Marshal.Copy(ptr, bgrValues, 0, bytes);
            bmp.UnlockBits(bmpData);
            float[] values = buffer == null ? new float[image.Width * image.Height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                int i = k / width;
                var b = bgrValues[channels_amount * k + 0 + paddingSize * padding * i];
                var g = bgrValues[channels_amount * k + 1 + paddingSize * padding * i];
                var r = bgrValues[channels_amount * k + 2 + paddingSize * padding * i];

                float h, s, v;
                RGB_to_HSV(r, g, b, out h, out s, out v);

                values[k * 3 + 0] = h;
                values[k * 3 + 1] = s;
                values[k * 3 + 2] = v;
            });
            return values;
        }

        //здесь и дальше - куча функций для перевода float[] в float[], но с разными цветовыми схемами
        //эти функции полезны, но в программе не используются
        public static float[] RGB_values_to_YCbCr_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float r = input[k * 3 + 0];
                float g = input[k * 3 + 1];
                float b = input[k * 3 + 2];

                float y, cb, cr;
                RGB_to_YCbCr(r, g, b, out y, out cb, out cr);

                values[k * 3 + 0] = y;
                values[k * 3 + 1] = cb;
                values[k * 3 + 2] = cr;
            });
            return values;
        }

        public static float[] RGB_values_to_HSV_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float r = input[k * 3 + 0];
                float g = input[k * 3 + 1];
                float b = input[k * 3 + 2];

                float h, s, v;
                RGB_to_HSV(r, g, b, out h, out s, out v);

                values[k * 3 + 0] = h;
                values[k * 3 + 1] = s;
                values[k * 3 + 2] = v;
            });
            return values;
        }

        public static float[] YCbCr_values_to_RGB_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float y = input[k * 3 + 0];
                float cb = input[k * 3 + 1];
                float cr = input[k * 3 + 2];

                float r, g, b;
                YCbCr_to_RGB(y, cb, cr, out r, out g, out b);

                values[k * 3 + 0] = r;
                values[k * 3 + 1] = g;
                values[k * 3 + 2] = b;
            });
            return values;
        }

        public static float[] YCbCr_values_to_HSV_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float y = input[k * 3 + 0];
                float cb = input[k * 3 + 1];
                float cr = input[k * 3 + 2];

                float h, s, v;
                YCbCr_to_RGB(y, cb, cr, out h, out s, out v);

                values[k * 3 + 0] = h;
                values[k * 3 + 1] = s;
                values[k * 3 + 2] = v;
            });
            return values;
        }

        public static float[] HSV_values_to_YCbCr_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float h = input[k * 3 + 0];
                float s = input[k * 3 + 1];
                float v = input[k * 3 + 2];

                float y, cb, cr;
                HSV_to_YCbCr(h, s, v, out y, out cb, out cr);

                values[k * 3 + 0] = y;
                values[k * 3 + 1] = cb;
                values[k * 3 + 2] = cr;
            });
            return values;
        }

        public static float[] HSV_values_to_RGB_values(float[] input, int width, int height, float[] buffer = null)
        {
            float[] values = buffer == null ? new float[width * height * 3] : buffer;
            Parallel.For(0, width * height, k =>
            {
                float h = input[k * 3 + 0];
                float s = input[k * 3 + 1];
                float v = input[k * 3 + 2];

                float r, g, b;
                HSV_to_RGB(h, s, v, out r, out g, out b);

                values[k * 3 + 0] = r;
                values[k * 3 + 1] = g;
                values[k * 3 + 2] = b;
            });
            return values;
        }

        //собрать Image из float[], в котором лежат RGB данные
        public static Image RGB_values_to_image(float[] values, int width, int height)
        {
            byte[] bytes = new byte[width * height * 3];
            Parallel.For(0, width * height, i =>
            {
                float r = values[i * 3 + 0];
                float g = values[i * 3 + 1];
                float b = values[i * 3 + 2];
                bytes[i * 3 + 0] = (byte)MathUtils.Clamp(b, 0, 255);
                bytes[i * 3 + 1] = (byte)MathUtils.Clamp(g, 0, 255);
                bytes[i * 3 + 2] = (byte)MathUtils.Clamp(r, 0, 255);
            });
            var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            // Row-by-row copy
            var arrRowLength = width * Image.GetPixelFormatSize(output.PixelFormat) / 8;
            var ptr = bmpData.Scan0;
            for (var i = 0; i < height; i++)
            {
                Marshal.Copy(bytes, i * arrRowLength, ptr, arrRowLength);
                ptr += bmpData.Stride;
            }
            output.UnlockBits(bmpData);
            return output;
        }

        //собрать Image из float[], в котором лежат YCbCr данные
        public static Image YCbCr_values_to_image(float[] values, int width, int height)
        {
            byte[] bytes = new byte[width * height * 3];
            Parallel.For(0, width * height, i =>
            {
                float y = values[i * 3 + 0];
                float cb = values[i * 3 + 1];
                float cr = values[i * 3 + 2];
                float r, g, b;
                YCbCr_to_RGB(y, cb, cr, out r, out g, out b);
                bytes[i * 3 + 0] = (byte)MathUtils.Clamp(b, 0, 255);
                bytes[i * 3 + 1] = (byte)MathUtils.Clamp(g, 0, 255);
                bytes[i * 3 + 2] = (byte)MathUtils.Clamp(r, 0, 255);
            });
            var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            // Row-by-row copy
            var arrRowLength = width * Image.GetPixelFormatSize(output.PixelFormat) / 8;
            var ptr = bmpData.Scan0;
            for (var i = 0; i < height; i++)
            {
                Marshal.Copy(bytes, i * arrRowLength, ptr, arrRowLength);
                ptr += bmpData.Stride;
            }
            output.UnlockBits(bmpData);
            return output;
        }

        //собрать Image из float[], в котором лежат HSV данные
        public static Image HSV_values_to_image(float[] values, int width, int height)
        {
            byte[] bytes = new byte[width * height * 3];
            Parallel.For(0, width * height, i =>
            {
                float h = values[i * 3 + 0];
                float s = values[i * 3 + 1];
                float v = values[i * 3 + 2];
                float r, g, b;
                HSV_to_RGB(h, s, v, out r, out g, out b);
                bytes[i * 3 + 0] = (byte)MathUtils.Clamp(b, 0, 255);
                bytes[i * 3 + 1] = (byte)MathUtils.Clamp(g, 0, 255);
                bytes[i * 3 + 2] = (byte)MathUtils.Clamp(r, 0, 255);
            });
            var output = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var rect = new Rectangle(0, 0, width, height);
            var bmpData = output.LockBits(rect, ImageLockMode.ReadWrite, output.PixelFormat);

            // Row-by-row copy
            var arrRowLength = width * Image.GetPixelFormatSize(output.PixelFormat) / 8;
            var ptr = bmpData.Scan0;
            for (var i = 0; i < height; i++)
            {
                Marshal.Copy(bytes, i * arrRowLength, ptr, arrRowLength);
                ptr += bmpData.Stride;
            }
            output.UnlockBits(bmpData);
            return output;
        }

        //сконвертировать Image в float[] с указанной цветовой схемой 
        public static float[] ConvertImageIntoValues(Image image, string colorScheme, float[] result=null)
        {
            if (image == null)
                return null;

            switch (colorScheme)
            {
                case "RGB":
                    result = Image_to_RGB_values(image, result);
                    break;
                case "YCbCr":
                    result = Image_to_YCbCr_values(image, result);
                    break;
                case "HSV":
                    result = Image_to_HSV_values(image, result);
                    break;
            }

            return result;
        }

        //сконвертировать float[] в Image с указанной цветовой схемой 
        public static Image ConvertValuesToImage(float[] values, string colorScheme, int width, int height)
        {
            if (values == null)
                return null;

            Image result = null;
            switch (colorScheme)
            {
                case "RGB":
                    result = RGB_values_to_image(values, width, height);
                    break;
                case "YCbCr":
                    result = YCbCr_values_to_image(values, width, height);
                    break;
                case "HSV":
                    result = HSV_values_to_image(values, width, height);
                    break;
            }
            return result;
        }

        // представить массив float как массив byte (без каста, тупо memcopy)
        public static byte[] floatArrayAsByteArray(float[] floatArray)
        {
            var byteArray = new byte[floatArray.Length * sizeof(float)];
            Buffer.BlockCopy(floatArray, 0, byteArray, 0, byteArray.Length);
            return byteArray;
        }

        // представить массив byte как массив float (без каста, тупо memcopy)
        public static float[] byteArrayAsFloatArray(byte[] byteArray)
        {
            var floatArray = new float[byteArray.Length / 4];
            Buffer.BlockCopy(byteArray, 0, floatArray, 0, byteArray.Length);
            return floatArray;
        }
        
        //сжать массив byte начиная с позиции offset с помощью gzip
        public static byte[] compressBytes(byte[] data, int offset=0)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                using (GZipStream gzip = new GZipStream(memory, CompressionMode.Compress, true))
                {
                    gzip.Write(data, offset, data.Length-offset);
                }
                return memory.ToArray();
            }
        }

        //разжать массив byte начиная с позиции offset с помощью gzip
        public static byte[] decompressBytes(byte[] data, int offset=0)
        {
            using (MemoryStream compressedStream = new MemoryStream(data))
            {
                compressedStream.Position += offset;
                using (GZipStream gzip = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        gzip.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
        }

        //сколько блоков по горизонтали и вертикали
        public static Tuple<int, int> getBlocksAmount(int block_size, int width, int height)
        {
            return Tuple.Create(width / block_size, height / block_size);
        }
        
        //сколько всего блоков в изображении
        public static int getTotalBlocksAmount(int block_size, int width, int height)
        {
            return (width / block_size) * (height / block_size);
        }

        //разбить изображение на блоки
        public static float[][] splitImageValuesIntoBlocks(float[] values, int width, int height, int block_size)
        {
            var blocks_amount = getBlocksAmount(block_size, width, height);
            long blocks_amount_w = blocks_amount.Item1;
            long blocks_amount_h = blocks_amount.Item2;
            long total_blocks_amount = blocks_amount_w * blocks_amount_h;
            long block_elements_amount = block_size * block_size * 3;

            float[][] blocks = new float[total_blocks_amount][];
            for (int i = 0; i < total_blocks_amount; i++)
                blocks[i] = new float[block_elements_amount];

            for (long block_i = 0; block_i < blocks_amount_h; ++block_i)
            {
                long block_i_begin = block_i * block_size;
                for (long block_j = 0; block_j < blocks_amount_w; ++block_j)
                {
                    long block_j_begin = block_j * block_size;
                    long current_block = block_i * blocks_amount_w + block_j;
                    for (int i = 0; i < block_size; ++i)
                    {
                        long actual_i = block_i_begin + i;
                        for (int j = 0; j < block_size; ++j)
                        {
                            long actual_j = block_j_begin + j;
                            for (long channel = 0; channel < 3; ++channel)
                            {
                                blocks[current_block][(i * block_size + j) * 3 + channel] = values[(actual_i * width + actual_j) * 3 + channel];
                            }
                        }
                    }
                }
            }
            return blocks;
        }

        //собрать блоки в изображение
        public static float[] mergeBlocksToImageValues(float[][] blocks, int width, int height, int block_size)
        {
            var blocks_amount = getBlocksAmount(block_size, width, height);
            int blocks_amount_w = blocks_amount.Item1;
            int blocks_amount_h = blocks_amount.Item2;
            int total_blocks_amount = blocks_amount_w * blocks_amount_h;
            int block_elements_amount = block_size * block_size * 3;
            
            float[] values = new float[width * height * 3];

            for (long block_i = 0; block_i < blocks_amount_h; ++block_i)
            {
                long block_i_begin = block_i * block_size;
                for (long block_j = 0; block_j < blocks_amount_w; ++block_j)
                {
                    long block_j_begin = block_j * block_size;
                    long current_block = block_i * blocks_amount_w + block_j;
                    for (long i = 0; i < block_size; ++i)
                    {
                        long actual_i = block_i_begin + i;
                        for (long j = 0; j < block_size; ++j)
                        {
                            long actual_j = block_j_begin + j;
                            for (long channel = 0; channel < 3; ++channel)
                            {
                                values[(actual_i * width + actual_j) * 3 + channel] = blocks[current_block][(i * block_size + j) * 3 + channel];
                            }
                        }
                    }
                }
            }
            return values;
        }

        //получить остоток, который не влез в блоки
        public static float[] getRemainder(float[] values, int width, int height, int block_size)
        {
            var blocks_amount = getBlocksAmount(block_size, width, height);
            int blocks_amount_w = blocks_amount.Item1;
            int blocks_amount_h = blocks_amount.Item2;

            int a = (width - blocks_amount_w * block_size) * (blocks_amount_h * block_size);
            int b = (height - blocks_amount_h * block_size) * width;

            if (a + b == 0)
                return null;

            float[] remainder = new float[(a + b) * 3];
            int k = 0;

            for (int i = 0; i < blocks_amount_h * block_size; ++i)
                for (int j = blocks_amount_w * block_size; j < width; ++j)
                    for (int channel = 0; channel < 3; ++channel)
                        remainder[k++] = values[(i * width + j) * 3 + channel];

            for (int i = blocks_amount_h * block_size; i < height; ++i)
                for (int j = 0; j < width; ++j)
                    for (int channel = 0; channel < 3; ++channel)
                        remainder[k++] = values[(i * width + j) * 3 + channel];

            return remainder;
        }

        //узнать размер остатка, который не влез в блоки
        public static long getRemainderSize(int width, int height, int block_size)
        {
            var blocks_amount = getBlocksAmount(block_size, width, height);
            int blocks_amount_w = blocks_amount.Item1;
            int blocks_amount_h = blocks_amount.Item2;

            int a = (width - blocks_amount_w * block_size) * (blocks_amount_h * block_size);
            int b = (height - blocks_amount_h * block_size) * width;

            return (a + b)*3;
        }

        //дописать остаток в изображение
        public static void writeRemainderIntoImageValues(float[] remainder, float[] values, int width, int height, int block_size)
        {
            if (remainder == null)
                return;

            var blocks_amount = getBlocksAmount(block_size, width, height);
            int blocks_amount_w = blocks_amount.Item1;
            int blocks_amount_h = blocks_amount.Item2;

            int a = (width - blocks_amount_w * block_size) * (blocks_amount_h * block_size);
            int b = (height - blocks_amount_h * block_size) * width;

            int k = 0;

            for (int i = 0; i < blocks_amount_h * block_size; ++i)
                for (int j = blocks_amount_w * block_size; j < width; ++j)
                    for (int channel = 0; channel < 3; ++channel)
                        values[(i * width + j) * 3 + channel] = remainder[k++];

            for (int i = blocks_amount_h * block_size; i < height; ++i)
                for (int j = 0; j < width; ++j)
                    for (int channel = 0; channel < 3; ++channel)
                        values[(i * width + j) * 3 + channel] = remainder[k++];
        }
        
        //сквозной мердж блоков в плоский массив
        public static float[] crossMergeBlocks(float[][] blocks, float[] output = null)
        {
            if (output == null)
                output = new float[blocks.Length * blocks[0].Length];

            int k = 0;
            for (int j = 0; j < blocks[0].Length; ++j)
                for (int i = 0; i < blocks.Length; ++i)
                    output[k++] = blocks[i][j];
            
            return output;
        }

        //восстановление блоков после кросс-мерджа
        public static float[][] restoreCrossMergedBlocks(float[] values, int blocks_amount, int elements_in_block, float[][] blocks = null)
        {
            if (blocks == null)
                blocks = new float[blocks_amount][];
            for (int i = 0; i < blocks.Length; ++i)
                if (blocks[i] == null)
                    blocks[i] = new float[elements_in_block];

            int k = 0;
            for (int j = 0; j < blocks[0].Length; ++j)
                for (int i = 0; i < blocks.Length; ++i)
                    blocks[i][j] = values[k++];

            return blocks;
        }
        
        //сжать изображение в формат .compressed (подробнее в Readme.md)
        public static byte[] compressImageValues(
            float[] imageValues,
            int width,
            int height,
            string colorScheme,
            string method,
            Tuple<float,float,float> quality,
            int blockSize,
            string travelMode,
            bool crossMerge
        )
        {
            if (imageValues == null)
                return null;

            //узнаем размер остатка
            long remainderSize = getRemainderSize(width, height, blockSize);

            //бьем изображение на блоки
            var blocks = splitImageValuesIntoBlocks(imageValues, width, height, blockSize);
            int pixelsInBlock = blockSize * blockSize;
            var elementsAmountInOneBlock = pixelsInBlock * 3;
            var elementsAmountInAllBlocks = elementsAmountInOneBlock * blocks.Length;

            //применяем прямое преобразоваение к каждому блоку
            switch (method)
            {
                case "FHT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.fht_2d, blocks);
                    break;
                case "DCT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.dct_2d, blocks);
                    break;
            }

            // Обнуляем малозначимые коэффициенты

            // узнаем, сколько элементов должно быть обнулено
            int[] q = new int[3];
            q[0] = (int)MathUtils.MapInterval(100-quality.Item1, 0, 100, 0, pixelsInBlock);
            q[1] = (int)MathUtils.MapInterval(100-quality.Item2, 0, 100, 0, pixelsInBlock);
            q[2] = (int)MathUtils.MapInterval(100-quality.Item3, 0, 100, 0, pixelsInBlock);
            
            switch (method)
            {
                case "FHT":
                { 
                    Parallel.For(0, blocks.Length, i =>
                    {
                        //В преобразовни Хаара малозначащие коэффициенты - это те,
                        //которые близки к нулю.

                        //Поэтому для каждого канала создадим список пар вида (индекс, значение)
                        //отсортируем по модулю значения
                        //и оставим только индексы
                        //и обнулим необходимое количество элементов по первым q[j] из этих индексов
                        
                        float[][] channels = new float[3][];
                        for (int j = 0; j < 3; ++j)
                        {
                            channels[j] = new float[pixelsInBlock];
                            for (int k = 0; k < pixelsInBlock; ++k)
                                channels[j][k] = blocks[i][k*3+j];
                        }

                        int[][] indexes = new int[3][];
                        for (int j = 0; j < 3; ++j)
                            indexes[j] = channels[j].Select((elem, ind) => new { Index = ind, Value = elem })
                                                    .OrderBy(x => Math.Abs(x.Value))
                                                    .Take(q[j])
                                                    .Select(x => x.Index)
                                                    .ToArray();

                        for (int j = 0; j < 3; ++j)
                            for (int k = 0; k < q[j]; ++k)
                                blocks[i][indexes[j][k]*3 + j] = 0;
                    });
                    break;
                }
                case "DCT":
                {
                    //малозначимые коэффициенты скапливаются в правом нижнем углу,
                    //так что обнуляем начиная оттуда и пока не обнулим столько, сколько надо
                    int[] indexes = ZigZag.getIndexes(blockSize);
                    Parallel.For(0, blocks.Length, i =>
                    {
                        for (int channel = 0; channel < 3; ++channel)
                            for (int j = 0; j < q[channel]; ++j)
                                blocks[i][indexes[pixelsInBlock - 1 - j] * 3 + channel] = 0;
                    });
                    break;
                }
            }

            
            //если нужно - обходим блоки зиг-загом
            if (travelMode == "Зиг-заг")
            {
                ZigZag.precompute(blockSize);
                Parallel.For(0, blocks.Length, i =>
                {
                    blocks[i] = toZigzag(blocks[i]);
                });
            }

            float[] values = new float[elementsAmountInAllBlocks];

            //укладываем блоки
            if (crossMerge) //с кросс-мерджем
            {
                crossMergeBlocks(blocks, values);
            }
            else //без кросс-мерджа
            {
                Parallel.For(0, blocks.Length, block_i =>
                {
                    for (int i = 0; i < elementsAmountInOneBlock; ++i)
                        values[block_i * elementsAmountInOneBlock + i] = blocks[block_i][i];
                });
            }
            
            // если есть остаток
            if (remainderSize != 0)
            {
                //то получаем его
                float[] remainder = getRemainder(imageValues, width, height, blockSize);

                //увеличиваем values, что бы он влез
                Array.Resize(ref values, values.Length + remainder.Length);
                
                //дописываем его в конец
                for (int i = 0; i < remainder.Length; ++i)
                    values[elementsAmountInAllBlocks + i] = remainder[i];
            }

            //сжимаем
            byte[] compressedBytes = compressBytes(floatArrayAsByteArray(values));

            //сюда запишем итоговый .compresed (то есть заголовок и сжатые байты(уложенные блоки + остаток))
            byte[] resultBytes = new byte[Constants.HeaderSize + compressedBytes.Length + remainderSize*4];

            //записываем заголовок
            writeHeader_v1(resultBytes, width, height, colorScheme, method, blockSize, travelMode, crossMerge);

            //дописываем сжатые данные
            for (int i = 0; i < compressedBytes.Length; ++i)
                resultBytes[Constants.HeaderSize + i] = compressedBytes[i];

            return resultBytes;
        }

        //Разжать изображение из формата .compressed (подробнее в Readme.md)
        static public float[] decompressImageBytes(
            byte[] compressedBytes,
            out int width,
            out int height,
            out string colorScheme,
            out string method,
            out int blockSize,
            out string travelMode,
            out bool crossMerge
        ) {
            //считваем заголовок, что бы узнать информацию об изображении
            readHeader_v1(
                compressedBytes,
                out width,
                out height,
                out colorScheme,
                out method,
                out blockSize,
                out travelMode,
                out crossMerge
            );
            
            int blocksAmount = getTotalBlocksAmount(blockSize, width, height);
            int elementsAmountInOneBlock = blockSize * blockSize * 3;
            
            //разжимаем основную часть (уложенне блоки и остаток)
            float[] values = byteArrayAsFloatArray(decompressBytes(compressedBytes, Constants.HeaderSize));

            //восстанавливаем блоки
            float[][] blocks = new float[blocksAmount][];

            if (crossMerge) //с кросс-мерджем
            {
                restoreCrossMergedBlocks(values, blocksAmount, elementsAmountInOneBlock, blocks);
            }
            else //без кросс-мерджа
            {
                Parallel.For(0, blocks.Length, i =>
                {
                    float[] temp = new float[elementsAmountInOneBlock];
                    for (int j = 0; j < elementsAmountInOneBlock; ++j)
                        temp[j] = values[i * elementsAmountInOneBlock + j];
                    blocks[i] = temp;
                });
            }
            
            //если надо - восстанавливаем линейность из зигзага
            if (travelMode == "Зиг-заг")
            {
                ZigZag.precompute(blockSize);
                Parallel.For(0, blocks.Length, i =>
                {
                    blocks[i] = fromZigzag(blocks[i]);
                });
            }

            //к каждому блоку применяем обратное преобразование
            switch (method)
            {
                case "FHT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.inv_fht_2d, blocks);
                    break;
                case "DCT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.inv_dct_2d, blocks);
                    break;
            }

            float[] resultValues = mergeBlocksToImageValues(blocks, width, height, blockSize);

            //записываем остаток, если он был
            long remainderSize = getRemainderSize(width, height, blockSize);
            if (remainderSize != 0)
            {
                float[] remainder = new float[remainderSize];
                for (int i = 0; i < remainderSize; ++i)
                    remainder[i] = values[blocks.Length * elementsAmountInOneBlock + i];

                writeRemainderIntoImageValues(remainder, resultValues, width, height, blockSize);
            }
            
            return resultValues;
        }
        
        //записать в начало массива заголовок
        static void writeHeader_v1(
            byte[] bytes,
            int width,
            int height,
            string colorScheme,
            string method,
            int blockSize,
            string travelMode,
            bool crossMerge
        ) {
            bytes[0]  = 1;
            bytes[1]  = (byte)(width);
            bytes[2]  = (byte)(width >> 8);
            bytes[3]  = (byte)(height);
            bytes[4]  = (byte)(height >> 8);
            bytes[5]  = (byte)((colorScheme == "RGB") ? 0 : (colorScheme == "YCbCr" ? 1 : 2));
            bytes[6]  = (byte)((method == "FHT") ? 0 : 1);
            bytes[7]  = (byte)(blockSize);
            bytes[8]  = (byte)(blockSize >> 8);
            bytes[9]  = (byte)((travelMode == "Линейный") ? 0 : 1);
            bytes[10] = (byte)(crossMerge ? 1 : 0);
        }

        //прочитать заголовок из начала массива
        static void readHeader_v1(
            byte[] bytes,
            out int width,
            out int height,
            out string colorScheme,
            out string method,
            out int blockSize,
            out string travelMode,
            out bool crossMerge
        )
        {
            if (bytes[0] != 1)
                throw new Exception("Cant read header: wrong verson " + bytes[0].ToString());

            colorScheme = method = travelMode = "";

            width  = (bytes[2] << 8) + bytes[1];
            height = (bytes[4] << 8) + bytes[3];
            switch (bytes[5])
            {
                case 0: colorScheme = "RGB"; break;
                case 1: colorScheme = "YCbCr"; break;
                case 2: colorScheme = "HSV"; break;
            }
            switch (bytes[6])
            {
                case 0: method = "FHT"; break;
                case 1: method= "DCT"; break;
            }
            blockSize = (bytes[8] << 8) + bytes[7];
            switch (bytes[9])
            {
                case 0: travelMode = "Линейный"; break;
                case 1: travelMode = "Зиг-заг"; break;
            }
            crossMerge = (bytes[10] == 1);
        }

        //обойти изображение зигзагом, резульат положить в output
        public static T[] toZigzag<T>(T[] values, T[] output=null)
        {
            if (output == null)
                output = new T[values.Length];

            int[] zigzag_indexes = ZigZag.getIndexes((int)Math.Sqrt(values.Length / 3));

            for (int i = 0; i < values.Length / 3; ++i)
            {
                for (int channel = 0; channel < 3; ++channel)
                    output[i * 3 + channel] = values[zigzag_indexes[i] * 3 + channel];
            }

            return output;
        }

        //восстановить линейность изображения, обойденного зигзагом.
        public static T[] fromZigzag<T>(T[] values, T[] output=null)
        {
            if (output == null)
                output = new T[values.Length];

            int[] zigzag_indexes = ZigZag.getIndexes((int)Math.Sqrt(values.Length / 3));

            for (int i = 0; i < values.Length / 3; ++i)
            {
                for (int channel = 0; channel < 3; ++channel)
                    output[zigzag_indexes[i] * 3 + channel] = values[i * 3 + channel];
            }

            return output;
        }

        //посчитать меры отклонения
        //https://ru.wikipedia.org/wiki/Пиковое_отношение_сигнала_к_шуму
        public static void calculateDifference(
            Image image1,
            Image image2,
            string colorScheme, 
            out Tuple<float, float, float> MSE,
            out Tuple<float, float, float> PSNR
        ) {
            
            float[] values1 = ConvertImageIntoValues(image1, colorScheme);
            float[] values2 = ConvertImageIntoValues(image2, colorScheme);

            int w = Math.Min(image1.Width, image2.Width);
            int h = Math.Min(image1.Height, image2.Height);

            
            float[] mse = new float[3];
            float[] psnr = new float[3];
            for (int channel = 0; channel < 3; ++channel)
            {
                for (int i = 0; i < w; ++i)
                    for (int j = 0; j < h; ++j)
                        mse[channel] += (float)MathUtils.Sqr(Math.Abs(values1[(i*image1.Width+j)*3+channel] - values2[(i*image2.Width+j)*3+channel]));
                mse[channel] /= w * h;
                //psnr[channel] = (Math.Abs(mse[channel]) <= 0.00001) ? 0 : (float)(20 * Math.Log10(255 / Math.Sqrt(mse[channel])));
                psnr[channel] = (float)(20 * Math.Log10(255 / Math.Sqrt(mse[channel])));

                mse[channel] = MathUtils.Round(mse[channel], 1);
                psnr[channel] = MathUtils.Round(psnr[channel], 1);
            }
            MSE = Tuple.Create(mse[0], mse[1], mse[2]);
            PSNR = Tuple.Create(psnr[0], psnr[1], psnr[2]);
        }
    }
}
