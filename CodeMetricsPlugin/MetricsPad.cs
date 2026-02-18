using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Editor;
using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;

namespace CodeMetrics
{
	public class MetricsPad : AbstractPadContent
	{
		private SplitContainer split;
		private DataGridView summaryGrid;
		private DataGridView methodsGrid;
		private string lastAnalyzedFile;
		private DateTime lastFileModified;
		private ITextEditor editor;
		
		public MetricsPad()
		{
			split = new SplitContainer();
			split.Dock = DockStyle.Fill;
			split.Orientation = Orientation.Vertical;
			split.SplitterDistance = 150;
			
			summaryGrid = new DataGridView();
			summaryGrid.Dock = DockStyle.Fill;
			summaryGrid.ReadOnly = true;
			summaryGrid.RowHeadersVisible = false;
			summaryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			summaryGrid.AutoGenerateColumns = false;
			
			DataGridViewTextBoxColumn colMetric = new DataGridViewTextBoxColumn();
			colMetric.Name = "Metric";
			colMetric.HeaderText = "Метрика";
			colMetric.Width = 150;
			summaryGrid.Columns.Add(colMetric);
			
			DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
			colValue.Name = "Value";
			colValue.HeaderText = "Значение";
			colValue.Width = 80;
			summaryGrid.Columns.Add(colValue);
			
			methodsGrid = new DataGridView();
			methodsGrid.Dock = DockStyle.Fill;
			methodsGrid.ReadOnly = true;
			methodsGrid.RowHeadersVisible = false;
			methodsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			methodsGrid.AutoGenerateColumns = false;
			
			DataGridViewTextBoxColumn colName = new DataGridViewTextBoxColumn();
			colName.Name = "Name";
			colName.HeaderText = "Метод";
			colName.Width = 180;
			methodsGrid.Columns.Add(colName);
			
			DataGridViewTextBoxColumn colLine = new DataGridViewTextBoxColumn();
			colLine.Name = "Line";
			colLine.HeaderText = "Строка";
			colLine.Width = 60;
			methodsGrid.Columns.Add(colLine);
			
			DataGridViewTextBoxColumn colCC = new DataGridViewTextBoxColumn();
			colCC.Name = "CC";
			colCC.HeaderText = "CC";
			colCC.Width = 50;
			methodsGrid.Columns.Add(colCC);
			
			DataGridViewTextBoxColumn colNesting = new DataGridViewTextBoxColumn();
			colNesting.Name = "Nesting";
			colNesting.HeaderText = "Вложенность";
			colNesting.Width = 90;
			methodsGrid.Columns.Add(colNesting);
			
			DataGridViewTextBoxColumn colParams = new DataGridViewTextBoxColumn();
			colParams.Name = "Params";
			colParams.HeaderText = "Параметры";
			colParams.Width = 80;
			methodsGrid.Columns.Add(colParams);
			
			split.Panel1.Controls.Add(summaryGrid);
			split.Panel2.Controls.Add(methodsGrid);
			
			methodsGrid.CellMouseDoubleClick += new DataGridViewCellMouseEventHandler(OnMethodDoubleClick);
			WorkbenchSingleton.Workbench.ActiveViewContentChanged += OnActiveViewContentChanged;
			
			SubscribeToCurrentEditor();
			RefreshView();
		}
		
		private void OnActiveViewContentChanged(object sender, EventArgs e)
		{
			UnsubscribeFromEditor();
			SubscribeToCurrentEditor();
		}
		
		private void SubscribeToCurrentEditor()
		{
			IViewContent view = WorkbenchSingleton.Workbench.ActiveViewContent;
			if (view == null)
				return;
			
			ITextEditorProvider provider = view as ITextEditorProvider;
			if (provider == null)
				return;
			
			editor = provider.TextEditor;
			if (editor == null)
				return;
			
			editor.Caret.PositionChanged += new EventHandler(OnCaretPositionChanged);
		}
		
		private void UnsubscribeFromEditor()
		{
			if (editor != null)
				editor.Caret.PositionChanged -= new EventHandler(OnCaretPositionChanged);
		}
		
		private void OnCaretPositionChanged(object sender, EventArgs e)
		{
			TriggerAnalysis();
		}
		
		private void TriggerAnalysis()
		{
			IViewContent view = WorkbenchSingleton.Workbench.ActiveViewContent;
			if (view == null || view.PrimaryFile == null)
				return;
			
			string fileName = view.PrimaryFileName.ToString();
			if (!File.Exists(fileName))
				return;
			
			// Оптимизация: анализ только при изменении файла
			FileInfo fi = new FileInfo(fileName);
			if (lastAnalyzedFile != fileName || fi.LastWriteTime != lastFileModified)
			{
				AnalyzeAndUpdate(fileName);
				lastAnalyzedFile = fileName;
				lastFileModified = fi.LastWriteTime;
			}
			else
			{
				// Быстрое обновление без повторного анализа
				RefreshView();
				HighlightCurrentMethod();
			}
		}
		
		private void AnalyzeAndUpdate(string fileName)
		{
			MetricsResult result = MetricsAnalyzer.AnalyzeFile(fileName);
			RefreshView();
			HighlightCurrentMethod();
		}
		
		private void HighlightCurrentMethod()
		{
			if (editor == null || editor.Caret == null)
				return;
			
			int currentLine = editor.Caret.Line + 1;
			MetricsResult result = MetricsAnalyzer.GetLastResult();
			
			// Поиск метода, содержащего текущую строку
			int closestIndex = -1;
			int minDistance = int.MaxValue;
			
			for (int i = 0; i < result.Methods.Count; i++)
			{
				MethodMetric method = (MethodMetric)result.Methods[i];
				int distance = Math.Abs(method.Line - currentLine);
				
				if (distance < minDistance)
				{
					minDistance = distance;
					closestIndex = i;
				}
			}
			
			if (closestIndex >= 0)
			{
				methodsGrid.ClearSelection();
				methodsGrid.Rows[closestIndex].Selected = true;
				methodsGrid.FirstDisplayedScrollingRowIndex = closestIndex;
			}
		}
		
		private void OnMethodDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.RowIndex < 0)
				return;
			
			try
			{
				DataGridViewRow row = methodsGrid.Rows[e.RowIndex];
				if (row.Tag != null && row.Tag is MethodMetric)
				{
					MethodMetric method = (MethodMetric)row.Tag;
					MetricsResult metrics = MetricsAnalyzer.GetLastResult();
					editor.JumpTo(method.Line, 1);
				}
			}
			catch (Exception ex)
			{
				LoggingService.Warn("Failed to jump to method position", ex);
			}
		}
		
		public override object Control
		{
			get { return split; }
		}
		
		public void RefreshView()
		{
			MetricsResult m = MetricsAnalyzer.GetLastResult();
			UpdateSummary(m);
			UpdateMethods(m);
		}
		
		private void UpdateSummary(MetricsResult m)
		{
			summaryGrid.Rows.Clear();
			
			AddRow("Файл", m.FileName, false);
			AddRow("Время анализа", m.AnalyzedAt.ToString("HH:mm:ss"), false);
			AddRow("Строк кода", m.LinesOfCode.ToString(), m.LinesOfCode > Constants.MaxLinesOfCode);
			AddRow("Комментариев", string.Format("{0} ({1:P1})", m.CommentLines, m.CommentRatio), false);
			AddRow("Пустых строк", m.BlankLines.ToString(), false);
			AddRow("Методов", m.MethodCount.ToString(), false);
			AddRow("Макс. CC", m.MaxCyclomaticComplexity.ToString(), m.MaxCyclomaticComplexity > Constants.MaxCyclomaticComplexity);
			AddRow("Средняя CC", m.AvgCyclomaticComplexity.ToString("F2"), false);
			AddRow("Макс. вложенность", m.MaxNestingDepth.ToString(), m.MaxNestingDepth > Constants.MaxNestingDepth);
			AddRow("Макс. параметров", m.MaxParameters.ToString(), m.MaxParameters > Constants.MaxParameters);
		}
		
		private void AddRow(string metric, string value, bool isWarning)
		{
			int idx = summaryGrid.Rows.Add();
			summaryGrid.Rows[idx].Cells[0].Value = metric;
			summaryGrid.Rows[idx].Cells[1].Value = value;
			
			if (isWarning)
				summaryGrid.Rows[idx].DefaultCellStyle.BackColor = Color.LightYellow;
		}
		
		private void UpdateMethods(MetricsResult m)
		{
			methodsGrid.Rows.Clear();
			
			for (int i = 0; i < m.Methods.Count; i++)
			{
				MethodMetric method = (MethodMetric)m.Methods[i];
				
				int idx = methodsGrid.Rows.Add();
				methodsGrid.Rows[idx].Cells[0].Value = method.Name;
				methodsGrid.Rows[idx].Cells[1].Value = method.Line.ToString();
				methodsGrid.Rows[idx].Cells[2].Value = method.CyclomaticComplexity.ToString();
				methodsGrid.Rows[idx].Cells[3].Value = method.NestingDepth.ToString();
				methodsGrid.Rows[idx].Cells[4].Value = method.ParameterCount.ToString();
				methodsGrid.Rows[idx].Tag = method;
				
				bool isWarning = method.CyclomaticComplexity > Constants.MaxCyclomaticComplexity || method.NestingDepth > Constants.MaxNestingDepth;
				if (isWarning)
					methodsGrid.Rows[idx].DefaultCellStyle.BackColor = Color.LightYellow;
			}
		}
		
		public override void Dispose()
		{
			WorkbenchSingleton.Workbench.ActiveViewContentChanged -= OnActiveViewContentChanged;
			UnsubscribeFromEditor();
			base.Dispose();
		}
	}
}