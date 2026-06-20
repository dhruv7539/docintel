namespace DocIntel.Api.Ai;

public static class VectorMath
{
    /// <summary>
    /// Cosine similarity in [-1, 1]. Returns 0 for empty or mismatched vectors
    /// rather than throwing, so retrieval degrades gracefully.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return 0d;
        }

        double dot = 0d, normA = 0d, normB = 0d;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0d || normB == 0d)
        {
            return 0d;
        }

        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
