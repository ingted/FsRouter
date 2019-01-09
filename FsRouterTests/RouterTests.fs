module Tests

open System.Threading
open Xunit
open Hopac
open FsRouter

[<Fact>]
let ``Single router`` () =
    use wait = new ManualResetEvent (false)
    
    let router msg =
        job {
            wait.Set () |> ignore
            return msg |> Some
        }
        
    Message.createNoHeaders "testing" |> router |> queueIgnore
    
    wait.WaitOne() |> ignore

open System.Net.Http
open Operators

[<Fact>]
let ``Compose two routers`` () =
    use wait1 = new ManualResetEvent (false)
    use wait2 = new ManualResetEvent (false)
    
    let router1 msg =
        job {
            wait1.Set () |> ignore
            return msg |> Some
        }
    let router2 msg =
        job {
            wait2.Set () |> ignore
            return msg |> Some
        }
        
    let routerComposed = router1 >=> router2
    
    Message.createNoHeaders "testing" |> routerComposed |> queueIgnore
    
    WaitHandle.WaitAll ([|wait1; wait2|])

[<Fact>]
let ``Broadcast to four routers`` () =
    use wait1 = new ManualResetEvent (false)
    use wait2 = new ManualResetEvent (false)
    use wait3 = new ManualResetEvent (false)
    use wait4 = new ManualResetEvent (false)
    let router1 msg =
        job {
            wait1.Set () |> ignore
            return msg |> Some
        }
    let router2 msg =
        job {
            wait2.Set () |> ignore
            return msg |> Some
        }
    let router3 msg =
        job {
            wait3.Set () |> ignore
            return msg |> Some
        }
    let router4 msg =
        job {
            wait4.Set () |> ignore
            return msg |> Some
        }
        
    let route =
        Routers.broadcast
            [
                router1
                router2
                router3
                router4
            ]
    
    Message.createNoHeaders "testing" |> route |> queueIgnore
    
    WaitHandle.WaitAll ([|wait1; wait2; wait3; wait4|])

[<Fact>]
let ``Route using table`` () =
    use wait1 = new ManualResetEvent (false)
    use wait2 = new ManualResetEvent (false)
    use wait3 = new ManualResetEvent (false)
    use wait4 = new ManualResetEvent (false)
    let router1 msg =
        job {
            if msg.Content = "testing1" then
                wait1.Set () |> ignore
            return msg |> Some
        }
    let router2 msg =
        job {
            if msg.Content = "testing2" then
                wait2.Set () |> ignore
            return msg |> Some
        }
    let router3 msg =
        job {
            if msg.Content = "testing3" then
                wait3.Set () |> ignore
            return msg |> Some
        }
    let router4 msg =
        job {
            if msg.Content = "testing4" then
                wait4.Set () |> ignore
            return msg |> Some
        }
        
    let table =
        [
            ("Route3", router3)
            ("Route4", router4)
            ("Route2", router2)
            ("Route1", router1)
        ] |> Map.ofList
    
    let messageNameRouter = Routers.header table "MessageName"
    
    Message.createNoHeaders "testing1" |> Message.addHeader ("MessageName", "Route1") |> messageNameRouter |> queueIgnore
    Message.createNoHeaders "testing2" |> Message.addHeader ("MessageName", "Route2") |> messageNameRouter |> queueIgnore
    Message.createNoHeaders "testing3" |> Message.addHeader ("MessageName", "Route3") |> messageNameRouter |> queueIgnore
    Message.createNoHeaders "testing4" |> Message.addHeader ("MessageName", "Route4") |> messageNameRouter |> queueIgnore
    
    WaitHandle.WaitAll ([|wait1; wait2; wait3; wait4|])

[<Fact>]
let ``Send http request`` () =
    let headers =
        [
            ("http.method", "GET")
            ("target", "https://www.google.com")
        ] |> Map.ofList
    use httpClient = new HttpClient ()
    use wait = new ManualResetEvent (false)
    let mutable (m:Message<string>) = Unchecked.defaultof<Message<string>>
    let handleResponseRouter (msg:Message<string>) =
        job {
            m <- msg
            wait.Set () |> ignore
            return msg |> Some
        }
    Message.create headers "" |> ((Routers.http httpClient) >=> handleResponseRouter) |> queueIgnore
    wait.WaitOne () |> ignore
    Assert.Equal("gws", m.Headers.["http.Server"])
