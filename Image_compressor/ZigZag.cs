using System;
using System.Collections.Generic;

namespace Image_compressor
{
    //Класс для работы с зигзаговым обходом квадратных двумерных массивов
    class ZigZag
    {
        //поскольку getZigzagIndexes возвращает массив, будем кешировать результаты
        private static Dictionary<int, int[]> precomputed = new Dictionary<int, int[]>();

        //пред-вычислить индексы для данного размера
        public static void precompute(int size)
        {
            getIndexes(size);
        }

        //Для данного размера вернет массив индексов,
        //по которым нужно пройти, что бы получился зигзаг
        public static int[] getIndexes(int n)
        {
            if (precomputed.ContainsKey(n))
                return precomputed[n];

            int[] indexes = new int[n * n];
            int k = 0;
            for (int i = 0; i < n * 2; i++)
                for (int j = (i < n) ? 0 : i - n + 1; j <= i && j < n; j++)
                    indexes[k++] = (i % 2 == 1) ? j * (n - 1) + i : (i - j) * n + j;

            precomputed[n] = indexes;
            return indexes;
        }   
    }
}
