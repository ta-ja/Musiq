namespace com.lukasvavrek

open System.IO;
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open FSharp.Data
open Fue.Data
open Fue.Compiler
open System.Net.Http
open System.Net
open System.Text
open System

module GetPage = 
    type ITunesSearchResult = JsonProvider<"./itunes-data.json">
    type Album = { 
        ArtistName : string
        AlbumName : string
        ArtworkUrl100 : string
        ReleaseDate : DateTimeOffset
    }

    [<FunctionName("GetPage")>]
    let Run([<HttpTrigger(AuthorizationLevel.Function, "get", Route = null)>] req: HttpRequest, 
            log: ILogger,
            [<Blob("html/detail.html", FileAccess.Read)>] detailTemplate: Stream,
            [<Blob("html/search.html", FileAccess.Read)>] searchTemplate: Stream) =
        
        let getAlbums musician =
            let path = "https://itunes.apple.com/search"
            let search = [
                ("term", musician);
                ("media", "music");
                ("entity", "album")
            ]

            async { 
                return! Http.AsyncRequestString(path, query=search, httpMethod="GET")
            } |> Async.RunSynchronously

        let serveOkResponse content =
            let response = new HttpResponseMessage(HttpStatusCode.OK)
            response.Content <- new StringContent(content, Encoding.UTF8, "text/html")
            response

        let serveSearchTemplate =
            use reader = new StreamReader(searchTemplate)
            let template = reader.ReadToEnd() 
            serveOkResponse template

        let itunesResultsToAlbum (result: ITunesSearchResult.Result) =
            { ArtistName = result.ArtistName
              AlbumName = result.CollectionName
              ArtworkUrl100 = result.ArtworkUrl100
              ReleaseDate = result.ReleaseDate }

        let serveDetailTemplate musician =
            let data = getAlbums musician

            let searchResults = ITunesSearchResult.Parse(data).Results |> Seq.map itunesResultsToAlbum
            use reader = new StreamReader(detailTemplate)
            let template = reader.ReadToEnd()
            let compiledHtml =
                init
                |> add "albums" searchResults
                |> fromText template

            serveOkResponse compiledHtml

        match req.Query.["name"].ToString() with
        | "" -> serveSearchTemplate
        | musician -> serveDetailTemplate musician
