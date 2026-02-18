using ICSharpCode.Core;
using ICSharpCode.NRefactory;
using ICSharpCode.NRefactory.Ast;
using ICSharpCode.NRefactory.Parser;
using ICSharpCode.NRefactory.Visitors;
using ICSharpCode.SharpDevelop;
using System;
using System.IO;
using System.Collections;

namespace CodeMetrics
{
	public static class MetricsAnalyzer
	{
		private static readonly MetricsResult emptyResult = new MetricsResult();
		private static MetricsResult lastResult;
		
		/// <summary>
		/// Проанализировать текущий файл *.cs.
		/// </summary>
		/// <param name="fileName">Файл *.cs.</param>
		/// <returns>Метрики кода.</returns>
		public static MetricsResult AnalyzeFile(string fileName)
		{
			try
			{
				if (!File.Exists(fileName))
					return emptyResult;
				
				string content = File.ReadAllText(fileName);
				StringReader reader = new StringReader(content);
				IParser parser = ParserFactory.CreateParser(SupportedLanguage.CSharp, reader);
				parser.Parse();
				
				if (parser.Errors.Count > 0)
					return emptyResult;
				
				MetricsResult metrics = new MetricsResult();
				metrics.FileName = fileName;
				metrics.AnalyzedAt = DateTime.Now;
				
				string[] lines = content.Split('\n');
				int locCount = 0;
				int blankCount = 0;
				
				for (int i = 0; i < lines.Length; i++)
				{
					string line = lines[i];
					
					if (string.IsNullOrWhiteSpace(line))
						blankCount++;
					else if (!line.TrimStart().StartsWith("//"))
						locCount++;
				}
				
				metrics.LinesOfCode = locCount;
				metrics.BlankLines = blankCount;
				metrics.CommentLines = CountComments(content);
				
				MethodMetricsVisitor visitor = new MethodMetricsVisitor();
				parser.CompilationUnit.AcceptVisitor(visitor, null);
				
				metrics.MethodCount = visitor.Methods.Count;
				metrics.Methods = visitor.Methods;
				
				int totalComplexity = 0;
				int maxComplexity = 0;
				int maxNesting = 0;
				int maxParams = 0;
				
				for (int i = 0; i < visitor.Methods.Count; i++)
				{
					MethodMetric method = (MethodMetric)visitor.Methods[i];
					totalComplexity += method.CyclomaticComplexity;
					
					if (method.CyclomaticComplexity > maxComplexity)
						maxComplexity = method.CyclomaticComplexity;
					
					if (method.NestingDepth > maxNesting)
						maxNesting = method.NestingDepth;
					
					if (method.ParameterCount > maxParams)
						maxParams = method.ParameterCount;
				}
				
				metrics.TotalComplexity = totalComplexity;
				metrics.MaxCyclomaticComplexity = maxComplexity;
				metrics.MaxNestingDepth = maxNesting;
				metrics.MaxParameters = maxParams;
				
				lastResult = metrics;
				return metrics;
			}
			catch (Exception ex)
			{
				LoggingService.Error("CodeMetrics analysis failed", ex);
				return emptyResult;
			}
		}
		
		/// <summary>
		/// Получить последний результат анализа файла.
		/// </summary>
		/// <returns>Последний результат анализа файла.</returns>
		public static MetricsResult GetLastResult()
		{
			if (lastResult == null)
				return emptyResult;
			
			return lastResult;
		}
		
		/// <summary>
		/// Подсчитать комментарии.
		/// </summary>
		/// <param name="content">Текст файла.</param>
		/// <returns>Количество комментариев.</returns>
		private static int CountComments(string content)
		{
			int count = 0;
			bool inBlockComment = false;
			string[] lines = content.Split('\n');
			
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				string trimmed = line.Trim();
				
				if (inBlockComment)
				{
					count++;
					if (trimmed.Contains("*/"))
						inBlockComment = false;
					
					continue;
				}
				
				if (trimmed.StartsWith("/*"))
				{
					count++;
					inBlockComment = !trimmed.Contains("*/");
					
					continue;
				}
				
				if (trimmed.StartsWith("//") || trimmed.StartsWith("///"))
				{
					count++;
					continue;
				}
				
				int singleLineCommentIndex = line.IndexOf("//");
				if (singleLineCommentIndex > 0 && !line.Substring(0, singleLineCommentIndex).Trim().EndsWith("\""))
				{
					count++;
				}
			}
			
			return count;
		}
	}
	
	public class MethodMetricsVisitor : AbstractAstVisitor
	{
		public ArrayList Methods;
		private int currentBlockDepth;
		private int maxBlockDepth;
		private int methodComplexity;
		private MethodMetric currentMethod;
		
		public MethodMetricsVisitor()
		{
			Methods = new ArrayList();
			currentBlockDepth = 0;
			maxBlockDepth = 0;
			methodComplexity = 1;
			currentMethod = null;
		}
		
		/// <summary>
		/// Обработка метода.
		/// </summary>
		/// <param name="methodDeclaration">Описание метода.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitMethodDeclaration(MethodDeclaration methodDeclaration, object data)
		{
			// Сохраняем состояние для вложенных методов (делегаты, анонимные методы)
			int savedDepth = currentBlockDepth;
			int savedMaxDepth = maxBlockDepth;
			int savedComplexity = methodComplexity;
			
			// Инициализация для нового метода
			currentMethod = new MethodMetric();
			currentMethod.Name = methodDeclaration.Name;
			currentMethod.Line = methodDeclaration.StartLocation.Line;
			currentMethod.ParameterCount = methodDeclaration.Parameters.Count;
			
			currentBlockDepth = 0;
			maxBlockDepth = 0;
			methodComplexity = 1;
			
			// Обход тела метода
			if (methodDeclaration.Body != null)
				methodDeclaration.Body.AcceptVisitor(this, data);
			
			// Сохранение результатов
			currentMethod.NestingDepth = maxBlockDepth;
			currentMethod.CyclomaticComplexity = methodComplexity;
			Methods.Add(currentMethod);
			currentMethod = null;
			
			// Восстановление состояния
			currentBlockDepth = savedDepth;
			maxBlockDepth = savedMaxDepth;
			methodComplexity = savedComplexity;
			
			return null;
		}
		
		/// <summary>
		/// Обработка блока кода.
		/// </summary>
		/// <param name="blockStatement">Объявление блока кода.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitBlockStatement(BlockStatement blockStatement, object data)
		{
			// Увеличиваем глубину при входе в блок {}
			currentBlockDepth++;
			
			// Обновляем максимальную глубину
			if (currentBlockDepth > maxBlockDepth)
				maxBlockDepth = currentBlockDepth;
			
			// Ручной обход дочерних элементов (без вызова base для контроля)
			for (int i = 0; i < blockStatement.Children.Count; i++)
			{
				object child = blockStatement.Children[i];
				if (child is Statement)
					((Statement)child).AcceptVisitor(this, data);
			}
			
			// Уменьшаем глубину при выходе из блока
			currentBlockDepth--;
			
			return null;
		}
		
		/// <summary>
		/// Обработка условий.
		/// </summary>
		/// <param name="ifStatement">Объявление условия.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitIfElseStatement(IfElseStatement ifStatement, object data)
		{
			methodComplexity++;
			return base.VisitIfElseStatement(ifStatement, data);
		}
		
		/// <summary>
		/// Обработка циклов while.
		/// </summary>
		/// <param name="doLoopStatement">Объявление цикла while.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitDoLoopStatement(DoLoopStatement doLoopStatement, object data)
		{
			methodComplexity++;
			return base.VisitDoLoopStatement(doLoopStatement, data);
		}
		
		/// <summary>
		/// Обработка циклов for
		/// </summary>
		/// <param name="forStatement">Объявление цикла for.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitForStatement(ForStatement forStatement, object data)
		{
			methodComplexity++;
			return base.VisitForStatement(forStatement, data);
		}
		
		/// <summary>
		/// Обработка циклов foreach.
		/// </summary>
		/// <param name="foreachStatement">Объявление цикла foreach.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitForeachStatement(ForeachStatement foreachStatement, object data)
		{
			methodComplexity++;
			return base.VisitForeachStatement(foreachStatement, data);
		}
		
		/// <summary>
		/// Обработка switch.
		/// </summary>
		/// <param name="switchStatement">Объявление блока switch.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitSwitchStatement(SwitchStatement switchStatement, object data)
		{
			// за сам оператор switch
			methodComplexity++;
			
			for (int i = 0; i < switchStatement.SwitchSections.Count; i++)
			{
				SwitchSection section = (SwitchSection)switchStatement.SwitchSections[i];
				
				// Каждый case (кроме default) добавляет 1 к сложности
				if (!section.SwitchLabels[0].ToString().Contains("default"))
					methodComplexity++;
				
				for (int j = 0; j < section.Children.Count; j++)
				{
					Statement stmt = (Statement)section.Children[j];
					stmt.AcceptVisitor(this, data);
				}
			}
			
			return null;
		}
		
		/// <summary>
		/// Обработка исключений.
		/// </summary>
		/// <param name="tryCatchStatement">Объявление блока try-catch.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitTryCatchStatement(TryCatchStatement tryCatchStatement, object data)
		{
			for (int i = 0; i < tryCatchStatement.CatchClauses.Count; i++)
				methodComplexity++;
			
			if (tryCatchStatement.StatementBlock != null)
				tryCatchStatement.StatementBlock.AcceptVisitor(this, data);
			
			for (int i = 0; i < tryCatchStatement.CatchClauses.Count; i++)
			{
				CatchClause clause = (CatchClause)tryCatchStatement.CatchClauses[i];
				if (clause.StatementBlock != null)
					clause.StatementBlock.AcceptVisitor(this, data);
			}
			
			if (tryCatchStatement.FinallyBlock != null)
				tryCatchStatement.FinallyBlock.AcceptVisitor(this, data);
			
			return null;
		}
		
		/// <summary>
		/// Обработка логических операторов в условиях (&&, ||).
		/// </summary>
		/// <param name="binaryOperatorExpression">Выражение бинарной операции.</param>
		/// <param name="data">Объект для возврата результата.</param>
		/// <returns>Объект для возврата результата.</returns>
		public override object VisitBinaryOperatorExpression(BinaryOperatorExpression binaryOperatorExpression, object data)
		{
			if (binaryOperatorExpression.Op == BinaryOperatorType.LogicalAnd ||
			    binaryOperatorExpression.Op == BinaryOperatorType.LogicalOr)
			{
				methodComplexity++;
			}
			
			if (binaryOperatorExpression.Left != null)
				binaryOperatorExpression.Left.AcceptVisitor(this, data);
			
			if (binaryOperatorExpression.Right != null)
				binaryOperatorExpression.Right.AcceptVisitor(this, data);
			
			return null;
		}
	}
}