<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Numerics.dll</Reference>
  <Namespace>System.Numerics</Namespace>
</Query>

// Program that tries to estimate the total number of nodes by analyzing the
// proxmity group of a number of nodes (1, 2, 4, 9, 20 or 41 nodes for each tested configuration).
// Potential candidate metrics:
// - average distance
// - min distance
// - max distance
// - standard deviation
// Max distance is the metric that, when divided by the proximity group size,
// seems to have the best average and the lowest standard deviation.
// (and min distance is the worst metric).
// Then I discovered empirically that the following formula gives an approximation
// of the the total number of nodes:
// max_value / (max_distance / proximity_count)
// Intuitivelly:
// - max_value is the radius of the network universe
// - the proximity group is a small part of this universe:
//   * centered on the node
//   * whose radius is max_distance
// - the universe having a homogeneous density, the number of nodes is proportional to the radius
//   (well, that's the hard part to swallow: this universe is not in 3D but linear: the addresses
//   are points on a line, or maybe a circle)

// The approximation seems to be improved when a fraction of the measured standard deviation is subtracted
// from the max distance (for example: mean max distance - 0.3 * standard deviation

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
	int groupSize = 8;

	int minNodes = 1000;
	int maxNodes = 100000;
	int stepNodes = 7;

	int minSamples = 12;
	int maxSamples = 48;
	int stepSamples = 4;

	// One supplementary byte to handle a max value with all bits set to 1
	BigInteger maxValue = new BigInteger(Enumerable.Range(0, byteSize + 1).Select(i => (byte)(i == byteSize ? 0 : 0xFF)).ToArray());

	// Generates maxNodes random nodes. This is the max configuration, other configurations use a subset of these nodes.
	Random rand = new Random();
	BigInteger[] nodes = Enumerable.Range(0, maxNodes).Select(_ =>
	{
		// One supplementary byte to generate positive numbers
		byte[] bytes = new byte[byteSize + 1];
		rand.NextBytes(bytes);
		bytes[byteSize] = 0;
		return new BigInteger(bytes);
	}).ToArray();

	// Summary results
	double[,] errorPercentageArray = new double[stepNodes,stepSamples];

	// From minSamples to maxSamples measuring nodes
	int j = 0;
	foreach (int sampleCount in GenerateLinearSamples(minSamples, maxSamples, stepSamples))
	{
		// Configurations having from minNodes to maxNodes nodes
		int i = 0;
		GenerateLogSamples(minNodes, maxNodes, stepNodes).Where(nodeCount => nodeCount >= sampleCount).Select(nodeCount =>
		{
			double[] groups = nodes.Take(sampleCount).Select(n1 =>
				{
					BigInteger max = nodes.Where(n2 => n1 != n2)
						.Take(nodeCount - 1).Select(n2 => n1 ^ n2).OrderBy(d => d)
						.ElementAt(groupSize - 1);
					return (double)(maxValue / (max / groupSize));
				})
				.ToArray();
			double estimatedNodeCount = groups.Aggregate(0.0, (acc, d) => acc + d) / sampleCount;
			double variance = groups.Aggregate(0.0, (acc, d) => acc + (d - estimatedNodeCount) * (d - estimatedNodeCount)) / sampleCount;
			double standardDeviation = Math.Sqrt(variance);
			double errorPercentage = ((estimatedNodeCount - nodeCount) * 100 / nodeCount);
			double estimatedNodeCount2 = estimatedNodeCount - 0.3 * standardDeviation;
			double errorPercentage2 = ((estimatedNodeCount2 - nodeCount) * 100 / nodeCount);
			errorPercentageArray[i, j] = errorPercentage2;
			i++;
			return new
			{
				RealNodeCount = nodeCount.ToString("N0").PadLeft(7),
				EstimatedNodeCount = estimatedNodeCount.ToString("N0").PadLeft(7),
				ErrorPercentage = errorPercentage.ToString("N2").PadLeft(6),
				StandardDeviation = standardDeviation.ToString("N0").PadLeft(6),
				EstimatedNodeCount2 = estimatedNodeCount2.ToString("N0").PadLeft(7),
				ErrorPercentage2 = errorPercentage2.ToString("N2").PadLeft(6),
			};
		}).Dump(string.Format("BitSize: {0}, GroupSize: {1}, SampleCount: {2}",
			byteSize * 8, groupSize, sampleCount));
		j++;
	}

	Console.Write("| Network ");
	foreach (int sampleCount in GenerateLinearSamples(minSamples, maxSamples, stepSamples))
		Console.Write(string.Format("|{0} nodes ", sampleCount.ToString("D").PadLeft(3)));
	Console.WriteLine("|");
	Console.Write("|---------");
	foreach (int sampleCount in GenerateLinearSamples(minSamples, maxSamples, stepSamples))
		Console.Write(string.Format("|----------", sampleCount.ToString("D").PadLeft(3)));
	Console.WriteLine("|");
	int k = 0;
	foreach (int nodeCount in GenerateLogSamples(minNodes, maxNodes, stepNodes))
	{
		Console.Write(string.Format("|{0} ", nodeCount.ToString("N0").PadLeft(8)));
		for (int i = 0; i < stepSamples; i++)
			Console.Write(string.Format("|{0} % ", errorPercentageArray[k, i].ToString("N2").PadLeft(7)));
		Console.WriteLine("|");
		k++;
	}
}
