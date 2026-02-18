using System;
using System.Collections;

namespace CodeMetrics
{
	[Serializable]
	public class MetricsResult
	{
		public string FileName;
		public DateTime AnalyzedAt;
		public int LinesOfCode;
		public int CommentLines;
		public int BlankLines;
		public double CommentRatio
		{
			get
			{
				if (LinesOfCode > 0)
					return (double)CommentLines / LinesOfCode;
				
				return 0;
			}
		}
		
		public int MethodCount;
		public int MaxCyclomaticComplexity;
		public int TotalComplexity;
		public int MaxNestingDepth;
		public int MaxParameters;
		public double AvgCyclomaticComplexity
		{
			get
			{
				if (MethodCount > 0)
					return (double)TotalComplexity / MethodCount;
				
				return 0;
			}
		}
		
		public ArrayList Methods;
		
		public MetricsResult()
		{
			Methods = new ArrayList();
		}
		
		public override string ToString()
		{
			return string.Format("CC={0} (avg: {1:F2}), LoC={2}, Methods={3}",
			                     MaxCyclomaticComplexity, AvgCyclomaticComplexity, LinesOfCode, MethodCount);
		}
	}
	
	[Serializable]
	public class MethodMetric
	{
		public string Name;
		public int Line;
		public int CyclomaticComplexity;
		public int NestingDepth;
		public int ParameterCount;
	}
}