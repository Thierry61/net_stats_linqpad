<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Numerics.dll</Reference>
  <Namespace>System.Numerics</Namespace>
</Query>

IEnumerable<int> GenerateLogSamples(int min, int max, int numbers)
{
	if (min == max || numbers < 2)
		return Enumerable.Range(min, 1);
	double lnMin = Math.Log(min);
	double lnMax = Math.Log(max);
	double lnStep = (lnMax - lnMin) / (numbers - 1);
	return Enumerable.Range(0, numbers).Select(n => (int)Math.Round(Math.Exp(lnMin + n * lnStep)));
}

IEnumerable<int> GenerateLinearSamples(int min, int max, int numbers)
{
	if (min == max || numbers < 2)
		return Enumerable.Range(min, 1);
	double step = (double)(max - min) / (numbers - 1);
	return Enumerable.Range(0, numbers).Select(n => (int)Math.Round(min + n * step));
}

void Main()
{
	int byteSize = 64;
	int chunksDuplication = 4;
	// TODO: prendre en compte un nombre maximum de chunks par noeud

	int minNodes = 1000;
	int maxNodes = 5000;
	int stepNodes = 3;

	int minChunks = 50000;
	int maxChunks = 50000;
	int stepChunks = 1;

	// sampleCount measuring nodes
	int sampleCount = 20;

	// Generates maxNodes random nodes. This is the max configuration, other configurations use a subset of these nodes.
	Random rand = new Random();
	Dictionary<BigInteger, int> invNodes = new Dictionary<BigInteger, int>();
	BigInteger[] nodes = Enumerable.Range(0, maxNodes).Select(i =>
	{
		// One supplementary byte to generate positive numbers
		byte[] bytes = new byte[byteSize + 1];
		rand.NextBytes(bytes);
		bytes[byteSize] = 0;
		BigInteger res = new BigInteger(bytes);
		invNodes[res] = i;
		return res;
	}).ToArray();

	// Generates maxChunks random chunks. This is the max configuration, other configurations use a subset of these chunks.
	BigInteger[] chunks = Enumerable.Range(0, maxChunks).Select(_ =>
	{
		// One supplementary byte to generate positive numbers
		byte[] bytes = new byte[byteSize + 1];
		rand.NextBytes(bytes);
		bytes[byteSize] = 0;
		return new BigInteger(bytes);
    }).ToArray();

	// Each chunk has a list of nodes ordered by distance.
	// First level index is index in chunks
	// Second level data is index in nodes
#if false	// Too memory and CPU intensive
	int[][] nodesByChunks = chunks.Select(chunk =>
	{
		int i = 0;
		return nodes.Select(node => new { i = i++, distance = node ^ chunk })
			.OrderBy(e => e.distance).Take(chunksDuplication*sampleCount).Select(e => e.i).ToArray();
	}).ToArray();
#endif
	int[][] nodesByChunks = new int[maxChunks][];
	BigInteger[] tmpNodes = new BigInteger[maxNodes];
	int[] tmpIndexes = new int[maxNodes];
	for (int ichunk = 0; ichunk < maxChunks; ichunk++)
	{
		for (int inode = 0; inode < maxNodes; inode++)
		{
			tmpNodes[inode] = nodes[inode] ^ chunks[ichunk];
			tmpIndexes [inode] = inode;
		}
		Array.Sort(tmpNodes, tmpIndexes);
		int[] indexes = new int[chunksDuplication*sampleCount];
		Array.Copy(tmpIndexes, indexes, indexes.Length);
		nodesByChunks[ichunk] = indexes;
	}

	// Configurations having from minNodes to maxNodes nodes
	foreach (int nodeCount in GenerateLogSamples(minNodes, maxNodes, stepNodes))
	{
		GenerateLogSamples(minChunks, maxChunks, stepChunks).Select(chunkCount =>
		{
			var chunksInSampleNodes = nodesByChunks.Take(chunkCount)
				.Select(c => c.Where(i => i < nodeCount).Take(chunksDuplication))
				.SelectMany(i => i)
				.Where(i => i < sampleCount)
				.GroupBy(i => i)
				.ToDictionary(g => g.Key, g => g.Count());
			double estimatedChunkCount = ((double) chunksInSampleNodes.Sum(kvp => kvp.Value))
				/ chunksInSampleNodes.Count() * nodeCount / chunksDuplication;
			double errorPercentage = ((estimatedChunkCount - chunkCount) * 100 / chunkCount);
			return new
			{
				RealChunkCount = chunkCount.ToString("N0").PadLeft(7),
				EstimatedChunkCount = estimatedChunkCount.ToString("N0").PadLeft(7),
				ErrorPercentage = errorPercentage.ToString("N2").PadLeft(6),
			};
		}).Dump(string.Format("BitSize: {0}, Chunks Duplication: {1}, Sample Count: {2}, Nodes: {3}",
	 		byteSize * 8, chunksDuplication, sampleCount, nodeCount));
	}
}

// Define other methods and classes here