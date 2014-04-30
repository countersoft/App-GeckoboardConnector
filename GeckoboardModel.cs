using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeckoboardConnector
{
    class GeckoboardModel
    {
        public class SLAWorkload
        {
            public SLAWorkload()
            {
                OpenSla = 0;
                ClosedSla = 0;
            }

            public string Fullname { get; set; }
            public int OpenSla { get; set; }
            public int ClosedSla { get; set; }
        }

        public class SlaStatusBreakdown
        {
            public SlaStatusBreakdown()
            {
                Stopped = 0;
                Paused = 0;
                Breached = 0;
                Red = 0;
                Amber = 0;
                Green = 0;
            }

            public int SlaId { get; set; }
            public string SlaName { get; set; }
            public int Stopped { get; set; }
            public int Paused { get; set; }
            public int Breached { get; set; }
            public int Red { get; set; }
            public int Amber { get; set; }
            public int Green { get; set; }
        }

    }
}
