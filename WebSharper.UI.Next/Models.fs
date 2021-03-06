// $begin{copyright}
//
// This file is part of WebSharper
//
// Copyright (c) 2008-2014 IntelliFactory
//
// Licensed under the Apache License, Version 2.0 (the "License"); you
// may not use this file except in compliance with the License.  You may
// obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
// implied.  See the License for the specific language governing
// permissions and limitations under the License.
//
// $end{copyright}

namespace WebSharper.UI.Next

[<JavaScript>]
type Key =
    | Key of int

    static member Fresh () = Key (Fresh.Int ())

type Model<'I,'M> =
    | M of Var<'M> * View<'I>

[<JavaScript>]
[<Sealed>]
type Model =

    static member Create proj init =
        let var = Var.Create init
        let view = View.Map proj var.View
        M (var, view)

    static member Update update (M (var, _)) =
        Var.Update var (fun x -> update x; x)

    static member View (M (_, view)) =
        view

type Model<'I,'M> with

    [<JavaScript>]
    member m.View = Model.View m

type Storage<'T> =
    abstract member Add      : 'T -> 'T[] -> 'T[]
    abstract member Init     : unit -> 'T[]
    abstract member RemoveIf : ('T -> bool) -> 'T [] -> 'T[]
    abstract member SetAt    : int -> 'T -> 'T[] -> 'T[]
    abstract member Set      : 'T seq -> 'T[]

type Serializer<'T> =
    {
        Encode : 'T -> obj
        Decode : obj -> 'T
    }

#if ZAFIR
module Macro =
    module CJ = WebSharper.Json.Macro

    open WebSharper.Core.AST

//    type M(comp : WebSharper.Core.Metadata.Compilation) =
//        inherit WebSharper.Core.Macro()
//
//        override this.TranslateCall(_, typ, meth, args, _) =
//            match typ.Generics with 
//            | [t] ->
//                let param = CJ.Parameters.Default tr
//                let enc = CJ.EncodeLambda param tr t
//                let dec = CJ.DecodeLambda param tr t
//                Object [ "Encode", enc; "Decode" dec ]
//            | _ -> failwith "Invalid UI.Next.Serializer macro use"

[<JavaScript>]
module Serializer =
    open WebSharper
    open WebSharper.JavaScript

    let Default =
        {
            Encode = box
            Decode = unbox
        }

//    [<Macro(typeof<Macro.M>)>]
//    let Typed =
//        {
//            Encode = box
//            Decode = unbox
//        }
#else
module Macro =
    module CJ = WebSharper.Json.Macro
    module J = WebSharper.Core.JavaScript.Core
    module M = WebSharper.Core.Macros
    module Q = WebSharper.Core.Quotations
    module R = WebSharper.Core.Reflection

    type M() =
        interface M.IMacro with
            member this.Translate(q, tr) =
                match q with
                | Q.CallOrCallModule ({Generics = [t]}, []) ->
                    let f =
                        let enc = Q.Id.Create "enc" (R.Type.FromType typeof<obj -> obj>)
                        let dec = Q.Id.Create "dec" (R.Type.FromType typeof<obj -> obj>)
                        let recT = R.Type.FromType typeof<Serializer<obj>>
                        Q.Lambda(enc,
                            Q.Lambda(dec,
                                Q.NewRecord(recT, [Q.Var enc; Q.Var dec])))
                        |> tr
                    let param = CJ.Parameters.Default tr
                    let enc = CJ.EncodeLambda param tr t
                    let dec = CJ.DecodeLambda param tr t
                    J.Application(J.Application(f, [enc]), [dec])
                | Q.NoMacro q | q -> failwithf "Invalid macro use: %A" q

[<JavaScript>]
module Serializer =
    open WebSharper
    open WebSharper.JavaScript

    [<Macro(typeof<Macro.M>)>]
    let Default =
        {
            Encode = box
            Decode = unbox
        }
#endif

[<JavaScript>]
module Storage =
    open WebSharper
    open WebSharper.JavaScript
    
    type private ArrayStorage<'T>(init) =

        interface Storage<'T> with
            member x.Add i arr = arr.JS.Push i |> ignore; arr
            member x.Init () = init
            member x.RemoveIf pred arr = Array.filter pred arr
            member x.SetAt idx elem arr = arr.[idx] <- elem; arr
            member x.Set coll = Seq.toArray coll

    type private LocalStorageBackend<'T>(id : string, serializer : Serializer<'T>) =
        let storage = JS.Window.LocalStorage
        let set (arr : 'T[]) = 
            storage.SetItem(id, arr |> Array.map serializer.Encode |> Json.Stringify)
            arr
        let clear () = storage.RemoveItem(id)

        interface Storage<'T> with
            member x.Add i arr = arr.JS.Push i |> ignore; set arr

            member x.Init () =
                let item = storage.GetItem(id)
                if item = null then [||]
                else 
                    try
                        let arr = As<obj []> <| Json.Parse(item)
                        arr |> Array.map serializer.Decode
                    with _ -> [||]

            member x.RemoveIf pred arr = set <| Array.filter pred arr
            member x.SetAt idx elem arr = arr.[idx] <- elem; set arr
            member x.Set coll = set <| Seq.toArray coll

    let InMemory init =
        new ArrayStorage<_>(init) :> Storage<_>

    let LocalStorage id serializer =
        new LocalStorageBackend<_>(id, serializer) :> Storage<_>

[<JavaScript>]
type ListModel<'Key,'T when 'Key : equality> =
    {
        key : 'T -> 'Key
        Var : IRef<'T[]>
        Storage : Storage<'T>
        view : View<seq<'T>>
    }

[<JavaScript>]
module ListModels =

    let Contains keyFn item xs =
        let t = keyFn item
        Array.exists (fun it -> keyFn it = t) xs


type ListModel<'Key,'T> with

    [<Inline>]
    member m.View = m.view

    [<Inline>]
    member m.Key = m.key

    member m.Add item =
        let v = m.Var.Value
        if not (ListModels.Contains m.Key item v) then
            m.Var.Value <- m.Storage.Add item v
        else
            let index = Array.findIndex (fun it -> m.Key it = m.Key item) v
            m.Var.Value <- m.Storage.SetAt index item v

    member m.Remove item =
        let v = m.Var.Value
        if ListModels.Contains m.key item v then
            let keyFn = m.key
            let k = keyFn item
            m.Var.Value <- m.Storage.RemoveIf (fun i -> keyFn i <> k) v

    member m.RemoveBy (f: 'T -> bool) =
        m.Var.Value <- m.Storage.RemoveIf (f >> not) m.Var.Value

    member m.RemoveByKey key =
        m.Var.Value <- m.Storage.RemoveIf (fun i -> m.Key i <> key) m.Var.Value

    member m.Iter fn =
        Array.iter fn m.Var.Value

    member m.Set lst =
        m.Var.Value <- m.Storage.Set lst

    member m.ContainsKey key =
        Array.exists (fun it -> m.key it = key) m.Var.Value

    member m.ContainsKeyAsView key =
        m.Var.View |> View.Map (Array.exists (fun it -> m.key it = key))

    member m.Find pred =
        Array.find pred m.Var.Value

    member m.TryFind pred =
        Array.tryFind pred m.Var.Value

    member m.FindAsView pred =
        m.Var.View |> View.Map (Array.find pred)

    member m.TryFindAsView pred =
        m.Var.View |> View.Map (Array.tryFind pred)

    member m.FindByKey key =
        Array.find (fun it -> m.key it = key) m.Var.Value

    member m.TryFindByKey key =
        Array.tryFind (fun it -> m.key it = key) m.Var.Value

    member m.FindByKeyAsView key =
        m.Var.View |> View.Map (Array.find (fun it -> m.key it = key))

    member m.TryFindByKeyAsView key =
        m.Var.View |> View.Map (Array.tryFind (fun it -> m.key it = key))

    member m.UpdateAll fn =
        m.Var.Update <| fun a ->
            a |> Array.iteri (fun i x ->
                fn x |> Option.iter (fun y -> a.[i] <- y))
            m.Storage.Set a

    member m.UpdateBy fn key =
        let v = m.Var.Value
        match Array.tryFindIndex (fun it -> m.key it = key) v with
        | None -> ()
        | Some index ->
            match fn v.[index] with
            | None -> ()
            | Some value ->
                m.Var.Value <- m.Storage.SetAt index value v

    member m.Clear () =
        m.Var.Value <- m.Storage.Set Seq.empty

    member m.Length =
        m.Var.Value.Length

    member m.LengthAsView =
        m.Var.View |> View.Map (fun arr -> arr.Length)

    member m.LensInto (get: 'T -> 'V) (update: 'T -> 'V -> 'T) (key : 'Key) : IRef<'V> =
        new RefImpl<'Key, 'T, 'V>(m, key, get, update) :> IRef<'V>

    member m.Lens (key: 'Key) =
        m.LensInto id (fun _ -> id) key

    member m.Value
        with [<Inline>] get () = m.Var.Value :> seq<_>
        and [<Inline>] set v = m.Var.Value <- Array.ofSeq v

and [<JavaScript>]
    RefImpl<'K, 'T, 'V when 'K : equality>
        (m: ListModel<'K, 'T>, key: 'K, get: 'T -> 'V, update: 'T -> 'V -> 'T) =

    let id = Fresh.Id()

    interface IRef<'V> with

        member r.Get() =
            m.FindByKey key |> get

        member r.Set(v) =
            m.UpdateBy (fun i -> Some (update i v)) key

        member r.Value
            with get() = (r :> IRef<'V>).Get()
            and set v = (r :> IRef<'V>).Set v

        member r.Update(f) =
            m.UpdateBy (fun i -> Some (update i (f (get i)))) key

        member r.UpdateMaybe(f) =
            m.UpdateBy (fun i -> Option.map (update i) (f (get i))) key

        member r.View =
            m.FindByKeyAsView(key)
            |> View.Map get

        member r.Id =
            id

[<JavaScript>]
[<Sealed>]
type ListModel =

    static member CreateWithStorage<'Key,'T when 'Key : equality>
            (key: 'T -> 'Key) (storage : Storage<'T>) =
        let var =
            Seq.distinctBy key (storage.Init ())
            |> Seq.toArray
            |> Var.Create
        let view = 
            var.View |> View.Map (fun x ->
                storage.Set x |> ignore
                Array.copy x :> seq<_>)
        {
            key = key
            Var = var
            Storage = storage
            view = view
        }

    static member Create<'Key, 'T when 'Key : equality> (key: 'T -> 'Key) init =
        ListModel.CreateWithStorage key (Storage.InMemory <| Seq.toArray init)

    static member Wrap<'Key, 'T, 'U when 'Key : equality>
            (underlying: ListModel<'Key, 'U>)
            (extract: 'T -> 'U)
            (createItem: 'U -> 'T)
            (updateItem: 'T -> 'U -> 'T) =
        let state = ref (Dictionary<'Key, 'T>())
        let init =
            underlying.Var.Value |> Array.map (fun u ->
                let t = createItem u
                (!state).[underlying.Key u] <- t
                t)
        let var : IRef<'T[]> =
            underlying.Var.Lens
                <| fun us ->
                    let newState = Dictionary<'Key, 'T>()
                    let ts =
                        us |> Array.map (fun u ->
                            let k = underlying.Key u
                            let t =
                                if (!state).ContainsKey(k) then
                                    updateItem (!state).[k] u
                                else
                                    createItem u
                            newState.[k] <- t
                            t
                        )
                    state := newState
                    ts
                <| fun us ts ->
                    let newState = Dictionary<'Key, 'T>()
                    let us =
                        ts |> Array.map (fun t ->
                            let u = extract t
                            newState.[underlying.Key u] <- t
                            u)
                    state := newState
                    us
        {
            key = extract >> underlying.Key
            Var = var
            Storage = Storage.InMemory init
            view = var.View.Map Seq.ofArray
        }

    static member FromSeq init =
        ListModel.Create id init

    static member View m =
        m.view

    static member Key m =
        m.key

type ListModel<'Key,'T> with

    member this.Wrap extract wrap update =
        ListModel.Wrap this extract wrap update
