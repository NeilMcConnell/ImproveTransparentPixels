using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImproveTransparentPixels
{
    interface Operation
    {
        void DoOperation(Processor processor);
    }

    public class SolidifyOperation : Operation
    {
        public FilterShape FilterShape = FilterShape.Square;
        public int FilterRadius = 2;

        public int MaxDistance = int.MaxValue;
        void Operation.DoOperation(Processor processor) => processor.Solidify(MaxDistance, FilterShape, FilterRadius);
    }

    public class SetColorOperation : Operation
    {
        public MagickColor Color = new MagickColor("#000");
        void Operation.DoOperation(Processor processor) => processor.SetColor(Color);
    }

    public class PreviewOperation: Operation
    {
        public string Filename;
        void Operation.DoOperation(Processor processor) => processor.WritePreviewFile(Filename);
    }

    public class OutputOperation : Operation
    {
        public string Filename;
        void Operation.DoOperation(Processor processor) => processor.WriteOuputFile(Filename);
    }

}
