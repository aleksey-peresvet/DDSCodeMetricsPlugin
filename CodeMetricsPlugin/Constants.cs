using System;

namespace CodeMetrics
{
	/// <summary>
	/// Константы для анализа метрик.
	/// </summary>
	public static class Constants
	{
		/// <summary>
		/// Максимальное рекомендуемое количество строк кода в файле.
		/// </summary>
		public const int MaxLinesOfCode = 500;
		
		/// <summary>
		/// Максимальная рекомендуемая цикломатическая сложность метода.
		/// </summary>
		public const int MaxCyclomaticComplexity = 10;
		
		/// <summary>
		/// Максимальная рекомендуемая вложенность метода.
		/// </summary>
		public const int MaxNestingDepth = 4;
		
		/// <summary>
		/// Максимальное рекомендуемое количество параметров метода.
		/// </summary>
		public const int MaxParameters = 5;
	}
}