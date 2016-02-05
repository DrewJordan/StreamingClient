using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.Serialization;

namespace Objects
{
    [DataContract]
    public class Playlist
    {
        [DataMember]
        public string FriendlyName { get; set; }
        [DataMember]
        public string Path { get; set; }
        [DataMember]
        public List<Song> Songs  { get; set; }

    }
}
