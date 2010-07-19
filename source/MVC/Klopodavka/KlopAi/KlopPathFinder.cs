﻿using System;
using System.Collections.Generic;
using System.Linq;
using KlopAi.algo;
using KlopIfaces;
using KlopModel;

namespace KlopAi
{
   public class KlopPathFinder
   {
      #region Fields and Constants

      public const int TurnBlockedCost = int.MaxValue; // Цена хода в запрещенную клетку (->inf)
      public const int TurnEatCost = 35; // Цена хода в занятую клетку
      public const int TurnEatEnemyBaseCost = 8; // Цена съедания клопа около вражеской базы
      public const int TurnEatOwnbaseCost = 5; // Цена съедания клопа около своей базы
      public const int TurnEmptyCost = 100; // Цена хода в пустую клетку
      public const int TurnNearBaseCost = 11000; // Цена хода около своей базы
      public const int TurnNearEnemyEmptyCost = 140; // Цена хода в пустую клетку рядом с врагом
      private readonly Node[,] field;
      private readonly IKlopModel klopModel;
      private AStar _aStar;

      #endregion

      #region Constructors

      /// <summary>
      /// Initializes a new instance of the <see cref="KlopPathFinder"/> class.
      /// </summary>
      /// <param name="model">The model.</param>
      /// <param name="player">The player to find path for.</param>
      public KlopPathFinder(IKlopModel model)
      {
         klopModel = model;
         field = new Node[model.FieldWidth,model.FieldHeight];
         foreach (IKlopCell cell in klopModel.Cells)
         {
            field[cell.X, cell.Y] = new Node(cell.X, cell.Y);
         }
      }

      #endregion

      #region Public methods

      /// <summary>
      /// Finds the path betweed specified nodes for specified player.
      /// </summary>
      public List<IKlopCell> FindPath(int startX, int startY, int finishX, int finishY, IKlopPlayer klopPlayer)
      {
         return FindPath(startX, startY, finishX, finishY, klopPlayer, false);
      }

      /// <summary>
      /// Finds the path betweed specified nodes for specified player.
      /// </summary>
      public List<IKlopCell> FindPath(int startX, int startY, int finishX, int finishY, IKlopPlayer klopPlayer, bool inverted)
      {
         // Init field
         foreach (IKlopCell cell in klopModel.Cells)
         {
            var f = field[cell.X, cell.Y];
            f.Reset();
            f.Cost = GetCellCost(cell, klopPlayer);
            if (f.Cost != TurnEmptyCost)
            {
               ((KlopCell) cell).Tag = f.Cost;
            }
         }

         // Get result
         var lastNode = PathFinder.FindPath(GetNodeByCoordinates(startX, startY), GetNodeByCoordinates(finishX, finishY), GetDistance, GetNodeByCoordinates, inverted);
         var result = new List<IKlopCell>();
         while (lastNode != null)
         {
            var node = klopModel[lastNode.X, lastNode.Y];
            lastNode = lastNode.Parent;

            if (node.Owner == klopPlayer) continue;
            result.Add(node);
         }
         return result;
      }

      /// <summary>
      /// Gets the distance between two nodes.
      /// </summary>
      public static double GetDistance(Node n1, Node n2)
      {
         return GetDistance(n1.X, n1.Y, n2.X, n2.Y);
      }

      /// <summary>
      /// Gets the distance between two nodes.
      /// </summary>
      public static double GetDistance(IKlopCell cell1, IKlopCell cell2)
      {
         return GetDistance(cell1.X, cell1.Y, cell2.X, cell2.Y);
      }

      /// <summary>
      /// Gets the distance between two nodes.
      /// </summary>
      public static double GetDistance(int x1, int y1, int x2, int y2)
      {
         var dx = x1 - x2;
         var dy = y1 - y2;

         // Diagonal turn should cost 1
         //return Math.Max(Math.Abs(dx), Math.Abs(dy)); 

         // Use Sqrt for more natural-looking paths
         return Math.Sqrt(dx*dx + dy*dy);
      }

      #endregion

      #region Private and protected properties and indexers

      private AStar PathFinder
      {
         get { return _aStar ?? (_aStar = new AStar()); }
      }

      #endregion

      #region Private and protected methods

      private double GetCellCost(IKlopCell cell, IKlopPlayer klopPlayer)
      {
         if (cell.Owner == klopPlayer)
         {
            return 0; // Zero cost for owned cell
         }

         if (cell.State == ECellState.Dead)
         {
            return TurnBlockedCost; // Can't move into own dead cell or base cell
         }

         if (cell.Owner != null && cell.State == ECellState.Alive)
         {
            if (IsCellNearBase(cell, klopPlayer))
            {
               return TurnEatOwnbaseCost;
            }

            if (klopModel.Players.Where(p => p != klopPlayer).Any(enemy => IsCellNearBase(cell, enemy)))
            {
               return TurnEatEnemyBaseCost;
            }

            return TurnEatCost;
         }

         if (IsCellNearBase(cell, klopPlayer))
         {
            return TurnNearBaseCost;
         }

         if (klopModel.GetNeighborCells(cell).Any(c => c.Owner != null && c.Owner != klopPlayer))
         {
            return TurnNearEnemyEmptyCost; // Turn near enemy klop costs a bit more.
         }

         var neighborCount = klopModel.GetNeighborCells(cell).Sum(c => c.Owner == null ? 0 : 10 / GetDistance(c, cell));

         return TurnEmptyCost + neighborCount; // Default - turn into empty cell.
      }

      private static bool IsCellNearBase(IKlopCell cell, IKlopPlayer baseOwner)
      {
         return Math.Max(Math.Abs(cell.X - baseOwner.BasePosX), Math.Abs(cell.Y - baseOwner.BasePosY)) == 1;
      }

      /// <summary>
      /// Gets the node by coordinates.
      /// </summary>
      /// <param name="x">The x.</param>
      /// <param name="y">The y.</param>
      /// <returns></returns>
      private Node GetNodeByCoordinates(int x, int y)
      {
         if ((x >= 0) && (x < field.GetLength(0)) && (y >= 0) && (y < field.GetLength(1)))
            return field[x, y];
         return null;
      }

      #endregion
   }
}