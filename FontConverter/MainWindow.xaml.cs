using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Unicode;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FontConverter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// 
    /// This program is a bit of a Hack job and does not demonstrate proper MVVM seperation of concerns
    /// </summary>
    public partial class MainWindow : Window
    {
        Font TheFont;
        System.Windows.Media.FontFamily TheFontFamily;
        int TheFontW;
        int TheFontH;
        int offx;
        int offy;
        private IDictionary<int, ushort> characterMap;
        private Dictionary<int, int> MyCharMap;

        public MainWindow()
        {
            InitializeComponent();

            TheFontW = 12;
            TheFontH = 20;

            Points.Text = "14";

            offx = -3;
            xoffset.Text = offx.ToString();

            offy = -3;
            yoffset.Text = offy.ToString();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Select initial font
            ReadOnlyCollection<System.Windows.Media.FontFamily> fonts = (ReadOnlyCollection<System.Windows.Media.FontFamily>)SystemFonts.ItemsSource;
            if (fonts.Contains(new System.Windows.Media.FontFamily("Consolas")))
                SystemFonts.SelectedItem = fonts.Where(ff => ff.Source == "Consolas").FirstOrDefault();
        }

        private void Combobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TheFontFamily = (System.Windows.Media.FontFamily)e.AddedItems[0];
            TweakChanged(sender, null);
        }

        private void ChangeThreshold(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            RegenerateCode();
        }

        private void IsTextAllowed(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !(e.Text.Contains("-") || int.TryParse(e.Text, out _));
        }

        private void TweakChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Points.Text, out int value) && TheFontFamily != null)
                TheFont = new Font(TheFontFamily.Source, value, System.Drawing.FontStyle.Regular);
            if (int.TryParse(xoffset.Text, out int valuex))
                offx = valuex;
            if (int.TryParse(yoffset.Text, out int valuey))
                offy = valuey;

            if (TheFont != null)
                UpdateNewFont();
        }

        private void UpdateNewFont()
        {
            string cr = "";
            foreach (Typeface typeface in TheFontFamily.GetTypefaces())
            {
                // which face are we using ?             
                typeface.TryGetGlyphTypeface(out GlyphTypeface glyph);
                if (glyph != null)
                {
                    characterMap = glyph.CharacterToGlyphMap;
                    cr = glyph.Copyrights.Values.FirstOrDefault();
                }
            }

            MyCharMap = new Dictionary<int, int>();

            GridOChars.Children.Clear();
            GridOChars.RowDefinitions.Clear();
            GridOChars.ColumnDefinitions.Clear();

            //Populate GridoChars
            for (int y = 0; y < 30; y++)
                GridOChars.RowDefinitions.Add(new RowDefinition());
            for (int x = 0; x < 60; x++)
                GridOChars.ColumnDefinitions.Add(new ColumnDefinition());

            // start at space (Char 32 x20)
            int c = 32;
            for (int y = 0; y < 30; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    bool skipping = true;
                    while (skipping && c < 0xFFFF)
                    {
                        if (characterMap.ContainsKey(c))
                        {
                            skipping = false;
                            var i = new System.Windows.Controls.Image() { Stretch = Stretch.None };
                            i.MouseEnter += UpdateFontView;
                            i.Margin = new Thickness(0);
                            i.Source = WriteChar2BM(((char)c).ToString());
                            i.ToolTip = c;

                            string charname = UnicodeInfo.GetName(c);
                            if (!string.IsNullOrEmpty(charname))
                                i.Name = charname.Replace(" ", "_").Replace("-", "_").ToLowerInvariant();

                            GridOChars.Children.Add(i);
                            Grid.SetRow(i, y);
                            Grid.SetColumn(i, x);

                            MyCharMap[c] = x + y * 60;
                        }
                        c++;
                    }
                }
            }

            Label1.Content = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*0123456789";
            Label1.FontFamily = TheFontFamily;
            Label1.FontSize = 16;

            Label2.Content = TheFont.FontFamily.Name; // TheFontFamily.Source;
            Label2.FontFamily = TheFontFamily;
            Label2.FontSize = 16;

            Label3.Content = cr;
            Label3.FontSize = 16;

            RegenerateCode();
        }

        private void RegenerateCode()
        {
            if (MyCharMap == null)
                return;

            // Generate Code the Meadow can use
            string code = Resource1.CodeTemplate;
            code = code.Replace("FONTINFO", (string)Label2.Content + " " + (string)Label3.Content);
            code = code.Replace("MYFONTNAME", (string)Label2.Content + "12x20");
            code = code.Replace("//CHARMAP", CharMaptoC());
            code = code.Replace("//FONTTABLE", AllCharsinFont());
            CodeOutput.Text = code;
        }

        // We need a WritableBitMap, but graphics Drawstring is for Bitmaps - but they can share memory !
        private WriteableBitmap WriteChar2BM(string c)
        {
            var wbm = new WriteableBitmap(TheFontW, TheFontH, 96, 96, PixelFormats.Bgra32, null);
            var bm = new Bitmap(wbm.PixelWidth, wbm.PixelHeight, wbm.BackBufferStride, System.Drawing.Imaging.PixelFormat.Format32bppArgb, wbm.BackBuffer);

            wbm.Lock();
            using (var g = System.Drawing.Graphics.FromImage(bm))
            {
                g.Clear(System.Drawing.Color.Black);
                g.DrawString(c, TheFont, System.Drawing.Brushes.White, offx, offy);
            }
            wbm.AddDirtyRect(new Int32Rect(0, 0, TheFontW, TheFontH));
            wbm.Unlock();

            return wbm;
        }

        private void UpdateFontView(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is System.Windows.Controls.Image)
            {
                string preview = "";

                WriteableBitmap wbm = (WriteableBitmap)((System.Windows.Controls.Image)sender).Source;

                int[] Pixels = new int[TheFontW * TheFontH];
                wbm.CopyPixels(Pixels, wbm.BackBufferStride, 0);

                // lets use the average algorithm to get gray scale (4 bytes to 1)
                for (int x = 0; x < Pixels.Length; x++)
                {
                    // get the greyscale pixel
                    int average = GSPixel(Pixels[x]);

                    // Large Preview 1Bit per pixel
                    if (average > Threshold.Value)
                        preview += "@";
                    else if (average > Threshold.Value / 2)
                        preview += "&";
                    else
                        preview += ".";

                    if (x % wbm.PixelWidth == wbm.PixelWidth - 1)
                        preview += " ";
                }

                preview += Environment.NewLine + Environment.NewLine;

                string c = ((System.Windows.Controls.Image)sender).ToolTip.ToString();
                string charname = UnicodeInfo.GetName((char)int.Parse(c));
                if (!string.IsNullOrEmpty(charname))
                    preview += charname;

                Preview.Text = preview;
            }
        }

        private int GSPixel(int pixel)
        {
            // get the component
            int blue = (pixel & 0x00FF0000) >> 16;
            int green = (pixel & 0x0000FF00) >> 8;
            int red = (pixel & 0x000000FF);
            uint opacity = ((uint)pixel & (uint)0xFF000000) >> 24;

            // get the average
            int average = (byte)((red + blue + green) / 3);
            average = average * (int)(opacity / 255);

            return average;
        }

        #region Generate Code

        //FONTTABLE
        private string AllCharsinFont()
        {
            StringBuilder result = new StringBuilder();
            foreach (var cell in GridOChars.Children)
            {
                if (cell is System.Windows.Controls.Image)
                {
                    WriteableBitmap wbm = (WriteableBitmap)((System.Windows.Controls.Image)cell).Source;
                    int[] Pixels = new int[TheFontW * TheFontH];
                    wbm.CopyPixels(Pixels, wbm.BackBufferStride, 0);

                    string c = ((System.Windows.Controls.Image)cell).ToolTip.ToString();
                    result.AppendLine(GetCharEncoded(Pixels, (char)int.Parse(c)));
                }
            }
            return result.ToString();
        }

        //CHARMAP
        private string CharMaptoC()
        {
            StringBuilder result = new StringBuilder();
            foreach (var k in MyCharMap.Keys)
            {
                result.Append($"{{ 0x{k:X4}, {MyCharMap[k]} }}");
                result.Append(", ");
            }
            result.Remove(result.Length - 2, 2);
            return result.ToString();
        }

        private string GetCharEncoded(int[] bits, char c)
        {
            StringBuilder result = new StringBuilder();

            BitArray ba = new BitArray(TheFontH * TheFontW);

            // lets use the average algorithm to get gray scale (4 bytes to 1)
            for (int x = 0; x < bits.Length; x++)
            {
                // get the greyscale pixel
                int average = GSPixel(bits[x]);

                // encode to 1Bit per pixel
                if (average > Threshold.Value)
                    ba[x] = true;
                else
                    ba[x] = false;
            }

            byte[] fontbyte = new byte[TheFontH * TheFontW / 8];
            ba.CopyTo(fontbyte, 0);

            // encode the font bits into code
            result.Append("            new byte[] { ");

            foreach (var b in fontbyte)
            {
                result.Append($"0x{b:X2}, ");
            }
            result.Remove(result.Length - 2, 2);

            result.Append($"}}, //{(int)c:X4}({c}) {UnicodeInfo.GetName(c)?.ToLowerInvariant()}");

            return result.ToString();
        }

        #endregion


    }
}
