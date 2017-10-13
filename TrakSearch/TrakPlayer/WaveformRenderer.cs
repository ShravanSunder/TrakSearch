using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CSCore;
using CSCore.Codecs;
using CSCore.MediaFoundation;

namespace DJ.Utilities
{
    public class WaveformRenderer : IDisposable
    {
        private MemoryStream waveStream = new MemoryStream();
        private ISampleSource waveSource;

        public void Dispose()
        {

            DisposeResources(ref waveStream, ref waveSource);

            GC.SuppressFinalize(this);
        }

        public  BitmapImage DrawNormalizedAudio (string fullPath, int height, int width)
        {
            try
            {
                var stream = waveStream;
                var wave = waveSource;

                new Task(() => DisposeResources(ref stream, ref wave)).Start();


                waveStream = new MemoryStream();
                waveSource =
                    CodecFactory.Instance.GetCodec(fullPath)
                    //.ChangeSampleRate(1000)
                    .ChangeSampleRate(11025)
                    //.ToMono()
                    .ToSampleSource();

                var input = new float[waveSource.Length];

                waveSource.Read(input, 0, (int)waveSource.Length);

                //var output = new float[input.Length / 4 + 1];
                //Buffer.BlockCopy(input, 0, output, 0, input.Length);

                return DrawNormalizedAudio(input, height, width);

            }
            catch (Exception)
            {
                return new BitmapImage();
            }
        }

        private void DisposeResources(ref MemoryStream stream, ref ISampleSource wave)
        {

            if (stream != null)
            {
                stream.Dispose();
                stream = null;
            }

            if (wave != null)
            {
                wave.Dispose();
                wave = null;
            }

            GC.Collect();
        }

        public BitmapImage DrawNormalizedAudio(float[] data, int height, int width)
        {
            var bmp = new Bitmap(width, height);

            int BORDER_WIDTH = 1;
            width = bmp.Width - (2 * BORDER_WIDTH);
            height = bmp.Height - (2 * BORDER_WIDTH);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.DarkSlateGray);
                Pen pen = new Pen(Color.OrangeRed);
                int size = data.Length;
                for (int iPixel = 0; iPixel < width; iPixel++)
                {
                    // determine start and end points within WAV
                    int start = (int)((float)iPixel * ((float)size / (float)width));
                    int end = (int)((float)(iPixel + 1) * ((float)size / (float)width));
                    float min = float.MaxValue;
                    float max = float.MinValue;
                    for (int i = start; i < end; i++)
                    {
                        float val = data[i];
                        min = val < min ? val : min;
                        max = val > max ? val : max;
                    }
                    int yMax = BORDER_WIDTH + height - (int)((max + 1) * .5 * height);
                    int yMin = BORDER_WIDTH + height - (int)((min + 1) * .5 * height);
                    g.DrawLine(pen, iPixel + BORDER_WIDTH, yMax,
                        iPixel + BORDER_WIDTH, yMin);
                }
            }

            return ToBitmapImage(bmp);
        }

        private BitmapImage ToBitmapImage(Bitmap bitmap)
        {
            bitmap.Save(waveStream, ImageFormat.Jpeg);
            waveStream.Position = 0;

            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = waveStream;
            bi.CacheOption = BitmapCacheOption.None;
            bi.DecodePixelHeight = bitmap.Height;
            bi.DecodePixelWidth = bitmap.Width;
            //bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            bi.EndInit();
            bi.Freeze();


            return bi;


        }

    }
}
