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
    public partial class View : Form
    {
        private Controller controller;

        //трекбар умеет хранить только int, а нам хотелось бы указывать дробные числа. 
        //Придется хранить "настоящее" качество отдельно
        private float[] quality = new float[3];

        public View()
        {
            InitializeComponent();
            controller = new Controller(this);

            ColorScheme = "RGB";
            Method = "FHT";
            Quality = Tuple.Create(100f, 100f, 100f);
            LockQuality = true;
            BlockSize = 8;
            TravelMode = "Линейный";
            Title = Constants.ProgramNameWithVersion;
        }

        //Геттеры и сеттеры для настроек

        public string Title
        {
            get => this.Text;
            set => this.Text = value;
        }

        public string Info
        {
            get => info_textBox.Text;
            set => info_textBox.Text = value.Replace("\n", Environment.NewLine);
        }

        public string ColorScheme
        {
            get => (string)color_scheme_comboBox.SelectedItem;
            set
            {
                if (!color_scheme_comboBox.Items.Contains(value))
                    throw new ArgumentException();
                color_scheme_comboBox.SelectedItem = value;
            }
        }

        public string Method
        {
            get => (string)method_comboBox.SelectedItem;
            set
            {
                if (!method_comboBox.Items.Contains(value))
                    throw new ArgumentException();
                method_comboBox.SelectedItem = value;
            }
        }

        public Tuple<float,float,float> Quality
        {
            get => Tuple.Create(quality[0], quality[1], quality[2]);
            set
            {
                quality[0] = MathUtils.Clamp(value.Item1, 0, 100);
                quality[1] = MathUtils.Clamp(value.Item2, 0, 100);
                quality[2] = MathUtils.Clamp(value.Item3, 0, 100);
            }
        }

        public bool LockQuality
        {
            get => lock_quality_checkBox.Checked;
            set => lock_quality_checkBox.Checked = value;
        }

        public int BlockSize
        {
            get
            {
                string s = (string)block_size_comboBox.SelectedItem;
                return int.Parse(s.Substring(0, s.IndexOf('x')));
            }
            set
            {
                string s = string.Format("{0}x{0}", value);
                if (!block_size_comboBox.Items.Contains(s))
                    throw new ArgumentException();
                block_size_comboBox.SelectedItem = s;
            }
        }

        public string TravelMode
        {
            get => (string)travel_mode_comboBox.SelectedItem;
            set
            {
                if (!travel_mode_comboBox.Items.Contains(value))
                    throw new ArgumentException();
                travel_mode_comboBox.SelectedItem = value;
            }
        }

        public bool CrossMerge
        {
            get => block_merge_checkBox.Checked;
            set => block_merge_checkBox.Checked = value;
        }

        public Image Image1
        {
            get => pictureBox1.Image;
            set
            {
                pictureBox1.Image = value;
                updateWindowSizeToMatchImagesSize();
            }
        }

        public Image Image2
        {
            get => pictureBox2.Image;
            set
            {
                pictureBox2.Image = value;
                updateWindowSizeToMatchImagesSize();
            }
        }

        public Image DefaultImage
        {
            get => pictureBox1.InitialImage;
        }

        //всякое

        private void updateWindowSizeToMatchImagesSize()
        {
            var image1 = pictureBox1.Image;
            var image2 = pictureBox2.Image;
            var w = Math.Max(image1.Size.Width, image2.Size.Width) * 2;
            var h = Math.Max(image1.Size.Height, image2.Size.Height) + control_panel.Size.Height;
            this.ClientSize = new Size(w, h);
        }

        //Коллбеки на изменениие элементов

        private void color_scheme_comboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            controller.ConvertCurrentImageIntoValues();
            switch (ColorScheme)
            {
                case "RGB":
                    quality_label1.Text = "R";
                    quality_label2.Text = "G";
                    quality_label3.Text = "B";
                    break;
                case "YCbCr":
                    quality_label1.Text = "Y";
                    quality_label2.Text = "Cb";
                    quality_label3.Text = "Cr";
                    break;
                case "HSV":
                    quality_label1.Text = "H";
                    quality_label2.Text = "S";
                    quality_label3.Text = "V";
                    break;
            }
        }

        private void quality_trackbar_Scroll(object sender, EventArgs e)
        {
            TrackBar t = (TrackBar)sender;

            if (LockQuality)
            {
                quality[0] = quality[1] = quality[2] = t.Value;
                quality_textbox1.Text = quality_textbox2.Text = quality_textbox3.Text = t.Value.ToString();
                quality_trackBar1.Value = quality_trackBar2.Value = quality_trackBar3.Value = t.Value;
            }
            else
            {
                switch (t.Name[t.Name.Length - 1])
                {
                    case '1': quality_textbox1.Text = t.Value.ToString(); break;
                    case '2': quality_textbox2.Text = t.Value.ToString(); break;
                    case '3': quality_textbox3.Text = t.Value.ToString(); break;
                }
            }
        }

        private void quality_textbox_TextChanged(object sender, EventArgs e)
        {
            
            TextBox t = (TextBox)sender;

            int val = 0;
            float fval = 0;
            try
            {
                fval = MathUtils.Clamp(float.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture), 0, 100);
            }
            catch { }
            val = (int)fval;

            if (LockQuality)
            {
                quality[0] = quality[1] = quality[2] = fval;
                quality_textbox1.Text = quality_textbox2.Text = quality_textbox3.Text = t.Text;
                quality_trackBar1.Value = quality_trackBar2.Value = quality_trackBar3.Value = val;
            }
            else
            {
                switch (t.Name[t.Name.Length - 1])
                {
                    case '1': quality_trackBar1.Value = val; quality[0] = fval; break;
                    case '2': quality_trackBar2.Value = val; quality[1] = fval; break;
                    case '3': quality_trackBar3.Value = val; quality[2] = fval; break;
                }
            }
        }
        
        private void acceptDigitsOnly(object sender, KeyPressEventArgs e)
        {
            //в этот текстбокс можно вводить только цифры
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
                e.Handled = true;
        }
        
        //Коллбеки на нажатие кнопок

        private void load_button_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = string.Format("Изображения ({0})|{0}|Сжатые изображения (*{1})|*{1}|Все файлы (*.*)|*.*", Constants.ImageFormats, Constants.CompessedExtension);
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                    controller.loadImage(dialog.FileName);
            }
        }

        private void save_button_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = string.Format("Изображение (*.jpg)|*.jpg;|Изображение (*.png)|*.png;|Сжатый файл (*{0})|*{0}", Constants.CompessedExtension);
                dialog.RestoreDirectory = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                    controller.saveImage(dialog.FileName);
            }
        }

        private void compress_button_Click(object sender, EventArgs e)
        {
            var bytes = controller.compressImageAndShowInfo();
            controller.decompressImageAndAddInfo(bytes);
        }

        private void show_spectre_button_Click(object sender, EventArgs e)
        {
            //если нажать на кнопку с зажатым ctrl, то спектры будут ярче
            controller.showSpectre(Form.ModifierKeys == Keys.Control);
        }

        private void show_blocks_button_Click(object sender, EventArgs e)
        {
            controller.showBlocks();
            // BUG: если слишком быстро жать на кнопку при BlockSize=2, то результат может содержать "дыры"
        }
    }
}
