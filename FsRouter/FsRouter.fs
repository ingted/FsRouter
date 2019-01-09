namespace FsRouter
open Hopac

/// Message consisting of headers and content.
type Message<'t> =
    {
        Id : System.Guid
        Headers : Map<string,string>
        Content : 't
    }
    
module Message =
    /// Creates a message with headers and content
    let create headers content =
        {
            Id = System.Guid.NewGuid ()
            Headers = headers
            Content = content
        }
    /// Creates a message with empty headers
    let createNoHeaders content = create Map.empty content
    
    /// Adds a header to a message
    let addHeader hdr msg =
        let newHeaders = msg.Headers |> Map.add (fst hdr) (snd hdr)
        { msg with Headers = newHeaders }

/// A router handles messages ofa  certain type.
type Router<'t> = Message<'t> -> Job<Message<'t> option>

module Router =
    let compose (router1 : Message<'t> -> Job<Message<'t> option>) (router2 : Message<'t> -> Job<Message<'t> option>)
                : Message<'t> -> Job<Message<'t> option> =
        fun msg ->
            job {
                match! router1 msg with
                | None -> return None
                | Some res -> return! router2 res
            }
    
module Operators =
    let (>=>) router1 router2 =
        Router.compose router1 router2
