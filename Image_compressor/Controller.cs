using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Diagnostics;


namespace Image_compressor
{
    class Constants
    {
        public const  int Version = 1;
        public const  string ProgramName = "Невероятный сжиматель изображений";
        public static string ProgramNameWithVersion { get; } = string.Format("{0} v{1}", ProgramName, Version);

        public const string CompessedExtension = ".compressed";
        public const string ImageFormats = "*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif";
        public const int HeaderSize = 11;
    }

    class Controller
    {
        private View view;
        private Image image;
        private float[] imageValues;

        public Controller(View v)
        {
            view = v;
        }

        // узнать текущее время в миллисекундах
        private long CurrentTime() => (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);

        // загрузить изображение из файла, разжать его при необходимости,
        // и записать результат в image и imageValues
        public void loadImage(string path)
        {
            if (path.EndsWith(Constants.CompessedExtension))
            {
                byte[] bytes = File.ReadAllBytes(path);
                var t1 = CurrentTime();
                float[] restoredImage = ImageUtils.decompressImageBytes(
                    bytes,
                    out int width,
                    out int height,
                    out string colorScheme,
                    out string method,
                    out int blockSize,
                    out string travelMode,
                    out bool crossMerge
                );
                var t2 = CurrentTime();
                var time = (t2 - t1) / 1000f;

                image = ImageUtils.ConvertValuesToImage(restoredImage, colorScheme, width, height);
                
                view.Info = string.Format("Разжато за {0} секунд", time);
                view.ColorScheme = colorScheme;
                view.Method = method;
                view.BlockSize = blockSize;
                view.TravelMode = travelMode;
                view.CrossMerge = crossMerge;
            }
            else
            {
                image = Image.FromFile(path);
            }
            imageValues = null;
            view.Image2 = view.DefaultImage;
            ConvertCurrentImageIntoValues();
            view.Image1 = image;
            view.Title = string.Format("{0}: {1}x{2}  ({3}КБ)", path, image.Width, image.Height, image.Width * image.Height * 3 / 1000f);
        }

        //сжать изображение и сохранить в файл
        public void saveImage(string path)
        {
            if (imageValues == null)
            {
                view.Info = "Сначала загрузите изображение";
                return;
            }
            
            if (path.EndsWith(Constants.CompessedExtension))
            {
                byte[] bytes = compressImageAndShowInfo();
                if (bytes != null)
                    File.WriteAllBytes(path, bytes);
            }
            else
            {
                view.Image2.Save(path);
            }
        }

        // разложить текущий this.image в this.imageValues, учитывая текущую цветовую схему
        public float[] ConvertCurrentImageIntoValues()
        {
            imageValues = ImageUtils.ConvertImageIntoValues(image, view.ColorScheme, imageValues);
            return imageValues;
        }
       
        //сжать изображение, вывести информацию
        public byte[] compressImageAndShowInfo()
        {
            if (imageValues == null)
            {
                view.Info = "Сначала загрузите изобраение";
                return null;
            }

            var t1 = CurrentTime();
            byte[] compressedBytes = ImageUtils.compressImageValues(
                imageValues,
                image.Width,
                image.Height,
                view.ColorScheme,
                view.Method,
                view.Quality,
                view.BlockSize,
                view.TravelMode,
                view.CrossMerge
            );
            var t2 = CurrentTime();
            
            var time = (t2 - t1) / 1000f;

            var rawRize = image.Width * image.Height * 3;
            long compressedSize = compressedBytes.Length;
            
            var compressedPercent = compressedSize * 100 / rawRize;

            int blocksAmount = ImageUtils.getTotalBlocksAmount(view.BlockSize, image.Width, image.Height);

            var remainderSize = ImageUtils.getRemainderSize(image.Width, image.Height, view.BlockSize);

            view.Info = string.Format(
                "Сжато за {0} секунд\n" +
                "Разбиение на {1} блоков\n" + 
                "С остатком {2} пикселей\n" + 
                "Размер после сжатия gzip: {3}КБ ({4}% от оригинала)",
                time,
                blocksAmount,
                remainderSize / 3,
                compressedSize / 1000f,
                compressedPercent
            );

            return compressedBytes;
        }

        //Разжать изображение, дополнить информацию, вывести результат в Image2
        public void decompressImageAndAddInfo(byte[] compressedBytes)
        {
            if (imageValues == null)
                return;

            var t1 = CurrentTime();
            float[] restoredImage = ImageUtils.decompressImageBytes(
                compressedBytes,
                out int width,
                out int height,
                out string colorScheme,
                out string method,
                out int blockSize,
                out string travelMode,
                out bool crossMerge
            );
            var t2 = CurrentTime();

            view.Image2 = ImageUtils.ConvertValuesToImage(restoredImage, colorScheme, width, height);
            ImageUtils.calculateDifference(
                view.Image1,
                view.Image2,
                colorScheme,
                out Tuple<float,float,float> MSE,
                out Tuple<float, float, float> PSNR
            );

            var time = (t2 - t1) / 1000f;
            view.Info += string.Format(
                "\nРазжато за {0} секунд\n" +
                "MSE = {1}\n" +
                "PSNR = {2}\n",
                time,
                MSE,
                PSNR
            );
        }

        // Разбить this.image на блоки, покрасить их в рандомные цвета и вывести в view.Image2
        public void showBlocks()
        {
            if (imageValues == null)
                return;

            //бьем изображение на блоки
            var blockSize = view.BlockSize;
            var blocks = ImageUtils.splitImageValuesIntoBlocks(imageValues, image.Width, image.Height, blockSize);
            
            //каждый блок красим в рандомный цвет
            Random random = new Random();
            Parallel.For(0, blocks.Length, i =>
            {
                float color = (float)random.NextDouble() * 255f;
                for (int j = 0; j < blocks[i].Length; ++j)
                    blocks[i][j] = color;
            });

            //собираем блоки обратно в изображение
            float[] resultValues = ImageUtils.mergeBlocksToImageValues(blocks, image.Width, image.Height, blockSize);

            //если есть остаток - то дописываем его
            var remainderSize = ImageUtils.getRemainderSize(image.Width, image.Height, blockSize);
            if (remainderSize != 0)
            {
                var remainder = ImageUtils.getRemainder(imageValues, image.Width, image.Height, blockSize);
                ImageUtils.writeRemainderIntoImageValues(remainder, resultValues, image.Width, image.Height, blockSize);
            }

            //выводим результат
            var resultImage = ImageUtils.ConvertValuesToImage(resultValues, view.ColorScheme, image.Width, image.Height);
            view.Image2 = resultImage;

            //выводим информацию
            var temp = ImageUtils.getBlocksAmount(blockSize, image.Width, image.Height);
            var blocksAmountHorisontal = temp.Item1;
            var blocksAmountVertical   = temp.Item2;
            var totalBlocksAmount = blocks.Length;

            view.Info = string.Format(
                "Всего {0} блоков\n" +
                "{1} по горизонтали и {2} по вертикали\n" +
                "С остатком в {3} пикселей",
                totalBlocksAmount,
                blocksAmountHorisontal, blocksAmountVertical,
                remainderSize
            );
        }

        // Разбить this.image на блоки, посчитать их спектры и вывести в view.Image2
        public void showSpectre(bool brighter=false)
        {
            if (imageValues == null)
                return;

            var t1 = CurrentTime();

            //бьем изображение на блоки
            var blockSize = view.BlockSize;
            var blocks = ImageUtils.splitImageValuesIntoBlocks(imageValues, image.Width, image.Height, blockSize);

            //применяем прямое преобразоваение к каждому блоку
            switch (view.Method)
            {
                case "FHT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.fht_2d, blocks);
                    break;
                case "DCT":
                    MathUtils.mapFunctionToEachBlock(MathUtils.dct_2d, blocks);
                    break;
            }
            
            //собираем блоки обратно в изображение
            float[] resultValues = ImageUtils.mergeBlocksToImageValues(blocks, image.Width, image.Height, blockSize);

            if (brighter)
            {
                //увеличиваем яркость, что бы на проекторе лучше было видно
                Parallel.For(0, resultValues.Length, i =>
                {
                    resultValues[i] *= 20;
                });
            }
            
            //если есть остаток - то дописываем его
            long remainderSize = ImageUtils.getRemainderSize(image.Width, image.Height, blockSize);
            if (remainderSize != 0)
            {
                float[] remainder = ImageUtils.getRemainder(imageValues, image.Width, image.Height, blockSize);
                ImageUtils.writeRemainderIntoImageValues(remainder, resultValues, image.Width, image.Height, blockSize);
            }

            //выводим результат
            Image resultImage = ImageUtils.ConvertValuesToImage(resultValues, "RGB", image.Width, image.Height);
            var t2 = CurrentTime();
            view.Image2 = resultImage;
            
            //выводим информацию
            var temp = ImageUtils.getBlocksAmount(blockSize, image.Width, image.Height);
            var blocksAmountHorisontal = temp.Item1;
            var blocksAmountVertical = temp.Item2;
            var totalBlocksAmount = blocks.Length;

            view.Info = string.Format(
                "Готово за {0} секунд\n" +
                "Всего {1} блоков\n" +
                "{2} по горизонтали и {3} по вертикали\n" +
                "С остатком в {4} пикселей",
                (t2 - t1) / 1000f,
                totalBlocksAmount,
                blocksAmountHorisontal, blocksAmountVertical,
                remainderSize
            );
        }
    }
}
