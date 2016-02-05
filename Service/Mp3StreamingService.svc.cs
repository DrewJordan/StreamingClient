using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.ServiceModel.Web;
using Id3;
using Objects;

namespace Service
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Mp3StreamingService : IStreamingService
    {
        public Mp3StreamingService()
        {
            genres = new Dictionary<int, string>
            {
                {0,"Blues"},
                {  1,"Classic Rock"},
                {  2,"Country"},
                {  3,"Dance"},
                {  4,"Disco"},
                {  5,"Funk"},
                {  6,"Grunge"},
                {  7,"Hip-Hop"},
                {  8,"Jazz"},
                {  9,"Metal"},
                { 10,"New Age"},
                { 11,"Oldies"},
                { 12,"Other"},
                { 13,"Pop"},
                { 14,"R&B"},
                { 15,"Rap"},
                { 16,"Reggae"},
                { 17,"Rock"},
                { 18,"Techno"},
                { 19,"Industrial"},
                { 20,"Alternative"},
                { 21,"Ska"},
                { 22,"Death Metal"},
                { 23,"Pranks"},
                { 24,"Soundtrack"},
                { 25,"Euro-Techno"},
                { 26,"Ambient"},
                { 27,"Trip-Hop"},
                { 28,"Vocal"},
                { 29,"Jazz+Funk"},
                { 30,"Fusion"},
                { 31,"Trance"},
                { 32,"Classical"},
                { 33,"Instrumental"},
                { 34,"Acid"},
                { 35,"House"},
                { 36,"Game"},
                { 37,"Sound Clip"},
                { 38,"Gospel"},
                { 39,"Noise"},
                { 40,"AlternRock"},
                { 41,"Bass"},
                { 42,"Soul"},
                { 43,"Punk"},
                { 44,"Space"},
                { 45,"Meditative"},
                { 46,"Instrumental Pop"},
                { 47,"Instrumental Rock"},
                { 48,"Ethnic"},
                { 49,"Gothic"},
                { 50,"Darkwave"},
                { 51,"Techno-Industrial"},
                { 52,"Electronic"},
                { 53,"Pop-Folk"},
                { 54,"Eurodance"},
                { 55,"Dream"},
                { 56,"Southern Rock"},
                { 57,"Comedy"},
                { 58,"Cult"},
                { 59,"Gangsta"},
                { 60,"Top 40"},
                { 61,"Christian Rap"},
                { 62,"Pop/Funk"},
                { 63,"Jungle"},
                { 64,"Native American"},
                { 65,"Cabaret"},
                { 66,"New Wave"},
                { 67,"Psychadelic"},
                { 68,"Rave"},
                { 69,"Showtunes"},
                { 70,"Trailer"},
                { 71,"Lo-Fi"},
                { 72,"Tribal"},
                { 73,"Acid Punk"},
                { 74,"Acid Jazz"},
                { 75,"Polka"},
                { 76,"Retro"},
                { 77,"Musical"},
                { 78,"Rock & Roll"},
                { 79,"Hard Rock"},
                {  80,"Folk"},
                { 81,"Folk-Rock"},
                { 82,"National Folk"},
                { 83,"Swing"},
                { 84,"Fast Fusion"},
                { 85,"Bebob"},
                { 86,"Latin"},
                { 87,"Revival"},
                { 88,"Celtic"},
                { 89,"Bluegrass"},
                { 90,"Avantgarde"},
                { 91,"Gothic Rock"},
                { 92,"Progressive Rock"},
                { 93,"Psychedelic Rock"},
                { 94,"Symphonic Rock"},
                { 95,"Slow Rock"},
                { 96,"Big Band"},
                { 97,"Chorus"},
                { 98,"Easy Listening"},
                { 99,"Acoustic"},
                {100,"Humour"},
                {101,"Speech"},
                {102,"Chanson"},
                {103,"Opera"},
                {104,"Chamber Music"},
                {105,"Sonata"},
                {106,"Symphony"},
                {107,"Booty Bass"},
                {108,"Primus"},
                {109,"Porn Groove"},
                {110,"Satire"},
                {111,"Slow Jam"},
                {112,"Club"},
                {113,"Tango"},
                {114,"Samba"},
                {115,"Folklore"},
                {116,"Ballad"},
                {117,"Power Ballad"},
                {118,"Rhythmic Soul"},
                {119,"Freestyle"},
                {120,"Duet"},
                {121,"Punk Rock"},
                {122,"Drum Solo"},
                {123,"A capella"},
                {124,"Euro-House"},
                {125,"Dance Hall"}
            };
        }

        public Stream GetFile(string path)
        {
            path = path.Replace("|", "\\");
            WebOperationContext.Current.OutgoingResponse.ContentType = "audio/mpeg";
            //path = Path.Combine(basePath, path);
            var bytes = File.ReadAllBytes(path);
            Stream s = new MemoryStream(bytes);
            return s;
        }

        public Dictionary<string, string> GetMetaData(string path)
        {
            Dictionary<string, string> list = new Dictionary<string, string>();
            path = path.Replace("|", "\\");
            using (var stream = new FileStream(path, FileMode.Open))
            {
                Mp3Stream m = new Mp3Stream(stream);
                if (m.HasTags)
                {
                    var tag = m.GetAllTags();

                    list.Add("Artist", tag[0].Artists.Value);
                    list.Add("Album", tag[0].Album.Value);
                    list.Add("Genre", GetGenre(tag[0].Genre.Value));
                    list.Add("Title", tag[0].Title.Value);
                    list.Add("Track", tag[0].Track.Value);
                }
            }
            return list;
        }

        private string GetGenre(string value)
        {
            int key;
            if (string.IsNullOrEmpty(value)) return "";
            if (!int.TryParse(value.Replace("(", "").Replace(")", ""),out key))
            {
                return value;
            }

            if (genres.ContainsKey(key)) return genres[key];
            return "unknown";
        }

        public Stream GetAlbumArtwork(string path)
        {
            string s = string.Empty;
            path = path.Replace("|", "\\");
            var d = new DirectoryInfo(path.Substring(0, path.LastIndexOf("\\"))).GetFiles("AlbumArtSmall*").FirstOrDefault();
            if (d != null)
            {
                s = d.FullName;
            }
            return !string.IsNullOrEmpty(s) ? GetFile(s) : null;
        }

        public string[] GetDirectoryList(string path)
        {
            path = path.Replace("|", "\\");
            var list = Directory.Exists(path) ? Directory.GetFileSystemEntries(path) : Directory.GetLogicalDrives();
            return list;
        }

        public string[] GetBaseDirectoryList()
        {
            string[] list = null;
            list = Directory.GetLogicalDrives();
            return list;
        }


        public List<Playlist> GetAllPlaylists()
        {
            List<Playlist> list = new List<Playlist>();
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = Path.Combine(path, "AudioStreamer");
            if (Directory.Exists(path))
            {
                var l = Directory.GetFileSystemEntries(path, "*.xml");
                foreach (var s in l)
                {
                    var x = s.Substring(s.LastIndexOf("\\")+1, s.Length - s.LastIndexOf("\\") - 5);
                    Playlist p = new Playlist { FriendlyName = x, Path = s };
                    list.Add(p);
                }
            }
            return list;
        }

        public Playlist GetSomething(string some)
        {
            List<Playlist> list = new List<Playlist>();
            string path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path = Path.Combine(path, "AudioStreamer");
            if (Directory.Exists(path))
            {
                var l = Directory.GetFileSystemEntries(path, "*.xml");
                foreach (var s in l)
                {
                    var x = s.Substring(s.LastIndexOf("\\")+1, s.Length - s.LastIndexOf("\\") - 5);
                    Playlist p = new Playlist { FriendlyName = x, Path = s,  Songs = new List<Song>()};

                    DataSet d = new DataSet();
                    DataTable Playlist = new DataTable();
                    d.ReadXml(s);
                    if (d.Tables.Contains("BasePlaylist"))
                    {
                        Playlist = d.Tables["BasePlaylist"];
                    }
                    Playlist.PrimaryKey = new[] { Playlist.Columns["Path"] };
                    
                    
                    int track;
                    foreach (DataRow row in Playlist.Rows)
                    {
                        p.Songs.Add(new Song
                        {
                            Album = row["Album"].ToString(),
                            Artist = row["Artist"].ToString(),
                            Genre = row["Genre"].ToString(),
                            StringLength = row["Length"].ToString(),
                            Path = row["Path"].ToString().Replace("\\", "|"),
                            Title = row["Title"].ToString(),
                            Track = int.TryParse(row["Track"].ToString(), out track) ? track : 0
                        });
                    }
                    list.Add(p);
                }
            }
            list.First().Path = list.First().Path.Replace("\\","|");
            return list.First();
        }



        public byte[] GetPlaylistData(string playlistFriendlyName)
        {
            List<Playlist> Playlists = GetAllPlaylists();
            return File.ReadAllBytes(Playlists.First(p => p.FriendlyName == playlistFriendlyName).Path);
        }


        private Dictionary<int, string> genres;
    }


}
