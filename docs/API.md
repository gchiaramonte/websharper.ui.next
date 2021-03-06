## API Reference
> [Documentation](../README.md) ▸ **API Reference**

* [Dataflow](Dataflow.md) - dataflow support
  * [Var](Var.md) - reactive variables
  * [View](View.md), ViewBuilder - computed reactive nodes
  * [Key](Key.md) - helper type for generating unique identifiers 
  * [Model](Model.md) - helpers for imperative models
  * [ListModel](ListModel.md) - `ResizeArray`-like reactive model helpers
  * [Submitter](Submitter.md) - helper to bring events in the dataflow
* DOM
  * [Attr](Attr.md) - attributes
  * [Doc](Doc.md) - document fragments
  * [Html](Html.md) - HTML Helper Functions
  * [Templates](Templates.md) - Using HTML files as templates
* [Animation](Animation.md) - support for animation
  * [Anim](Anim.md) - abstract animation types
  * [Easing](Easing.md) - easing functions
  * [Interpolation](Interpolation.md) - interpolation between two values
  * [NormalizedTime](NormalizedTime.md) - type alias for the `[0, 1]` range
  * [Time](Time.md) - type alias for duration in milliseconds
  * [Trans](Trans.md) - support for animating change, enter and exit transitions
* Structure
  * [Flow](Flow.md), FlowBuilder - multi-stage documents such as wizards
  * [Router](Router.md), [RouteId](Router.md#RouteId) - support for routing and structuring sites
  * [RouteMap](RouteMap.md) - bijection between a route and an action type
* Input
  * [Input](Input.md) - Views of the mouse and keyboard
* Misc
  * [Notation](Notation.md)
  * [Client vs Server](ClientServer.md) - a discussion of client-side and server-side functionality
