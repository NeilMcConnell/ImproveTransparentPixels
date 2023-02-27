using ImageMagick;
namespace ImproveTransparentPixels
{
    internal class Processor
    {
        struct PositionAndColor
        {
            public PositionAndColor(int x, int y)
            {
                X = x;
                Y = y;
                C0 = 0;
                C1 = 0;
                C2 = 0;
            }

            public int X;
            public int Y;
            public float C0;
            public float C1;
            public float C2;
            public float this[int i]
            {
                get => i switch { 0 => C0, 1 => C1, 2 => C2, _ => throw new IndexOutOfRangeException() };
                set
                {
                    switch (i)
                    {
                        case 0: C0 = value; break;
                        case 1: C1 = value; break;
                        case 2: C2 = value; break;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }
        }


        private readonly MagickImage _srcImage;
        private readonly int _width;
        private readonly int _height;
        private readonly int _channelCount;
        private readonly int _alphaChannelIndex;
        private readonly int[] _colorChannelIndices;
        private readonly (int x, int y)[] _samplePattern;
        private readonly float[] _sampleWeights;

        //processed image
        bool[,] _hasData;  //hasData is true if a pixel is non-transparent, or if it has already been processed
        ushort[] _pixels;


        public Processor(MagickImage srcImage)
        {
            _srcImage = srcImage;
            _channelCount = srcImage.ChannelCount;
            PixelChannel[] channels = srcImage.Channels.ToArray();
            List<int> colorChannelIndices = new();
            _alphaChannelIndex = -1;
            for (int index = 0; index < channels.Length; ++index)
            {
                if (channels[index] == PixelChannel.Alpha)
                {
                    _alphaChannelIndex = index;
                }
                else if (channels[index] == PixelChannel.Index || channels[index] == PixelChannel.Composite)
                {
                    throw new Exception($"Cannot process an image that contains {channels[index]} channel");
                }
                else
                {
                    colorChannelIndices.Add(index);
                }
            }
            if (colorChannelIndices.Count == 0 || colorChannelIndices.Count > 3)
            {
                throw new Exception($"Cannot process this image - it has {colorChannelIndices.Count} color channels");
            }
            if (_alphaChannelIndex == -1)
            {
                throw new Exception($"Cannot process this image - it has no alpha channel");
            }
            _colorChannelIndices = colorChannelIndices.ToArray();
            _height = srcImage.Height;
            _width = srcImage.Width;

            //set up the processed Image
            _pixels = _srcImage.GetPixels().GetArea(0, 0, _width, _height);
            _hasData = FindNonTransparentPixels();

            SetSampling(out _samplePattern, out _sampleWeights);
        }


        //TODO - update sampling pattern?  Gaussian?  Honestly it looks pretty fine even if you just set all weights to 1
        private void SetSampling(out (int x, int y)[] samplePattern, out float[] sampleWeights)
        {
            List<(int x, int y)> offsets = new();
            List<float> weights = new();

            for (int x = -2; x <= 2; ++x)
            {
                for (int y = -2; y <= 2; ++y)
                {
                    if (x == 0 && y == 0) continue;
                    offsets.Add((x, y));
                    weights.Add(1 / MathF.Sqrt(x * x + y * y));
                }
            }
            samplePattern = offsets.ToArray();
            sampleWeights = weights.ToArray();
        }


        private void SolidifyOnceMT(List<PositionAndColor> currentWorkload, ushort[] pixels)
        {
            int workers = Math.Max(8, currentWorkload.Count / 100);
            if (workers < 2)
            {
                SolidifyOnce(currentWorkload, pixels, 0, currentWorkload.Count);
            }
            else
            {
                Parallel.For(0, workers, workerIndex => {
                    int start = currentWorkload.Count * workerIndex / workers;
                    int end = currentWorkload.Count * (workerIndex + 1) / workers;
                    SolidifyOnce(currentWorkload, pixels, start, end);
                });
            }
        }

        private void SolidifyOnce(List<PositionAndColor> currentWorkload, Span<ushort> pixels, int startIndex, int endIndex)
        {
            for (int indexInWorkload = startIndex; indexInWorkload < endIndex; indexInWorkload++)
            {
                currentWorkload[indexInWorkload] = SolidifyOnePixel(currentWorkload[indexInWorkload], pixels);
            }
        }

        private PositionAndColor SolidifyOnePixel(PositionAndColor positionAndColor, Span<ushort> pixels)
        {
            float totalWeight = 0;
            for (int sampleIndex = 0; sampleIndex < _samplePattern.Length; ++sampleIndex)
            {
                int sampleX = _samplePattern[sampleIndex].x + positionAndColor.X;
                int sampleY = _samplePattern[sampleIndex].y + positionAndColor.Y;

                if (SafeGetHasData(sampleX, sampleY))
                {
                    float sampleWeight = _sampleWeights[sampleIndex];
                    totalWeight += sampleWeight;
                    var pixel = GetPixel(pixels, sampleX, sampleY);
                    for (int x = 0; x < _colorChannelIndices.Length; ++x)
                    {
                        positionAndColor[x] += pixel[_colorChannelIndices[x]] * sampleWeight;
                    }
                }
            }
            if (totalWeight == 0)
            {
                throw new Exception("Total weight was zero.  Probably there's a pixel in currentWorkLoad that should not be there");
            }
            for (int x = 0; x < _colorChannelIndices.Length; ++x)
            {
                positionAndColor[x] /= totalWeight;
            }
            return positionAndColor;
        }

        private Span<ushort> GetPixel(Span<ushort> pixels, int x, int y) => pixels.Slice((y * _width + x) * _channelCount, _channelCount);

        private void UpdateImage(List<PositionAndColor> currentWorkload, Span<ushort> pixels)
        {
            for (int indexInWorkload = 0; indexInWorkload < currentWorkload.Count; indexInWorkload++)
            {
                var positionAndColor = currentWorkload[indexInWorkload];
                var pixel = GetPixel(pixels, positionAndColor.X, positionAndColor.Y);
                for (int x = 0; x < _colorChannelIndices.Length; ++x)
                {
                    int newValue = (int)(positionAndColor[x] + 0.5f);
                    if (newValue < ushort.MinValue || newValue > ushort.MaxValue)
                    {
                        System.Console.WriteLine("Possible logic error - newvalue was " + newValue);
                    }

                    pixel[_colorChannelIndices[x]] = (ushort)Math.Clamp(newValue, ushort.MinValue, ushort.MaxValue);
                }
            }
        }


        private void UpdateHasData(List<PositionAndColor> currentWorkload)
        {
            for (int indexInWorkload = 0; indexInWorkload < currentWorkload.Count; indexInWorkload++)
            {
                var positionAndColor = currentWorkload[indexInWorkload];
                _hasData[positionAndColor.Y, positionAndColor.X] = true;
            }
        }

        private HashSet<(int x, int y)> _reusablePositionsHashSet = new HashSet<(int x, int y)>();
        void NextWorkload(List<PositionAndColor> currentWorkload)
        {
            HashSet<(int x, int y)> nextPoints = _reusablePositionsHashSet;
            nextPoints.Clear();
            foreach (var position in currentWorkload)
            {
                int x = position.X;
                int y = position.Y;
                if (x < _width - 1 && !SafeGetHasData(x + 1, y))
                    nextPoints.Add((x + 1, y));
                if (x > 0 && !SafeGetHasData(x - 1, y))
                    nextPoints.Add((x - 1, y));
                if (y < _height - 1 && !SafeGetHasData(x, y + 1))
                    nextPoints.Add((x, y + 1));
                if (y > 0 && !SafeGetHasData(x, y - 1))
                    nextPoints.Add((x, y - 1));
            }
            currentWorkload.Clear();
            foreach (var point in nextPoints)
            {
                currentWorkload.Add(new PositionAndColor(point.x, point.y));
            }
        }

        bool SafeGetHasData(int x, int y) => x >= 0 && y >= 0 && x < _width && y < _height && _hasData[y, x];

        //NOTE that the returned array should be indexed by [y,x]
        bool[,] FindNonTransparentPixels()
        {
            bool[,] nonTransparent = new bool[_height, _width];

            for (int i = 0; i < _height; i++)
            {
                for (int j = 0; j < _width; j++)
                {
                    var pixel = GetPixel(_pixels, j, i);
                    nonTransparent[i, j] = pixel[_alphaChannelIndex] > 0;
                }
            }
            return nonTransparent;
        }


        List<PositionAndColor> SolidifyFindStartingPixels()
        {
            List<PositionAndColor> startingPoints = new List<PositionAndColor>();
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (!SafeGetHasData(x, y)
                        && (SafeGetHasData(x, y + 1)
                         || SafeGetHasData(x, y - 1)
                         || SafeGetHasData(x + 1, y)
                         || SafeGetHasData(x - 1, y)))
                        startingPoints.Add(new PositionAndColor(x, y));
                }
            }
            return startingPoints;
        }

        public void Solidify(int maxDistance)
        {
            Span<ushort> pixelsSpan = _pixels;
            List<PositionAndColor> currentWorkload = SolidifyFindStartingPixels();

            for (int cycles = 0; cycles < maxDistance && currentWorkload.Count > 0; ++cycles)
            {
                //SolidifyOnce(currentWorkload, _pixels, 0, currentWorkload.Count);
                SolidifyOnceMT(currentWorkload, _pixels);
                UpdateImage(currentWorkload, pixelsSpan);
                UpdateHasData(currentWorkload);
                NextWorkload(currentWorkload);
            }
        }

        public void SetColor(MagickColor color)
        {
            ushort[] colorArray = _srcImage.Channels.Select(channel => channel switch
            {
                PixelChannel.Red => color.R,
                PixelChannel.Green => color.G,
                PixelChannel.Blue => color.B,
                PixelChannel.Alpha => (ushort)0,
                _ => throw new Exception("Unhandled channel " + channel)
            }).ToArray();

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    if (!_hasData[y, x])
                    {
                        var pixel = GetPixel(_pixels, x, y);
                        colorArray.CopyTo(pixel);
                        _hasData[y, x] = true;
                    }
                }
            }
        }

        public MagickImage GetOutput()
        {
            MagickImage output = new MagickImage(_srcImage);
            output.GetPixels().SetArea(0, 0, _width, _height, _pixels);
            return output;
        }

        public void WriteOuputFile(string filePath) => GetOutput().Write(filePath);
        public void WritePreviewFile(string filePath)
        {
            var image = GetOutput();
            image.Evaluate(Channels.Alpha, EvaluateOperator.Set, ushort.MaxValue);
            image.Write(filePath);
        }

    }
}
