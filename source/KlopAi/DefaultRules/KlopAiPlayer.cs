﻿#region Usings

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using KlopAi.algo;
using KlopAi.Extentions;
using KlopIfaces;

#endregion


namespace KlopAi.DefaultRules
{
    /// <summary>
    /// AI player for default rules (must be connected to the base).
    /// </summary>
    public class KlopAiPlayer : KlopAiPlayerBase
    {
        /// <summary>
        /// Gets or sets the turn delay. No delay when null. Can be used for demo purposes.
        /// </summary>
        public TimeSpan? TurnDelay { get; set; }

        /// <summary>
        /// Sets the model. Must be called to activate CPU player.
        /// </summary>
        /// <param name="klopModel">The klop model.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        public override void SetModel(IKlopModel klopModel)
        {
            base.SetModel(klopModel);
            _pathFinder = new KlopPathFinder(klopModel);
        }

        /// <summary>
        /// Makes the turn.
        /// </summary>
        protected override void MakeTurn()
        {
            var path = new List<IKlopCell>();
            _distanceMap = BuildEnemyDistanceMap();
            while (Model.CurrentPlayer == this && Model.Cells.Any(c => c.Available) && !Worker.CancellationPending)
            {
                while (path.Count == 0)
                {
                    int maxPathLength;
                    var target = FindNextTarget(out maxPathLength);

                    if (target == null)
                    {
                        // Game over or we are defeated
                        return;
                    }

                    // Find path FROM target to have correct ordered list
                    path.AddRange(_pathFinder.FindPath(target.X, target.Y, BasePosX, BasePosY, this).Take(maxPathLength));
                }
                var cell = path.First();
                path.Remove(cell);

                if (!cell.Available)
                {
                    // Something went wrong, pathfinder returned unavailable cell. Use simple fallback logic:
                    // This can happen also when base reached. Need to switch strategy.
                    //TODO!!
                    //Debug.Assert(false, "PathFinder returned unavailable cell!");
                    cell = Model.Cells.FirstOrDefault(c => c.Available);
                    path.Clear();
                    if (cell == null) continue;
                }

                DoDelay();
                Model.MakeTurn(cell);
            }
        }

        /// <summary>
        /// Visualizes the distance map.
        /// Just for debugging.
        /// </summary>
        protected string VisualizeDistanceMap(int[,] distanceMap)
        {
            //TODO: Make an extension method; or better - allow on-screen visualization with some overlay.
            var sb = new StringBuilder();
            for (var y = 0; y < Model.FieldHeight; y++)
            {
                for (var x = 0; x < Model.FieldWidth; x++)
                {
                    sb.Append(distanceMap[x, y].ToString().PadRight(4));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// For each cell calculates it's distance from any enemy cell.
        /// </summary>
        private int[,] BuildEnemyDistanceMap()
        {
            var distanceMap = new int[Model.FieldWidth, Model.FieldHeight];
            var totalCellCount = distanceMap.Length;
            var markedCellCount = 0;
            var maxHeat = 0;

            foreach (var cell in Model.Cells)
            {
                if (cell.Owner != null && cell.Owner != this)
                {
                    // Enemy cell default heat = 0;
                    markedCellCount++;
                }
                else
                {
                    // Non-visited cells are marked with -1
                    distanceMap[cell.X, cell.Y] = -1;
                }
            }

            // Then in each pass mark all flagged cells neighbors with flag until we find an enemy.
            while (markedCellCount < totalCellCount)
            {
                maxHeat++;
                var neighborCells = Model.Cells.Where(c => distanceMap[c.X, c.Y] >= 0).SelectMany(c => Model.GetNeighborCells(c)).ToArray();
                foreach (var cell in neighborCells)
                {
                    if (distanceMap[cell.X, cell.Y] >= 0) continue; // Cell already visited. We could use Distinct, but it can be slow.
                    distanceMap[cell.X, cell.Y] = maxHeat;
                    markedCellCount++;
                }
            }

            // Visualize:
            //var s = VisualizeDistanceMap(distanceMap);

            return distanceMap;
        }

        /// <summary>
        /// Delays processing if TurnDelay is not null.
        /// </summary>
        private void DoDelay()
        {
            if (TurnDelay != null)
            {
                Thread.Sleep(TurnDelay.Value);
            }
        }

        private IKlopCell DoFight()
        {
            var enemy = GetPreferredEnemy();

            if (enemy == null)
            {
                // All enemies have been defeated. Game over.
                return Model.Cells.FirstOrDefault(c => c.Owner == null);
            }

            var target = Model[enemy.BasePosX, enemy.BasePosY];
            var importantCell = FindMostImportantCell(BasePosX, BasePosY, target.X, target.Y, enemy);

            //TODO: Find most important reacheble cell!
            // Need to find a set of most important reachable cells, so the sum of their values will be maximum.
            if (importantCell != null /* && importantCell.Item2 > KlopCellEvaluator.TurnEmptyCost*2*/)
            {
                //TODO: FindMostImportantCell should return list of cells, filter it and use.
                target = importantCell.Item1;
            }
            else
            {
                // No good cells to eat - seems like we've been disconnected
                var pathToBase = _pathFinder.FindPath(target.X, target.Y, BasePosX, BasePosY, this);
                // 0) Try to rush to base if there are available cells on the path
                // 1) Seek for available alive enemy
                // 2) Seek for next-to-available alive enemy
                // 3) Seek for any available enemy
                // 4) Seek for any available cell
                target = pathToBase.FirstOrDefault(c => c.Available) ??
                         Model.Cells.FirstOrDefault(c => c.Available && c.Owner != this) ??
                         Model.Cells.FirstOrDefault(c => c.Owner != this && c.State == ECellState.Alive && Model.GetNeighborCells(c).Any(cc => cc.Available)) ??
                         Model.Cells.FirstOrDefault(c => c.Owner != this && c.State == ECellState.Alive) ??
                         Model.Cells.Where(c => c.Available).Random();
            }

            return target;
        }

        private IKlopCell FindEnemyCellToAttack(IKlopCell targetCell = null)
        {
            var nearestEnemyCells = FindNearestEnemyCells(targetCell);
            // There could be several enemy cells with equal distances. Find the one closer to enemy base!
            var pathLengths = nearestEnemyCells.Select(c => new
            {
                c,
                pathLength = _pathFinder.FindPath(c.X, c.Y, c.Owner.BasePosX, c.Owner.BasePosY, this).Count
            });
            return pathLengths.Highest((c1, c2) => c1.pathLength < c2.pathLength).c;
        }


        /// <summary>
        /// Finds the most important cell: cell which most of all affects total path cost.
        /// </summary>
        /// <returns>Tuple of most important cell and path cost difference.</returns>
        private Tuple<IKlopCell, double> FindMostImportantCell(int startX, int startY, int finishX, int finishY, IKlopPlayer klopPlayer)
        {
            var startN = _pathFinder.GetNodeByCoordinates(startX, startY);
            var finishN = _pathFinder.GetNodeByCoordinates(finishX, finishY);
            var initialCost = _pathFinder.FindPath(startN, finishN, klopPlayer, false).Sum(n => n.Cost);
            double maxCost = 0;
            Node resultNode = null;
            foreach (
                var node in
                    Model.Cells.Where(c => c.Available && c.Owner == klopPlayer && c.State == ECellState.Alive).Select(
                        c => _pathFinder.GetNodeByCoordinates(c.X, c.Y)))
            {
                var oldCost = node.Cost;
                node.Cost = KlopCellEvaluator.TurnBlockedCost;

                var cost = _pathFinder.FindPath(startN, finishN, klopPlayer, false, true).Sum(n => n.Cost);
                if (cost > maxCost)
                {
                    maxCost = cost;
                    resultNode = node;
                }

                node.Cost = oldCost;
            }
            return resultNode == null ? null : new Tuple<IKlopCell, double>(Model[resultNode.X, resultNode.Y], maxCost - initialCost);
        }


        /// <summary>
        /// Finds enemy cell(s) which are closest to specified cell, or to any player cell if targetCell is null.
        /// if targetCell is not specified, all player-owned cells are used as targets.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<IKlopCell> FindNearestEnemyCells(IKlopCell targetCell = null)
        {
            if (targetCell == null)
            {
                var availableCellsWithDistances = Model.Cells.Where(c => c.Available).Select(c => new
                {
                    c,
                    d = _distanceMap[c.X, c.Y]
                }).ToArray();
                var minDistance = availableCellsWithDistances.Select(c => c.d).Min();
                // Go over all available cells with the same minimum distance
                foreach (var c in availableCellsWithDistances.Where(c => c.d == minDistance).SelectMany(c => FindNearestEnemyCells(c.c)))
                {
                    yield return c;
                }
                yield break;
            }


            var cell = targetCell;
            var cellDistance = _distanceMap[cell.X, cell.Y];
            if (cellDistance == 0)
            {
                // End of recursion, we have found enemy cell.
                yield return cell;
            }
            else
            {
                foreach (var c in Model.GetNeighborCells(cell).Where(c => _distanceMap[c.X, c.Y] < cellDistance).SelectMany(FindNearestEnemyCells))
                {
                    yield return c;
                }
            }
        }

        /// <summary>
        /// Finds the next target cell. Core thinking method where gaming logic is situated.
        /// </summary>
        private IKlopCell FindNextTarget(out int maxPathLength)
        {
            if (Model.IsFightStarted())
            {
                // Fight started, rush to base
                maxPathLength = 1;
                return DoFight();
            }

            return PrepareOrAttack(out maxPathLength);
        }

        /// <summary>
        /// Gets the distance to the closest enemy cell.
        /// </summary>
        private double GetEnemyDistance(IKlopCell cell)
        {
            return _distanceMap[cell.X, cell.Y];
        }

        /// <summary>
        /// Gets the distance to the nearest enemy cell.
        /// </summary>
        /// <returns></returns>
        private int GetMinEnemyDistance()
        {
            return Model.Cells.Where(c => c.Available).Select(c => _distanceMap[c.X, c.Y]).Min();
        }


        /// <summary>
        /// Gets the enemy player which is most suitable to be attacked by us.
        /// </summary>
        private IKlopPlayer GetPreferredEnemy()
        {
            var enemies = Model.Players.Where(p => p != this).Except(Model.DefeatedPlayers).ToArray();
            var humanPlayers = enemies.Where(p => p.Human).ToArray();

            // Human enemies are preferred (make the game harder)
            if (humanPlayers.Length > 0)
                enemies = humanPlayers;

            if (enemies.Length == 0) return null;
            if (enemies.Length == 1) return enemies[0];
            return GetPreferredEnemy(enemies);
        }

        /// <summary>
        /// Gets the enemy player which is most suitable to be attacked by us.
        /// </summary>
        private IKlopPlayer GetPreferredEnemy(IEnumerable<IKlopPlayer> enemies)
        {
            // Find the enemy with cheapest path to it's base and attack it
            // We can also try to find weakest enemy
            // Or an enemy with closest cells
            // Or an enemy which attacks us most
            return enemies.OrderByDescending(e => Model.Cells.Count(c => c.Owner == e && c.State == ECellState.Alive)).FirstOrDefault();
        }

        /// <summary>
        /// Check whether if enemy is close enough and attacks; in other case generates starting pattern.
        /// </summary>
        private IKlopCell PrepareOrAttack(out int maxPathLength)
        {
            if (GetMinEnemyDistance() < Model.RemainingKlops*AttackThreshold)
            {
                maxPathLength = int.MaxValue;
                return FindEnemyCellToAttack();
            }

            // Fight not started, generate pattern
            maxPathLength = 2; // _model.TurnLength / 3;
            return Model.GenerateStartingPattern(this, GetEnemyDistance);
        }

        private const decimal AttackThreshold = 0.4M;
        private int[,] _distanceMap;
        private KlopPathFinder _pathFinder;
    }
}