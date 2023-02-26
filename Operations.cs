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
        public int MaxDistance = int.MaxValue;
        void Operation.DoOperation(Processor processor) => processor.Solidify(MaxDistance);

    }

}
