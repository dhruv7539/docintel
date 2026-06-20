namespace DocIntel.Api.Evaluation;

/// <summary>
/// Standard information-retrieval metrics for evaluating ranked search results.
/// All methods take a ranked list of candidate ids (best match first) and the
/// set of ids that are actually relevant for the query.
/// </summary>
public static class RetrievalMetrics
{
    /// <summary>
    /// Recall@k: fraction of the relevant items that appear in the top-k results.
    /// Returns 0 when nothing is relevant.
    /// </summary>
    public static double RecallAtK(IReadOnlyList<string> ranked, ISet<string> relevant, int k)
    {
        if (relevant.Count == 0)
        {
            return 0d;
        }

        var hits = ranked.Take(k).Count(relevant.Contains);
        return (double)hits / relevant.Count;
    }

    /// <summary>
    /// Reciprocal rank: 1 / (position of the first relevant result), or 0 if none
    /// of the relevant items were retrieved. Positions are 1-based.
    /// </summary>
    public static double ReciprocalRank(IReadOnlyList<string> ranked, ISet<string> relevant)
    {
        for (var i = 0; i < ranked.Count; i++)
        {
            if (relevant.Contains(ranked[i]))
            {
                return 1d / (i + 1);
            }
        }

        return 0d;
    }

    /// <summary>Mean of a per-query metric across an evaluation set.</summary>
    public static double Mean(IEnumerable<double> values)
    {
        var list = values as IReadOnlyCollection<double> ?? values.ToList();
        return list.Count == 0 ? 0d : list.Sum() / list.Count;
    }
}
