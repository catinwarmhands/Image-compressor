using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Image_compressor
{
    static class MathUtils
    {
        //ограничить значение сверху и снизу
        public static T Clamp<T>(this T val, T min, T max) where T : IComparable<T>
        {
            if (val.CompareTo(min) < 0) return min;
            else if (val.CompareTo(max) > 0) return max;
            else return val;
        }

        //квадратный корень
        public static double Sqr(double x) => (x * x);

        //перенести число, находящееся в оном интервале, в другой интервал
        public static float MapInterval(float val , float A, float B, float a, float b)
        {
            return (val - A) * (b - a) / (B - A) + a;
        }

        //округлить до n знаков после запятой
        public static float Round(float x, int n)
        {
            try
            {
                return (float)Math.Round(Convert.ToDecimal(x), n);
            }
            catch
            {
                return x;
            }
        }

        //один шаг одномерного преобразования Хаара
        public static void fht_1d(float[] vec, int size)
        {
            float[] temp = new float[size];
            int n = size / 2;
            for (int i = 0; i < n; ++i)
            {
                float a = vec[2 * i];
                float b = vec[2 * i + 1];
                float summ = a + b;
                float diff = a - b;
                temp[i] = summ / 2;
                temp[n + i] = diff / 2;
            }
            for (int i = 0; i < size; ++i)
            {
                vec[i] = temp[i];
            }
        }

        //один шаг одномерного обратного преобразования Хаара
        public static void inv_fht_1d(float[] vec, int size)
        {
            float[] temp = new float[size];
            int n = size / 2;
            for (int i = 0; i < n; ++i)
            {
                float a = vec[i];
                float b = vec[i + n];
                float summ = a + b;
                float diff = a - b;
                temp[2*i] = summ;
                temp[2*i+1] = diff;
            }
            for (int i = 0; i < size; ++i)
            {
                vec[i] = temp[i];
            }
        }

        //двумерное преобразование Хаара
        public static void fht_2d(float[] image, int width, int height, float[] temp=null)
        {
            if (temp == null)
                temp = new float[Math.Max(width, height)];
            int w = width;
            int h = height;
            while (w >= 2 && h >= 2)
            {
                //применяем один шаг к каждой строке и столбцу
                mapFunctionToEachRowAndColumn(fht_1d, w, h, image, width, height, temp);

                //и повторяем это для области LL
                w /= 2;
                h /= 2;
            }
        }

        //обратное двумерное преобразование Хаара
        public static void inv_fht_2d(float[] image, int width, int height, float[] temp=null)
        {
            if (temp == null)
                temp = new float[Math.Max(width, height)];
            
            //узнаем, на каком размере закончился прямой алгоритм
            int w = width;
            int h = height;
            while (w >= 2 && h >= 2)
            {
                w /= 2;
                h /= 2;
            }
            w *= 2;
            h *= 2;
            while (w <= width && h <= height)
            {
                //применяем ко всем строкам и столбцам области LL обратное преобразование
                mapFunctionToEachRowAndColumn(inv_fht_1d, w, h, image, width, height, temp);

                //и увеличиваем область
                w *= 2;
                h *= 2;
            }
        }

        //одномерное косинусное преобразование
        public static void dct_1d(float[] vec, int size)
        {
            dct_unscaled_1d(vec, size, 0, new float[size]);

            vec[0] /= (float)Math.Sqrt(2.0);
            for (int i = 0; i < size; i++)
                vec[i] *= (float)Math.Sqrt(2.0 / size);
        }

        //ненормированное одномерное косинусное преобразование
        private static void dct_unscaled_1d(float[] vec, int size, int offset, float[] temp)
        {
            // Algorithm by Byeong Gi Lee, 1984. For details, see:
            // See: http://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.118.3056&rep=rep1&type=pdf#page=34
            
            if (size == 1)
                return;
            int halfLen = size / 2;
            for (int i = 0; i < halfLen; i++)
            {
                float x = vec[offset + i];
                float y = vec[offset + size - 1 - i];
                temp[offset + i] = x + y;
                temp[offset + i + halfLen] = (float)((x - y) / (Math.Cos((i + 0.5) * Math.PI / size) * 2));
            }
            dct_unscaled_1d(temp, halfLen, offset, vec);
            dct_unscaled_1d(temp, halfLen, offset + halfLen, vec);
            for (int i = 0; i < halfLen - 1; i++)
            {
                vec[offset + i * 2 + 0] = temp[offset + i];
                vec[offset + i * 2 + 1] = temp[offset + i + halfLen] + temp[offset + i + halfLen + 1];
            }
            vec[offset + size - 2] = temp[offset + halfLen - 1];
            vec[offset + size - 1] = temp[offset + size - 1];
        }

        //обратное одномерное косинусное преобразование
        public static void inv_dct_1d(float[] vec, int size)
        {
            vec[0] /= (float)Math.Sqrt(2.0);
            inv_dct_unscaled_1d(vec, size, 0, new float[size]);
            for (int i = 0; i < size; i++)
                vec[i] *= (float)Math.Sqrt(2.0 / size);
        }

        //обратное ненормированное одномерное косинусное преобразование
        private static void inv_dct_unscaled_1d(float[] vec, int size, int offset, float[] temp)
        {
            // Algorithm by Byeong Gi Lee, 1984. For details, see:
            // https://www.nayuki.io/res/fast-discrete-cosine-transform-algorithms/lee-new-algo-discrete-cosine-transform.pdf
            if (size == 1)
                return;
            int halfLen = size / 2;
            temp[offset + 0] = vec[offset + 0];
            temp[offset + halfLen] = vec[offset + 1];
            for (int i = 1; i < halfLen; i++)
            {
                temp[offset + i] = vec[offset + i * 2];
                temp[offset + i + halfLen] = vec[offset + i * 2 - 1] + vec[offset + i * 2 + 1];
            }
            inv_dct_unscaled_1d(temp, halfLen, offset, vec);
            inv_dct_unscaled_1d(temp, halfLen, offset + halfLen, vec);
            for (int i = 0; i < halfLen; i++)
            {
                float x = temp[offset + i];
                float y = (float)(temp[offset + i + halfLen] / (Math.Cos((i + 0.5) * Math.PI / size) * 2));
                vec[offset + i] = x + y;
                vec[offset + size - 1 - i] = x - y;
            }
        }

        //двумерное косинусное преобразование
        public static void dct_2d(float[] image, int width, int height, float[] temp=null)
        {
            if (temp == null)
                temp = new float[Math.Max(width, height)];
            //применяем к каждой строке и столбцу
            mapFunctionToEachRowAndColumn(dct_1d, width, height, image, width, height, temp);
        }

        //обратное двумерное косинусное преобразование
        public static void inv_dct_2d(float[] image, int width, int height, float[] temp = null)
        {
            if (temp == null)
                temp = new float[Math.Max(width, height)];
            //применяем к каждой строке и столбцу
            mapFunctionToEachRowAndColumn(inv_dct_1d, width, height, image, width, height);
        }
        
        //применить указанную одномерную функцию к каждой строке размера w и столбцу рразмера h
        private static void mapFunctionToEachRowAndColumn(Action<float[], int> func, int w, int h, float[] image, int width, int height, float[] temp=null)
        {
            if (temp == null)
                temp = new float[Math.Max(w, h)];

            for (int channel = 0; channel < 3; ++channel)
            {
                // для всех строк
                for (int i = 0; i < h; ++i)
                {
                    for (int j = 0; j < w; ++j)
                        temp[j] = image[(i * width + j) * 3 + channel];
                    func(temp, w);
                    for (int j = 0; j < w; ++j)
                        image[(i * width + j) * 3 + channel] = temp[j];
                }
                //для всех столбцов
                for (int j = 0; j < w; ++j)
                {
                    for (int i = 0; i < h; ++i)
                        temp[i] = image[(i * width + j) * 3 + channel];
                    func(temp, h);
                    for (int i = 0; i < h; ++i)
                        image[(i * width + j) * 3 + channel] = temp[i];
                }
            }
        }
        
        //применить двумерную функцию к каждому блоку
        public static void mapFunctionToEachBlock(Action<float[], int, int, float[]> func, float[][] blocks)
        {
            Parallel.For(0, blocks.Length, i =>
            {
                int block_size = (int)Math.Sqrt(blocks[i].Length / 3);
                func(blocks[i], block_size, block_size, null);
            });
        }
    }
}
