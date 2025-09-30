using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class SetLightRequest
    {
        public SetLightRequest()
        {
            Port = 55443;
        }
        public string IP { get; set; }
        public int Port { get; set; }
        public int Brightness { get; set; }
        public bool Power { get; set; }
        public string Color { get; set; }
    }
}
