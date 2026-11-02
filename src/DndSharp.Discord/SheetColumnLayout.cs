namespace DndSharp.Discord;

/// <summary>Finds the shortest three-column arrangement with the stats panel fixed in column zero.</summary>
internal static class SheetColumnLayout
{
    public static int[] Arrange(IReadOnlyList<int> heights, int statsHeight)
    {
        if (statsHeight < 0 || heights.Any(height => height < 0))
            throw new ArgumentOutOfRangeException(nameof(heights));
        var ordered = heights.Select((height, index) => (Height: (long)height, Index: index))
            .OrderByDescending(item => item.Height).ThenBy(item => item.Index).ToArray();
        long[] columns = [statsHeight, 0, 0];
        var best = new int[heights.Count];

        // A greedy placement provides an initial upper bound; the search below
        // improves it to the true minimum, rather than assuming greedy is optimal.
        foreach (var item in ordered)
        {
            var column = Array.IndexOf(columns, columns.Min());
            best[item.Index] = column;
            columns[column] += item.Height;
        }
        var bestHeight = columns.Max();
        var lowerBound = Math.Max((statsHeight + heights.Sum(height => (long)height) + 2) / 3, statsHeight);
        Array.Copy(new long[] { statsHeight, 0, 0 }, columns, 3);
        var current = new int[heights.Count];

        void Search(int next)
        {
            if (bestHeight == lowerBound)
                return;
            if (next == ordered.Length)
            {
                var height = columns.Max();
                if (height < bestHeight)
                {
                    bestHeight = height;
                    Array.Copy(current, best, current.Length);
                }
                return;
            }
            if (Math.Max(columns.Max(), columns.Min() + ordered[next].Height) >= bestHeight)
                return;

            // Columns with the same current height are interchangeable for this item.
            var triedHeights = new HashSet<long>();
            foreach (var column in Enumerable.Range(0, 3).OrderBy(i => columns[i]).ThenBy(i => i))
            {
                if (!triedHeights.Add(columns[column]))
                    continue;
                var item = ordered[next];
                if (columns[column] + item.Height >= bestHeight)
                    continue;
                columns[column] += item.Height;
                current[item.Index] = column;
                Search(next + 1);
                columns[column] -= item.Height;
            }
        }

        Search(0);
        return best;
    }
}
