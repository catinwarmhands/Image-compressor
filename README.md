﻿# Невероятный сжиматель изображений

![Screenshot](screenshot.png?raw=true)

* Код: (c) CIWH 2018, MIT License
* Тестовые изображения: все права принадлежат создателям


## Фичи

+ Интерфейс
+ Открытие и сохранение обычных и сжатых изображений
+ 3 цветовых пространства RGB, YCbCr, HSV
+ 2 алгоритма: FastHaarTransform, DiscreteCosineTransform
+ Выбор целевого качества в процентах для каждого канала
+ Разбиение изображения на блоки указанного размера
+ Пиксели, не вошедшие в блоки, не теряются
+ Обход каждого блока построчно или зиг-загом
+ Линейный или сквозной мердж блоков перед сжатием
+ Сжатие с помощью gzip
+ Отображение MSE, PSNR


## Алгоритм сжатия

1. Изображение конвертируется в `float[]` с нужной цветовой схемой
2. Разбиение на блоки и остаток
3. К каждому блоку применяется прямое преобразование
4. Малозначящие коэффициенты обнуляются
4. Если надо, каждый блок обходится зиг-загом
5. Блоки укладываются в байтовый плоский массив
6. За ним укладывается остаток
7. Всё это сжимается gzip
8. Перед сжатыми данными записывается заголовок c мета-данными


## Алгоритм разжатия

1. Считывается заголовокс мета-данными
2. Сжатые данные разжимаются gzip
3. Массив байт представляется в виде массива флоатов
4. Восстанавливаются блоки
5. Если надо - зигзаговый обход блоков восстанавливается на линейный
6. К каждому блоку применяется обратное преобразование
7. Блоки восстанавливаются в изображение
8. В изображение дописывается остаток
9. `float[]` с получившимися данными переводится в Image и отображается на экране


## Структура файла `.compressed` версии 1:

* Заголовк (11 байт):
  * `u8 version` версия программы, в которой был сохранен файл
  * `u16 width` ширина в пикселях
  * `u16 height` высота в пикселях
  * `u8 colorScheme` (0 = RGB | 1 = YCbCr | 2 = HSV) - цветовая схема
  * `u8 method` (0 = FHT | 1 = DCT) - вид преобразования
  * `u16 blockSize` (2 | 4 | 8 | 16 | 32 | 64 | 128 | 256 | 512) - размер стороны блока
  * `u8 travelMode` (0 = Линейный | 1 = Зиг-заг) - обход блоков
  * `u8 crossMerge` (0 = false, 1 = true) - линейный или сквозной мердж

* Сжатые данные (остальные байты):
  * `byte[] compressedBytes` байты сжатые gzip

После разжатия `byte[] compressedBytes` его нужно кастануть в `float[]`, он будет иметь размер `width * height * 3`

Общее количество блоков `totalBlocksAmount` можно узнать с помщью `ImageUtils.getTotalBlocksAmount(width, height, blockSize)`

В первых `totalBlocksAmount * blockSize * blockSize * 3` элементах будут лежать уложенные блоки
В оставшихся элементах будет лажать остаток, не влезший в блоки, его размер можно узнать с помощью `ImageUtils.getRemainderSize(width, height, blockSize)`

* Остаток записывается построчно и состоит из 2 секций:
  * a
    * `horizontalBlocksAmount * blockSize <= x < width`
    * `0 <= y < verticalBlocksAmount * blockSize`
  * b
    * `0 <= x < width`
    * `verticalBlocksAmount * blockSize <= y < height`
