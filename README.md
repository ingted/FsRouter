FsRouter
========

Composable message routers for processing messages.

Messages have two parts - Header and Body.  An FsRouter generally inspects only the message headers and then chooses 
from the next available downstream routers to send the message.  The Body is carried along as the payload, as routing 
usually does not involve content inspection.

However, some routers may actually process the message.  For example, a router could read an XML message body and 
transform the contents to JSON before sending to the next router in the chain.

### Endpoint Routers

A downstream router may not even inspect headers, but simply provide a termination point for message routing.  This is
referred to as an "endpoint router" as it does no further routing, it is the endpoint for processing.  Instead, an 
endpoint router typically sends the message to another system.  For example, an endpoint router may send the message to
an HTTPS URI.

### Intake Routers

Intake routers are bound to a listener where they accept messages and are considered the entry point.

Routing Types
-------------

* Headers - routers that process based on message headers typically check for some header conditions or a combination of
headers and then choose the appropriate downstream router based on this.
* Broadcast - routers that copy the message to all downstream routers.

Routing Chain
-------------

A chain is a series of routers through which messages will be passed, and they will be send down a path in the chain
based on the routing criteria they meet.  A chain can be considered a "top-level" router, only it has no real
implementation other than to pass all message to the first router in the chain.

Example
-------

Router1 - broadcast
    - Router2 - Accounting - Create Account
    - Router3 - Metering - Track quantity of orders
    - Router4 - Fulfillment - Fulfill the order (sequence)
        -Router5 - ReserveParts - Reserve parts needed to fulfill the order
        -Router6 - BuildThing - Build thing to fulfill the order
        -Router7 - ShipThing - Ship thing to customer
        -Router8 - InvoiceAccounting - Send invoice to accounting

Each router may process the message, may drop the message, may do nothing.

Represent the above as this:
```
let routeOrder = 
    broadcast
        [
            accounting
            metering
            reserve >=> build >=> ship >=> invoice
        ]

// With this routing chain, a message can be constructed and routed directly (no headers needed in this case)

order |> Message |> routeOrder
```

In order for this to work, each router has a simple signature: 

```
type Router<'t> = Message<'t> -> Message<'t> option
```

The `broadcast` router has a slightly different signature, as it accepts a list of possible routes, and sends the message to all of them.

```
type broadcast = (routers:Router list) -> unit

```
