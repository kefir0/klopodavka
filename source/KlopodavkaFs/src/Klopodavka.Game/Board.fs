module Klopodavka.Game.Board
open Klopodavka.Game

[<Literal>]
let DefaultWidth = 34

[<Literal>]
let DefaultHeight = 40

[<Literal>]
let BaseOffset = 5

let tiles (Tiles board) = board

let tile board x y = (tiles board).[x, y]

let size board =
    let arr = tiles board
    (arr.GetLength(0), arr.GetLength(1))

let basePosition player width height =
    match player with
        | Red -> (width - BaseOffset, BaseOffset)
        | Blue -> (BaseOffset, height - BaseOffset)

let createBoard =
    let arr = Array2D.create DefaultWidth DefaultHeight Empty

    let redX, redY = basePosition Red DefaultWidth DefaultHeight
    arr.[redX, redY] <- Base Red

    let blueX, blueY = basePosition Blue DefaultWidth DefaultHeight
    arr.[blueX, blueY] <- Base Blue

    arr |> Tiles

let neighbors board x y =
    let offs = [ -1; 0; 1 ]
    let w, h = size board
    Seq.allPairs offs offs
    |> Seq.except [ (0, 0) ]
    |> Seq.map (fun (a, b) -> (x + a, y + b))
    |> Seq.where (fun (a, b) -> a >= 0 && b >= 0 && a < w && b < h)

let moves board player =
    let w, h = size board
    let bx, by = basePosition player w h
    let visited = Array2D.create w h false

    let rec loop (tiles: seq<int * int>) = seq {
        for pos in tiles do
            let x, y = pos
            match visited.[x, y] with
            | true -> ()
            | false ->
                visited.[x, y] <- true
                let t = tile board x y
                let loopNeighbors () = loop (neighbors board x y)

                match t with
                | Empty -> yield pos
                | Alive p when p = player -> yield! loopNeighbors()
                | Squashed p when p = player -> yield! loopNeighbors()
                | _ -> ()

    }

    loop [bx, by]

