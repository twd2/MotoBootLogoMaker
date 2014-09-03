﻿// ReSharper disable EmptyGeneralCatchClause
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using Moto_Logo.Properties;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Ionic.Zip;


namespace Moto_Logo
{
    public partial class Form1 : Form
    {

        enum ImageOption
        {
            ImageOptionCenter,
            ImageOptionStretchProportionately,
            ImageOptionFill
        };

        enum ImageLayout
        {
            ImageLayoutPortrait,
            ImageLayoutLandscape
        };

        private bool _fileSaved;
        private bool _autoselectlogobinversion = true;
        private int _maxFileSize = 4*1024*1024; //4MiB
        
        private readonly List<String> _loadedbitmapnames = new List<string>(); 
        private readonly List<Bitmap> _loadedbitmaps = new List<Bitmap>();
        private readonly List<ImageOption> _loadedbitmapimageoptions = new List<ImageOption>();
        private readonly List<ImageLayout> _loadedbitmapimagelayout = new List<ImageLayout>(); 

        private readonly List<int> _deviceResolutionX = new List<int>();
        private readonly List<int> _deviceResolutionY = new List<int>();
        private readonly List<int> _deviceLogoBinSize = new List<int>();
        private readonly List<UInt32> _deviceLogoBinContents = new List<UInt32>();

        private Image FixedSizePreview(Image imgPhoto)
        {
            return FixedSize(!rdoAndroid44.Checked ? FixedSize(imgPhoto, 540, 540) : imgPhoto, 
                (int) udResolutionX.Value, (int) udResolutionY.Value,
                !rdoAndroid44.Checked);
        }

        private Bitmap FixedSizeSave(Image imgPhoto)
        {
            var xmax = rdoAndroid44.Checked ? (int)udResolutionX.Value : 540;
            var ymax = rdoAndroid44.Checked ? (int)udResolutionY.Value : 540;
            return (rdoImageCenter.Checked && (imgPhoto.Width <= xmax) 
                    && (imgPhoto.Height <= ymax) && rdoAndroid44.Checked)
                        ? (Bitmap)imgPhoto
                        : FixedSize(imgPhoto,xmax,ymax);
        }

        private Bitmap FixedSize(Image imgPhoto, int imgWidth, int imgHeight, bool forceCenter = false)
        {
            var landscape = (Image)imgPhoto.Clone();
            landscape.RotateFlip(RotateFlipType.Rotate90FlipNone);
            var img = (rdoLayoutLandscape.Checked ? landscape : imgPhoto);

            var sourceWidth = img.Width;
            var sourceHeight = img.Height;
            const int sourceX = 0;
            const int sourceY = 0;
            var destX = 0;
            var destY = 0;

            float nPercent = 0;


// ReSharper disable RedundantCast
            var nPercentW = ((float)imgWidth / (float)sourceWidth);
            var nPercentH = ((float)imgHeight / (float)sourceHeight);
// ReSharper restore RedundantCast

            if (((sourceWidth <= imgWidth) && (sourceHeight <= imgHeight)) && (rdoImageCenter.Checked || forceCenter))
            {
                nPercent = 1.0f;
                destX = (imgWidth - sourceWidth)/2;
                destY = (imgHeight - sourceHeight)/2;
            }
            else if ((nPercentH < nPercentW) && (!rdoImageFill.Checked || forceCenter))
            {
                nPercent = nPercentH;
                destX = Convert.ToInt16((imgWidth -
                              (sourceWidth * nPercent)) / 2);
            }
            else if (!rdoImageFill.Checked || forceCenter)
            {
                nPercent = nPercentW;
                destY = Convert.ToInt16((imgHeight -
                              (sourceHeight * nPercent)) / 2);
            }

            var destWidth = (int)(sourceWidth * ((rdoImageFill.Checked && !forceCenter) ? nPercentW : nPercent));
            var destHeight = (int)(sourceHeight * ((rdoImageFill.Checked && !forceCenter) ? nPercentH : nPercent));

            var bmPhoto = new Bitmap(imgWidth, imgHeight,
                              PixelFormat.Format24bppRgb);
            bmPhoto.SetResolution(img.HorizontalResolution,
                             img.VerticalResolution);

            var grPhoto = Graphics.FromImage(bmPhoto);
            grPhoto.Clear(((Bitmap) img).GetPixel(0, 0));
            grPhoto.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;

            

            grPhoto.DrawImage(img,
                new Rectangle(destX, destY, destWidth, destHeight),
                new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight),
                GraphicsUnit.Pixel);

            grPhoto.Dispose();
            return bmPhoto;
        }


        public Form1()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        // ReSharper disable InconsistentNaming
        [Flags]
        private enum LOGO
        {
            LOGO_RAW = 0,
            LOGO_BOOT = 1,
            LOGO_BATTERY = 2,
            LOGO_UNLOCKED = 4,
            LOGO_LOWPOWER = 8,
            LOGO_UNPLUG = 0x10,
            LOGO_CHARGE = 0x20,
            KITKAT_DISABLED = 0x40000000
        };
        // ReSharper restore InconsistentNaming


        private void init_tree(UInt32 logobincontents)
        {
            if (logobincontents == (int) LOGO.LOGO_RAW)
            {
                init_tree(false, false, true, false, false, false);
                rdoAndroid43.Enabled = false;
                rdoAndroid44.Enabled = false;
                rdoAndroidRAW.Checked = true;
                return;
            }
            var enableKitkat = ((logobincontents & (int)LOGO.KITKAT_DISABLED) == 0);
            rdoAndroid43.Enabled = true;
            rdoAndroid44.Enabled = enableKitkat;
            if (_autoselectlogobinversion && enableKitkat) rdoAndroid44.Checked = true;
            else if (_autoselectlogobinversion && rdoAndroid44.Checked) rdoAndroid43.Checked = true;
            init_tree((logobincontents & (UInt32)LOGO.LOGO_BOOT) == (UInt32)LOGO.LOGO_BOOT,
                (logobincontents & (UInt32)LOGO.LOGO_BATTERY) == (UInt32)LOGO.LOGO_BATTERY,
                (logobincontents & (UInt32)LOGO.LOGO_UNLOCKED) == (UInt32)LOGO.LOGO_UNLOCKED,
                (logobincontents & (UInt32)LOGO.LOGO_LOWPOWER) == (UInt32)LOGO.LOGO_LOWPOWER,
                (logobincontents & (UInt32)LOGO.LOGO_UNPLUG) == (UInt32)LOGO.LOGO_UNPLUG,
                (logobincontents & (UInt32)LOGO.LOGO_CHARGE) == (UInt32)LOGO.LOGO_CHARGE);
        }

        private void init_tree(bool logoboot, bool logobattery, bool logounlocked, bool logolowpower, bool logounplug, bool logocharge)
        {
            var logoBoot = false;
            var logoBattery = false;
            var logoUnlocked = false;
            var logoLowpower = false;
            var logoUnplug = false;
            var logoCharge = false;
            for (var index = tvLogo.Nodes.Count - 1; index >= 0; index--)
            {
                var node = tvLogo.Nodes[index];
                var removenode = false;
                switch (node.Text)
                {
                        
                    case "logo_boot":
                        if (logoboot)
                            logoBoot = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                    case "logo_battery":
                        if(logobattery)
                            logoBattery = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                    case "logo_unlocked":
                        if(logounlocked)
                            logoUnlocked = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                    case "logo_lowpower":
                        if(logolowpower)
                            logoLowpower = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                    case "logo_unplug":
                        if(logounplug)
                            logoUnplug = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                    case "logo_charge":
                        if (logocharge)
                            logoCharge = true;
                        else removenode = (cboMoto.SelectedIndex > 0);
                        break;
                }
                if(removenode)
                    node.Remove();
            }
            if (!logoBoot && logoboot) tvLogo.Nodes.Add("logo_boot");
            if (!logoBattery && logobattery) tvLogo.Nodes.Add("logo_battery");
            if (!logoUnlocked && logounlocked) tvLogo.Nodes.Add("logo_unlocked");
            if (!logoLowpower && logolowpower) tvLogo.Nodes.Add("logo_lowpower");
            if (!logoUnplug && logounplug) tvLogo.Nodes.Add("logo_unplug");
            if (!logoCharge && logocharge) tvLogo.Nodes.Add("logo_charge");
            for (var index = tvLogo.Nodes.Count - 1; index >= 0; index--)
            {
                var node = tvLogo.Nodes[index];
                switch (node.Text)
                {

                    case "logo_boot":
                        node.ToolTipText = "Visible only with boot-loader locked phone.  It is suggested you remove" +
                                           " the picture that is in this entry, to save bytes in your logo.bin";
                        break;
                    case "logo_battery":
                        node.ToolTipText = "Visible when your phone has had its battery fully discharged, and you " +
                                           "plug your phone in to charge";
                        break;
                    case "logo_unlocked":
                        node.ToolTipText =
                            "Visible on boot-loader unlocked phones. What you put here is likely to look" +
                            " much better than the unlocked device warning. :)";
                        break;
                    case "logo_lowpower":
                        node.ToolTipText = "Visible when the phone has more than 3% power while fully powerd off. Not much more is known.\n" +
                            "This feature is only present on the Moto E";
                        break;
                    case "logo_unplug":
                        node.ToolTipText = "Visible when the phone is fully charged while plugged in and fully powered off.\n" +
                            "This feature is only present on the Moto E";
                        break;
                    case "logo_charge":
                        node.ToolTipText =
                            "Visible when your phone is plugged in while fully powered off, and the phone has more" +
                            " than 3% charge.  logo_battery is shown instead if it has 0-3% charge.\n" +
                            "This feature is only available on Moto G that have received the Android 4.4.4 OTA update.";
                        break;
                }
            }

            
        }

        private void udResolutionX_ValueChanged(object sender, EventArgs e)
        {
            tvLogo_AfterSelect(sender, null);
        }

        private void AddToBitmapList(Bitmap img, String filename, String logoname)
        {
            
            var nodeindex = -1;
            for (var i = 0; i < tvLogo.Nodes.Count; i++)
            {
                if (tvLogo.Nodes[i].Text != logoname) continue;
                nodeindex = i;
                break;
            }
            if (nodeindex == -1)
            {
                tvLogo.Nodes.Add(logoname);
                nodeindex = tvLogo.Nodes.Count - 1;
            }
            try
            {
                

                if (_loadedbitmaps.IndexOf(img) != -1) return;
                _loadedbitmaps.Add(img);
                tvLogo.Nodes[nodeindex].Name = _loadedbitmaps.IndexOf(img).ToString();
                _loadedbitmapnames.Add(filename);
                _loadedbitmapimageoptions.Add(rdoImageCenter.Checked
                    ? ImageOption.ImageOptionCenter
                    : rdoImageStretchAspect.Checked
                        ? ImageOption.ImageOptionStretchProportionately
                        : ImageOption.ImageOptionFill);
                _loadedbitmapimagelayout.Add(rdoLayoutLandscape.Checked
                    ? ImageLayout.ImageLayoutLandscape
                    : ImageLayout.ImageLayoutPortrait);
            }
            catch
            {
                tvLogo.Nodes[nodeindex].Name = "";
            }
        }

        private void ClearBitmapList()
        {
            _loadedbitmaps.Clear();
            _loadedbitmapnames.Clear();
            _loadedbitmapimageoptions.Clear();
            _loadedbitmapimagelayout.Clear();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if ((cboMoto.SelectedIndex > 0) && button1.Text == Resources.Append) return;
            if (txtLogoInternalFile.Text == "") return;

            if (button1.Text == Resources.Append)
                switch (txtLogoInternalFile.Text)
                {
                    case "logo_boot":
                        init_tree(true, false, false, false, false,false);
                        break;
                    case "logo_battery":
                        init_tree(false, true, false, false, false, false);
                        break;
                    case "logo_unlocked":
                        init_tree(false, false, true, false, false, false);
                        break;
                    case "logo_lowpower":
                        init_tree(false, false, false, true, false, false);
                        break;
                    case "logo_unplug":
                        init_tree(false, false, false, false, true, false);
                        break;
                    case "logo_charge":
                        init_tree(false, false, false, false, false, true);
                        break;
                }

            openFileDialog1.Filter = Resources.SelectImageFile;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                var img = new Bitmap(new MemoryStream(File.ReadAllBytes(openFileDialog1.FileName)));
                AddToBitmapList(img, Path.GetFileName(openFileDialog1.FileName), txtLogoInternalFile.Text);
                toolStripStatusLabel1.Text = openFileDialog1.FileName;
            }
            else
            {
                var nodeFound = false;
                foreach (var node in tvLogo.Nodes.Cast<TreeNode>().Where(node => node.Text == txtLogoInternalFile.Text))
                {
                    node.Name = "";
                    nodeFound = true;
                }
                if (!nodeFound) tvLogo.Nodes.Add(txtLogoInternalFile.Text);
            }
            button1.Text = Resources.Replace;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (tvLogo.SelectedNode == null) return;
            if((cboMoto.SelectedIndex == 0))
                tvLogo.SelectedNode.Remove();
            else
            {
                tvLogo.SelectedNode.Name = "";
                if (tvLogo.Nodes.Count == 0)
                    ClearBitmapList();
            }
        }

        private void SetRadioButtons(int index)
        {
            switch (_loadedbitmapimageoptions[index])
            {
                case ImageOption.ImageOptionCenter:
                    rdoImageCenter.Checked = true;
                    break;
                case ImageOption.ImageOptionFill:
                    rdoImageFill.Checked = true;
                    break;
                case ImageOption.ImageOptionStretchProportionately:
                    rdoImageStretchAspect.Checked = true;
                    break;
            }
            switch (_loadedbitmapimagelayout[index])
            {
                case ImageLayout.ImageLayoutPortrait:
                    rdoLayoutPortrait.Checked = true;
                    break;
                case ImageLayout.ImageLayoutLandscape:
                    rdoLayoutLandscape.Checked = true;
                    break;
            }
        }

        bool _tvLogoAfterSelectProcessing;
        private void tvLogo_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_tvLogoAfterSelectProcessing) return;
            _tvLogoAfterSelectProcessing = true;
            try
            {
                var index = Convert.ToInt32(tvLogo.SelectedNode.Name);
                var bitmap = _loadedbitmaps[index];
                if (bitmap == null) return;
                SetRadioButtons(index);
                pictureBox1.Image = FixedSizePreview(bitmap);
                toolStripStatusLabel1.Text = _loadedbitmapnames[index]
                    + @": " + bitmap.Width + @"x" + bitmap.Height;
                Application.DoEvents();
            }
            catch
            {
                pictureBox1.Image = new Bitmap(1, 1);
                toolStripStatusLabel1.Text = "";
                Application.DoEvents();
            }
            _tvLogoAfterSelectProcessing = false;
        }

        private void tvLogo_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (tvLogo.SelectedNode == null) return;
            openFileDialog1.Filter = Resources.SelectImageFile;
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            var img = new Bitmap(new MemoryStream(File.ReadAllBytes(openFileDialog1.FileName)));
            AddToBitmapList(img, Path.GetFileName(openFileDialog1.FileName), tvLogo.SelectedNode.Text);
            toolStripStatusLabel1.Text = openFileDialog1.FileName;
            tvLogo_AfterSelect(sender, null);
        }

        private void txtLogoInternalFile_TextChanged(object sender, EventArgs e)
        {
            button1.Text = tvLogo.Nodes.Cast<TreeNode>().Any(node => node.Text == txtLogoInternalFile.Text) 
                ? Resources.Replace 
                : Resources.Append;
        }

        private static byte[] ExtractLogoBin(string zipfilename)
        {
            byte[] buffer = null;
            using (var input = new ZipInputStream(zipfilename))
            {
                ZipEntry e;
                while ((e = input.GetNextEntry()) != null)
                {
                    if (e.IsDirectory) continue;
                    if (Path.GetFileName(e.FileName) != "logo.bin") continue;
                    buffer = new byte[e.UncompressedSize];
                    input.Read(buffer, 0, buffer.Length);
                    break;
                }
            }
            return buffer;
        }

// ReSharper disable once InconsistentNaming
        private Bitmap Decode540x540Image(BinaryReader reader)
        {
            var img = new Bitmap(540, 540, PixelFormat.Format24bppRgb);
            ProgressBar.Visible = true;
            ProgressBar.Maximum = 540;
            ProgressBar.Value = 0;
            ProgressBar.Minimum = 0;
            Application.DoEvents();

            for (var y = 0; y < 540; y++)
            {
                for (var x = 0; x < 540; x++)
                {
                    var blue = reader.ReadByte();
                    var green = reader.ReadByte();
                    var red = reader.ReadByte();
                    img.SetPixel(x, y,
                        Color.FromArgb(blue, green, red));
                }
                ProgressBar.Value++;
                Application.DoEvents();
            }
            ProgressBar.Visible = false;
            Application.DoEvents();
            return img;
        }

        private void OpenFile(string filename)
        {
            var zipFile = false;
            var openfilename = filename;
            byte[] logobin = null;

            try
            {
                if (ZipFile.IsZipFile(filename))
                {
                    zipFile = true;
                    if ((logobin = ExtractLogoBin(filename)) == null)
                    {
                        toolStripStatusLabel1.Text = Resources.Zipfile_logo_bin_error.Replace("<ZFN>",filename);
                        Application.DoEvents();
                        return;
                    }
                    
                }
                Stream stream;

                try
                {
                    if (zipFile)
                        stream = new MemoryStream(logobin);
                    else
                        stream = new FileStream(openfilename, FileMode.Open);
                }
                catch (Exception)
                {
                    ProgressBar.Visible = false;
                    toolStripStatusLabel1.Text = Resources.FileOpenError.Replace("<FN>", filename);
                    return;
                }


                using (var reader = new BinaryReader(stream))
                {
                    _tvLogoAfterSelectProcessing = true;
                    pictureBox1.Image = new Bitmap(1, 1);
                    _fileSaved = false;
                    var android43 = false;
                    cboMoto.SelectedIndex = 0;
                    rdoAndroid44.Checked = true;
                    rdoImageCenter.Checked = true;
                    rdoLayoutPortrait.Checked = true;
                    udResolutionX.Value = 720;
                    udResolutionY.Value = 1280;
                    tvLogo.Nodes.Clear();
                    ClearBitmapList();
                    Bitmap img;
                    if ((reader.ReadInt64() != 0x6F676F4C6F746F4DL) || (reader.ReadByte() != 0x00))
                    {
                        if (reader.BaseStream.Length != 0xD5930)
                        {
                            toolStripStatusLabel1.Text = @"Invalid logo.bin file loaded";
                            return;
                        }
                        reader.BaseStream.Position = 0;
                        rdoAndroidRAW.Checked = true;
                        img = Decode540x540Image(reader);

                        AddToBitmapList(img, 
                            Path.GetFileName(filename) + (zipFile 
                                ? @"\logo.bin\logo_unlocked" 
                                : @"\logo_unlocked"), 
                            "logo_unlocked");
                        toolStripStatusLabel1.Text = @"Processing Complete :)";
                        return;
                    }
                    var count = (reader.ReadInt32() - 0x0D) / 0x20;
                    var name = new string[count];
                    var offset = new Int32[count];
                    var size = new Int32[count];
                    for (var i = 0; i < count; i++)
                    {
                        reader.BaseStream.Position = 0x0D + (0x20 * i);
                        name[i] = Encoding.ASCII.GetString(reader.ReadBytes(0x18)).Split('\0')[0];
                        offset[i] = reader.ReadInt32();
                        size[i] = reader.ReadInt32();
                    }
                    reader.BaseStream.Position = offset[0];
                    if (reader.ReadInt64() != 0x006E75526F746F4DL)
                    {
                        android43 = true;
                        rdoAndroid43.Checked = true;
                    }
                    for (var i = 0; i < count; i++)
                    {
                        toolStripStatusLabel1.Text = @"Processing " + name[i];
                        ProgressBar.Value = 0;
                        Application.DoEvents();

                        if (!android43)
                        {
                            reader.BaseStream.Position = offset[i] + 8;
                            var x = (UInt16) (reader.ReadByte() << 8);
                            x |= reader.ReadByte();
                            var y = (UInt16) (reader.ReadByte() << 8);
                            y |= reader.ReadByte();
                            img = new Bitmap(x, y, PixelFormat.Format24bppRgb);
                            var xx = 0;
                            var yy = 0;
                            ProgressBar.Visible = true;
                            ProgressBar.Maximum = y;
                            ProgressBar.Value = 0;
                            ProgressBar.Minimum = 0;
                            Application.DoEvents();
                            while (yy < y)
                            {
                                var pixelcount = (UInt16) (reader.ReadByte() << 8);
                                pixelcount |= reader.ReadByte();
                                var repeat = (pixelcount & 0x8000) == 0x8000;
                                pixelcount &= 0x7FFF;

                                int red, green, blue;

                                if (repeat)
                                {
                                    blue = reader.ReadByte();
                                    green = reader.ReadByte();
                                    red = reader.ReadByte();
                                    while (pixelcount-- > 0)
                                    {
                                        img.SetPixel(xx++, yy,
                                            Color.FromArgb(red, green, blue));
                                        if (xx != x) continue;
                                        ProgressBar.Value++;
                                        Application.DoEvents();
                                        xx = 0;
                                        yy++;
                                        if (yy == y) break;
                                    }
                                }
                                else
                                {
                                    while (pixelcount-- > 0)
                                    {
                                        blue = reader.ReadByte();
                                        green = reader.ReadByte();
                                        red = reader.ReadByte();
                                        img.SetPixel(xx++, yy,
                                            Color.FromArgb(red, green, blue));
                                        if (xx != x) continue;
                                        ProgressBar.Value++;
                                        Application.DoEvents();
                                        xx = 0;
                                        yy++;
                                        if (yy == y) break;
                                    }
                                }
                            }
                            ProgressBar.Visible = false;
                            Application.DoEvents();
                        }
                        else
                        {
                            reader.BaseStream.Position = offset[i];
                            img = Decode540x540Image(reader);
                        }
                        AddToBitmapList(img, 
                            Path.GetFileName(filename) + (zipFile 
                                ? @"\logo.bin\" 
                                : @"\") + name[i], 
                            name[i]);


                    }
                    _tvLogoAfterSelectProcessing = false;
                }
            }
            catch (Exception ex)
            {
                ProgressBar.Visible = false;
                toolStripStatusLabel1.Text = @"Exception: " + ex.GetBaseException();
                return;
            }

            toolStripStatusLabel1.Text = @"File Load Complete :)";
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = @"Logo Files|*.zip;*.bin|Bin Files|*.bin|Flashable Zip files|*.zip|All Files|*.*";
            if (openFileDialog1.ShowDialog() != DialogResult.OK) return;
            OpenFile(openFileDialog1.FileName);
        }


        private byte[] encode_image(Bitmap img)
        {
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            ProgressBar.Visible = true;
            ProgressBar.Minimum = 0;
            ProgressBar.Value = 0;
            ProgressBar.Maximum = img.Height;
            Application.DoEvents();
            if (!rdoAndroid44.Checked)
            {
                for (var y = 0; y < 540; y++)
                {
                    for (var x = 0; x < 540; x++)
                    {
                        writer.Write(img.GetPixel(x, y).B);
                        writer.Write(img.GetPixel(x, y).G);
                        writer.Write(img.GetPixel(x, y).R);
                    }
                    ProgressBar.Value++;
                    Application.DoEvents();
                }
            }
            else
            {
                writer.Write(0x006E75526F746F4DL);
                writer.Write((byte)(img.Width >> 8));
                writer.Write((byte)(img.Width & 0xFF));
                writer.Write((byte)(img.Height >> 8));
                writer.Write((byte)(img.Height & 0xFF));

                for (var y = 0; y < img.Height; y++)
                {
                    var colors = new Color[img.Width];
                    for (var x = 0; x < img.Width; x++)
                        colors[x] = Color.FromArgb(255, img.GetPixel(x, y));
                    var compress = compress_row(colors);
                    writer.Write(compress);
                    ProgressBar.Value++;
                    Application.DoEvents();
                }
            }
            ProgressBar.Visible = false;
            Application.DoEvents();
            return stream.ToArray();
        }

        private static byte[] compress_row(IList<Color> colors)
        {
            var j = 0;
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            while (j < colors.Count)
            {
                var k = j;
                while ((k < colors.Count) && (colors[j] == colors[k]))
                {
                    k++;
                }
                if ((k - j) > 1)
                {
                    writer.Write((byte)(0x80 | ((k - j) >> 8)));
                    writer.Write((byte)((k - j) & 0xFF));
                    writer.Write(colors[j].B);
                    writer.Write(colors[j].G);
                    writer.Write(colors[j].R);
                    j = k;
                }
                else
                {
                    var l = k;
                    int m;
                    do
                    {
                        k = l - 1;
                        while ((l < colors.Count) && (colors[k] != colors[l]))
                        {
                            k++;
                            l++;
                        }
                        while ((l < colors.Count) && (colors[k] == colors[l]))
                        {
                            l++;
                        }
                        if (l == colors.Count)
                            break;
                        m = l;
                        while ((m < colors.Count) && (colors[l] == colors[m]))
                        {
                            m++;
                        }
                        
                        
                    } while (((l - k) < 3) && ((m-l) < 2));
                    if ((k - j) == 0)
                    {
                        writer.Write((byte)0);
                        writer.Write((byte)1);
                        writer.Write(colors[colors.Count - 1].B);
                        writer.Write(colors[colors.Count - 1].G);
                        writer.Write(colors[colors.Count - 1].R);
                        break;
                    }
                    if (k == (colors.Count - 1))
                        k++;

                    writer.Write((byte)((k - j) >> 8));
                    writer.Write((byte)((k - j) & 0xFF));
                    for (l = 0; l < (k - j); l++)
                    {
                        writer.Write(colors[j + l].B);
                        writer.Write(colors[j + l].G);
                        writer.Write(colors[j + l].R);
                    }
                    j = k;
                }
            }
            return stream.ToArray();
        }

        
        private void SaveFile()
        {
            var stream = new MemoryStream();
            var errorCount = 0;
            var blankCount = 0;
            var errorproceed = false;
            var fileext = Path.GetExtension(saveFileDialog1.FileName);

            using (var writer = new BinaryWriter(stream))
            {
                if (rdoAndroidRAW.Checked)
                {
                    try
                    {
                        _tvLogoAfterSelectProcessing = true;
                        SetRadioButtons(Convert.ToInt32(tvLogo.Nodes[0].Name));
                        var img = FixedSizeSave(_loadedbitmaps[Convert.ToInt32(tvLogo.Nodes[0].Name)]);
                        _tvLogoAfterSelectProcessing = false;
                        writer.Write(encode_image(img));
                    }
                    catch
                    {
                        if (tvLogo.Nodes[0].Name != "")
                        {
                            toolStripStatusLabel1.Text = @"Error loading image - Processing Aborted :(";
                            writer.Close();
                            return;
                        }
                        if (MessageBox.Show(@"Are you sure you wish to proceed with a blank image?",
                            @"Motorola Boot Logo Maker",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2) == DialogResult.No)
                            {
                                toolStripStatusLabel1.Text = @"Processing Aborted";
                                writer.Close();
                                return;
                            }
                        writer.Write(Resources._540x540);
                    }
                    

                }
                else
                {
                    writer.Write(0x6F676F4C6F746F4DL);
                    writer.Write((byte) 0);
                    writer.Write(0x0D + (tvLogo.Nodes.Count*0x20));
                    var android43 = rdoAndroid43.Checked;
                    for (var i = 0; i < tvLogo.Nodes.Count; i++)
                    {
                        writer.BaseStream.Position = 0x0D + (i*0x20);
                        var name = Encoding.ASCII.GetBytes(tvLogo.Nodes[i].Text);
                        writer.Write(name);
                        writer.Write(new byte[0x20 - name.Length]);
                    }
                    for (var i = 0; i < tvLogo.Nodes.Count; i++)
                    {
                        toolStripStatusLabel1.Text = @"Processing " + tvLogo.Nodes[i].Text;
                        while ((writer.BaseStream.Position%0x200) != 0)
                            writer.Write((byte) 0xFF);
                        writer.BaseStream.Position = 0x0D + 0x18 + (i*0x20);
                        writer.Write((int) writer.BaseStream.Length);
                        writer.BaseStream.Position = writer.BaseStream.Length;
                        byte[] result;
                        try
                        {
                            _tvLogoAfterSelectProcessing = true;
                            SetRadioButtons(Convert.ToInt32(tvLogo.Nodes[i].Name));
                            var img = FixedSizeSave(_loadedbitmaps[Convert.ToInt32(tvLogo.Nodes[i].Name)]);
                            result = encode_image(img);
                            _tvLogoAfterSelectProcessing = false;
                            if (!errorproceed && (errorCount > 0))
                            {
                                if (MessageBox.Show(@"At least one image failed to load, " +
                                    @"are you sure you wish to proceed?",
                                    @"Motorola Boot Logo Maker",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question,
                                    MessageBoxDefaultButton.Button2) == DialogResult.No)
                                    {
                                        toolStripStatusLabel1.Text = @"Processing Aborted";
                                        writer.Close();
                                        return;
                                    }
                                errorproceed = true;
                            }
                        }
                        catch
                        {
                            if (tvLogo.Nodes[0].Name != "")
                                errorCount++;
                            else
                                blankCount++;
                            if ((blankCount == tvLogo.Nodes.Count) &&
                               (MessageBox.Show(@"No images were loaded, are you sure you wish to" +
                               @" proceed with blank images?",
                               @"Motorola Boot Logo Maker",
                               MessageBoxButtons.YesNo,
                               MessageBoxIcon.Question,
                               MessageBoxDefaultButton.Button2) == DialogResult.No))
                            {
                                toolStripStatusLabel1.Text = @"Processing Aborted";
                                writer.Close();
                                return;
                            }
                            if (((errorCount + blankCount) == tvLogo.Nodes.Count) && (errorCount > 0))
                            {
                                toolStripStatusLabel1.Text = @"Every single image selected failed to load"+
                                                             @" - Processing Aborted :(";
                                writer.Close();
                                return;
                            }
                            result = android43
                                ? Resources._540x540
                                : Resources.motorun;
                        }
                        writer.Write(result);

                        writer.BaseStream.Position = 0x0D + (i*0x20) + 0x1C;
                        writer.Write(result.Length);
                        writer.BaseStream.Position = writer.BaseStream.Length;
                        if (writer.BaseStream.Length <= _maxFileSize) continue;
                        toolStripStatusLabel1.Text =
                            @"Error: Images/options selected will not fit in logo.bin, Failed at " +
                            tvLogo.Nodes[i].Text + @" Produced file is " +
                            (writer.BaseStream.Length - _maxFileSize) + @" Bytes Too Large";
                        return;
                    }
                }
            }

            if (fileext == ".zip")
            {
                var zipfilename = saveFileDialog1.FileName;

                using (var zip = new ZipFile())
                {
                    zip.AddEntry("logo.bin", stream.ToArray());
                    zip.AddDirectoryByName("META-INF");
                    zip.AddDirectoryByName("META-INF\\com");
                    zip.AddDirectoryByName("META-INF\\com\\google");
                    zip.AddDirectoryByName("META-INF\\com\\google\\android");

                    zip.AddEntry("META-INF\\com\\google\\android\\updater-script", Resources.updater_script);
                    zip.AddEntry("META-INF\\com\\google\\android\\update-binary", Resources.update_binary);
                    zip.Save(zipfilename);
                }
            }
            else
                File.WriteAllBytes(saveFileDialog1.FileName, stream.ToArray());

            toolStripStatusLabel1.Text = @"Processing Complete :)";
            _fileSaved = true;
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "";
            Application.DoEvents();
            saveFileDialog1.Filter = Resources.ZipBins;
            if ((!_fileSaved) && (saveFileDialog1.ShowDialog() != DialogResult.OK)) return;
            try
            {
                SaveFile();
            }
            catch (Exception ex)
            {
                ProgressBar.Visible = false;
                toolStripStatusLabel1.Text = @"Exception during processing: " + ex.GetBaseException();
            }
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            toolStripStatusLabel1.Text = "";
            Application.DoEvents();
            saveFileDialog1.Filter = Resources.ZipBins;
            if (saveFileDialog1.ShowDialog() != DialogResult.OK) return;
            try
            {
                SaveFile();
            }
            catch (Exception ex)
            {
                ProgressBar.Visible = false;
                toolStripStatusLabel1.Text = @"Exception during processing: " + ex.GetBaseException();
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _loadedbitmaps.Clear();
            _loadedbitmapnames.Clear();
            _fileSaved = false;
            rdoAndroid44.Checked = true;
            cboMoto.SelectedIndex = 3;
            tvLogo.Nodes.Clear();
            cboMoto_SelectedIndexChanged(sender,e);
            toolStripStatusLabel1.Text = "";
            Application.DoEvents();
            pictureBox1.Image = new Bitmap(1, 1);
            rdoImageCenter.Checked = true;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            Init_cboMoto("Custom",720,1280,4194304,0x3FFFFFFF);
            Init_cboMoto("Moto E", 540,960,4194304,(int)(LOGO.LOGO_BOOT | LOGO.LOGO_BATTERY | LOGO.LOGO_UNLOCKED | LOGO.LOGO_LOWPOWER | LOGO.LOGO_UNPLUG));
            Init_cboMoto("Moto X", 720,1280,4194304,(int)(LOGO.LOGO_BOOT | LOGO.LOGO_BATTERY | LOGO.LOGO_UNLOCKED ));
            Init_cboMoto("Moto G", 720, 1280, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_BATTERY | LOGO.LOGO_UNLOCKED | LOGO.LOGO_CHARGE));
            Init_cboMoto("Droid Ultra", 720, 1280, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_BATTERY | LOGO.LOGO_UNLOCKED));
            Init_cboMoto("Droid RAZR HD", 720, 1280, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_UNLOCKED | LOGO.KITKAT_DISABLED));
            Init_cboMoto("RAZR i", 540, 960, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_UNLOCKED | LOGO.KITKAT_DISABLED));
            Init_cboMoto("Droid RAZR M", 540, 960, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_UNLOCKED | LOGO.KITKAT_DISABLED));
            Init_cboMoto("Photon Q 4G LTE", 540, 960, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_UNLOCKED | LOGO.KITKAT_DISABLED));
            Init_cboMoto("Atrix HD", 720, 1280, 4194304, (int)(LOGO.LOGO_BOOT | LOGO.LOGO_UNLOCKED | LOGO.KITKAT_DISABLED));
            Init_cboMoto("Droid 4", 540,960,1048576,(int)LOGO.LOGO_RAW);
            Init_cboMoto("Atrix 2", 540, 960, 1048576, (int)LOGO.LOGO_RAW);
            Init_cboMoto("Droid RAZR", 540, 960, 1048576, (int)LOGO.LOGO_RAW);
            Init_cboMoto("Photon 4G", 540, 960, 1048576, (int)LOGO.LOGO_RAW);

            newToolStripMenuItem_Click(sender, e);
        }

        private void rdoAndroid43_CheckedChanged(object sender, EventArgs e)
        {
            if (_tvLogoAfterSelectProcessing) return;
            if (tvLogo.SelectedNode == null) return;
            if (string.IsNullOrEmpty(tvLogo.SelectedNode.Name)) return;
            var index = Convert.ToInt32(tvLogo.SelectedNode.Name);
            _loadedbitmapimageoptions[index] = rdoImageCenter.Checked
                ? ImageOption.ImageOptionCenter
                : rdoImageStretchAspect.Checked
                    ? ImageOption.ImageOptionStretchProportionately
                    : ImageOption.ImageOptionFill;
            _loadedbitmapimagelayout[index] = rdoLayoutLandscape.Checked
                ? ImageLayout.ImageLayoutLandscape
                : ImageLayout.ImageLayoutPortrait;


            tvLogo_AfterSelect(sender, null);
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            new AboutBox1().ShowDialog();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            Bitmap img;
            try
            {
                img = File.Exists(tvLogo.SelectedNode.Name)
                    ? new Bitmap(new MemoryStream(File.ReadAllBytes(tvLogo.SelectedNode.Name)))
                    : _loadedbitmaps[Convert.ToInt32(tvLogo.SelectedNode.Name)];
            }
            catch (Exception)
            {

                return;
            }
            saveFileDialog2.Filter = @"Png file|*.png|Jpeg file|*.jpg|Bitmap File|*.bmp|Gif file|*.gif|All Files|*.*";
            if (saveFileDialog2.ShowDialog() != DialogResult.OK) return;
            try
            {
                

                switch (Path.GetExtension(saveFileDialog2.FileName))
                {
                    case ".gif":
                        img.Save(saveFileDialog2.FileName, ImageFormat.Gif);
                        break;
                    case ".jpg":
                        img.Save(saveFileDialog2.FileName, ImageFormat.Jpeg);
                        break;
                    case ".bmp":
                        img.Save(saveFileDialog2.FileName, ImageFormat.Bmp);
                        break;
                    default:
                        img.Save(saveFileDialog2.FileName, ImageFormat.Png);
                        break;

                }
                toolStripStatusLabel1.Text = @"Image saved as " + Path.GetFileName(saveFileDialog2.FileName) +
                                             @" Successfully :)";
                Application.DoEvents();
            }
            catch (Exception)
            {
                toolStripStatusLabel1.Text = @"Unable to Extract Image from bootlogo :(";
                Application.DoEvents();
            }
        }

        private void rdoAndroidRAW_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoAndroidRAW.Checked)
            {
                if (tvLogo.Nodes.Count <= 1) return;
                for(var i = tvLogo.Nodes.Count-1;i >= 0; i--)
                {
                    if (tvLogo.Nodes[i].Text == @"logo_unlocked") continue;
                    if(tvLogo.Nodes.Count > 1)
                        tvLogo.Nodes[i].Remove();
                }
            }
            else
            {
                _autoselectlogobinversion = false;
                cboMoto_SelectedIndexChanged(sender, e);
                _autoselectlogobinversion = true;
            }
            tvLogo_AfterSelect(sender, null);
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
            if((files.Count() == 1) && ((Path.GetExtension(files[0]) == ".bin") || 
                                        (Path.GetExtension(files[0]) == ".zip")))
                e.Effect = DragDropEffects.Copy;
        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
            if ((files.Count() == 1) && ((Path.GetExtension(files[0]) == ".bin") ||
                                         (Path.GetExtension(files[0]) == ".zip")))
            {
                OpenFile(files[0]);
            }
        }

        private void udResolutionY_ValueChanged(object sender, EventArgs e)
        {
            tvLogo_AfterSelect(sender, null);
        }

        private void Init_cboMoto(string device, int resolutionX, int resolutionY, int logobinsize, UInt32 logoContents)
        {
            cboMoto.Items.Add(device);
            _deviceResolutionX.Add(resolutionX);
            _deviceResolutionY.Add(resolutionY);
            _deviceLogoBinSize.Add(logobinsize);
            _deviceLogoBinContents.Add(logoContents);
        }

        private void cboMoto_SelectedIndexChanged(object sender, EventArgs e)
        {
            var idx = cboMoto.SelectedIndex;
            udResolutionX.Enabled = (idx == 0);
            udResolutionY.Enabled = (idx == 0);
            udResolutionX.Value = _deviceResolutionX[idx];
            udResolutionY.Value = _deviceResolutionY[idx];
            _maxFileSize = _deviceLogoBinSize[idx];
            init_tree(_deviceLogoBinContents[idx]);
            toolStripStatusLabel1.Text = @"Max Logo.bin size = " + (_maxFileSize / 1024 / 1024) + @"MiB";
        }
    }
}
