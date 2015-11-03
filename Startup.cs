using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Http;
using Microsoft.Framework.Logging;

namespace SudokuGenerator
{
    public class Startup
    {
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole();

            app.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                var json = SudokuPuzzle.RandomGrid(9).ToString();
                await context.Response.WriteAsync(json);
            });
        }
    }

    internal sealed class SudokuPuzzle
    {
        private static readonly Dictionary<Tuple<int, int>, int[]> SavedPeers = new Dictionary<Tuple<int, int>, int[]>();
        private static readonly Random Random = new Random();
        public static string[][] String;

        public int[][] Cells;
        public int Length;

        public SudokuPuzzle(int n)
        {
            Cells = Enumerable.Repeat(Enumerable.Range(1, n).ToArray(), n*n).ToArray();
            String = Enumerable.Repeat(Enumerable.Range(1, n).Select(c => c.ToString()).ToArray(), n*n).ToArray();
            Length = n;
        }

        private int BoxSize => (int) Math.Sqrt(Length);

        public object Clone()
        {
            var clone = new SudokuPuzzle(Length) {Cells = new int[Cells.Length][]};

            for (var i = 0; i < Cells.Length; i++)
            {
                clone.Cells[i] = new int[Cells[i].Length];
                Buffer.BlockCopy(Cells[i], 0, clone.Cells[i], 0, Buffer.ByteLength(Cells[i]));
            }
            return clone;
        }

        private bool IsPeer(int c1, int c2)
        {
            return (c1/Length == c2/Length
                    || c1%Length == c2%Length
                    || (c1/Length/BoxSize == c2/Length/BoxSize && c1%Length/BoxSize == c2%Length/BoxSize))
                   && c1 != c2;
        }

        private int[] Peers(int cell)
        {
            var key = new Tuple<int, int>(Length, cell);
            if (!SavedPeers.ContainsKey(key))
                SavedPeers.Add(key, Enumerable.Range(0, Length*Length).Where(c => IsPeer(cell, c)).ToArray());

            return SavedPeers[key];
        }

        private SudokuPuzzle ApplyConstraints(int cellIndex, int value)
        {
            var puzzle = (SudokuPuzzle) Clone();

            puzzle.Cells[cellIndex] = new[] {value};

            foreach (var peerIndex in puzzle.Peers(cellIndex))
            {
                var newPeers = puzzle.Cells[peerIndex].Except(new[] {value}).ToArray();
                if (!newPeers.Any())
                    return null;

                puzzle.Cells[peerIndex] = newPeers;
            }
            return puzzle;
        }

        private static List<int> FindSingularizedCells(SudokuPuzzle puzzle1, SudokuPuzzle puzzle2, int cellIndex)
        {
            return
                puzzle1.Peers(cellIndex)
                    .Where(i => puzzle1.Cells[i].Length > 1 && puzzle2.Cells[i].Length == 1)
                    .ToList();
        }

        private SudokuPuzzle PlaceValue(int cellIndex, int value)
        {
            if (!Cells[cellIndex].Contains(value))
                return null;

            var puzzle = ApplyConstraints(cellIndex, value);
            if (puzzle == null)
                return null;

            return
                FindSingularizedCells(this, puzzle, cellIndex)
                    .Any(i => (puzzle = puzzle.PlaceValue(i, puzzle.Cells[i].Single())) == null)
                    ? null
                    : puzzle;
        }

        private int FindWorkingCell()
        {
            var minCandidates = Cells.Where(cands => cands.Length >= 2).Min(cands => cands.Length);
            return Array.FindIndex(Cells, c => c.Length == minCandidates);
        }

        private static List<SudokuPuzzle> MultiSolve(SudokuPuzzle input, int maximumSolutions = -1)
        {
            var solutions = new List<SudokuPuzzle>();
            input.Solve(p =>
            {
                solutions.Add(p);
                return solutions.Count < maximumSolutions || maximumSolutions == -1;
            });
            return solutions;
        }

        private SudokuPuzzle Solve(Func<SudokuPuzzle, bool> solutionFunc = null)
        {
            if (Cells.All(cell => cell.Length == 1))
                return solutionFunc != null && solutionFunc(this) ? null : this;

            var activeCell = FindWorkingCell();
            foreach (var guess in Cells[activeCell])
            {
                SudokuPuzzle puzzle;
                if ((puzzle = PlaceValue(activeCell, guess)) == null) continue;
                if ((puzzle = puzzle.Solve(solutionFunc)) != null)
                    return puzzle;
            }
            return null;
        }

        public static SudokuPuzzle RandomGrid(int size)
        {
            var puzzle = new SudokuPuzzle(size);

            while (true)
            {
                var unsolvedCellIndexes = puzzle.Cells
                    .Select((cands, index) => new {cands, index})
                    .Where(t => t.cands.Length >= 2)
                    .Select(u => u.index)
                    .ToArray();

                var cellIndex = unsolvedCellIndexes[Random.Next(unsolvedCellIndexes.Length)];
                var candidateValue = puzzle.Cells[cellIndex][Random.Next(puzzle.Cells[cellIndex].Length)];

                var workingPuzzle = puzzle.PlaceValue(cellIndex, candidateValue);
                if (workingPuzzle == null) continue;
                var solutions = MultiSolve(workingPuzzle, 2);
                switch (solutions.Count)
                {
                    case 0:
                        continue;
                    case 1:
                        return solutions.Single();
                    default:
                        puzzle = workingPuzzle;
                        break;
                }
            }
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            var numberOfSquaresToFill = 20;
            var difficulty = numberOfSquaresToFill / 81.0;
            for (int i = 0; i < 81; i++)
            {
                if (numberOfSquaresToFill == 0) break;
                if (!(Random.NextDouble() < difficulty)) continue;
                Cells[i][0] = -1;
                numberOfSquaresToFill--;
            }

            Cells.Select((cands, index) => new { cands, index }).ToList().ForEach(a =>
            {
                if (a.index % Length == 0) result.Append("[");
                result.Append("\"" + (a.cands[0] == -1 ? "?" : a.cands[0].ToString()) + "\",");
                if (a.index % Length == Length - 1 && a.index <= 72) result.AppendLine("],");
                if (a.index % Length == Length - 1 && a.index >= 72) result.AppendLine("]");
            });
            var r = result.ToString().Replace(",]", "]");

            return "{ \"puzzle\" : \n[\n" + r + "\n]\n}";
        }
    }
}