using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Objects
{
    [DataContract]
    public class Song
    {
        [DataMember]
        public string Artist { get; set; }
        [DataMember]
        public string Album { get; set; }
        [DataMember]
        public string Genre { get; set; }
        [DataMember]
        public int Track { get; set; }
        [DataMember]
        public string Title { get; set; }
        public TimeSpan Length {
            get
            {
                TimeSpan t = TimeSpan.Zero;
                return TimeSpan.TryParse(StringLength, out t) ? t : TimeSpan.Zero;
            }  }
        [DataMember]
        public string StringLength { get; set; }
        [DataMember]
        public string Path { get; set; }
    }
}
