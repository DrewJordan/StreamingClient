using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Web;
using Objects;

namespace Service
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the interface name "IService1" in both code and config file together.
    [ServiceContract(SessionMode = SessionMode.NotAllowed)]
    public interface IStreamingService
    {
        [OperationContract]
        [WebGet(UriTemplate = "/AudioStream/Metadata/{path}",
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Dictionary<string, string> GetMetaData(string path);

        [OperationContract]
        [WebGet(
        UriTemplate = "/AudioStream/GetFile/{path}")]
        Stream GetFile(string path);

        [OperationContract]
        [WebGet(UriTemplate = "/AudioStream/GetAlbumArtwork/{path}")]
        Stream GetAlbumArtwork(string path);

        [WebGet(UriTemplate = "/AudioStream/GetDirectoryList/{path}", 
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare)]
        [OperationContract]
        string[] GetDirectoryList(string path);

        [WebGet(UriTemplate = "/AudioStream/GetBaseDirectoryList", 
            ResponseFormat = WebMessageFormat.Json, 
            BodyStyle = WebMessageBodyStyle.Bare)]
        [OperationContract]
        string[] GetBaseDirectoryList();

        [OperationContract]
        [WebGet(UriTemplate = "/AudioStream/GetPlaylists/{some}",
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare)]
        Playlist GetSomething(string some);

        [OperationContract]
        [WebGet(UriTemplate = "/AudioStream/GetPlaylists",
            ResponseFormat = WebMessageFormat.Json,
            BodyStyle = WebMessageBodyStyle.Bare)]
        List<Playlist> GetAllPlaylists();

        [OperationContract]
        [WebGet(UriTemplate = "/AudioStream/GetPlaylistData/{playlistFriendlyName}",
            BodyStyle = WebMessageBodyStyle.Bare)]
        byte[] GetPlaylistData(string playlistFriendlyName);
    }
}
