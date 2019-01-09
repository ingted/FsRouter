namespace FsRouter
open Hopac

module Routers =
    open System.Net.Http

    let broadcast (routers:Router<'t> list) (msg:Message<'t>) =
        job {
            routers |> List.iter (fun router -> msg |> router |> queueIgnore)
            return msg |> Some
        }
    
    let header (routingTable:Map<string,Router<'a>>) headerName msg =
        job {
            match msg.Headers |> Map.tryFind headerName with
            | None -> return None
            | Some msgName ->
                match routingTable |> Map.tryFind msgName with
                | Some router ->
                    return! router msg
                | None -> return None
        }

    let http (httpClient:HttpClient) msg =
        job {
            use httpMsg = new HttpRequestMessage ()
            use content = new StringContent (msg.Content, System.Text.Encoding.UTF8)
            httpMsg.Content <- content
            msg.Headers |> Map.iter (fun k v ->
                    match k.ToLower() with
                    | "target" ->
                        match System.Uri.TryCreate (v, System.UriKind.Absolute) with
                        | true, uri -> httpMsg.RequestUri <- uri
                        | _ -> ()
                    | "http.method" ->
                        match v.ToLower() with
                        | "get" -> httpMsg.Method <- HttpMethod.Get
                        | "put" -> httpMsg.Method <- HttpMethod.Put
                        | "head" -> httpMsg.Method <- HttpMethod.Head
                        | "post" -> httpMsg.Method <- HttpMethod.Post
                        | "patch" -> httpMsg.Method <- HttpMethod.Patch
                        | "delete" -> httpMsg.Method <- HttpMethod.Delete
                        | "options" -> httpMsg.Method <- HttpMethod.Options
                        | _ -> ()
                    | "http.content-type" ->
                        match Headers.MediaTypeHeaderValue.TryParse v with
                        | true, contentType ->
                            content.Headers.ContentType <- contentType
                        | _ -> ()
                    | headerName when headerName.StartsWith "http." ->
                        httpMsg.Headers.Add(headerName.Substring 5, v)
                    | _ -> ()
                )
            return!
                if not (isNull httpMsg.RequestUri) then
                    job {
                        let! response = (fun () -> httpMsg |> httpClient.SendAsync) |> Job.fromTask
                        let! content = response.Content.ReadAsStringAsync |> Job.fromTask
                        // get headers from response
                        let httpHeaders =
                            response.Headers
                            |> Seq.map (fun kvp -> (System.String.Concat ("http.", kvp.Key), kvp.Value |> String.concat ","))
                            |> Map.ofSeq
                        // and headers from existing message
                        let originalMsgHeaders = msg.Headers |> Map.filter(fun k _ -> k.StartsWith "http." || k = "target")
                        let newMsgHeaders = originalMsgHeaders |> Map.fold (fun acc k v -> acc |> Map.add k v) httpHeaders
                        return
                            {
                                Id = msg.Id
                                Headers = newMsgHeaders
                                Content = content
                            } |> Some
                    }
                else
                    job { return None }
        }
