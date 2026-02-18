using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using System;
using System.Collections.Generic;

namespace CodeMetrics
{
	public class MetricsCommand : AbstractMenuCommand
	{
		/// <summary>
		/// Точка входа в логику плагина.
		/// </summary>
		public override void Run()
		{
			IViewContent view = WorkbenchSingleton.Workbench.ActiveViewContent;
			if (view != null && view.PrimaryFile != null)
			{
				string fileName = view.PrimaryFileName.ToString();
				MetricsResult result = MetricsAnalyzer.AnalyzeFile(fileName);
				
				ShowPad();
				UpdatePadContent();
			}
		}
		
		/// <summary>
		/// Отобразить вкладку "Code Metrics".
		/// </summary>
		private static void ShowPad()
		{
			IList<PadDescriptor> pads = WorkbenchSingleton.Workbench.PadContentCollection;
			int padIndex = -1;
			for (int i = 0; i < pads.Count; i++)
			{
				PadDescriptor pd = (PadDescriptor)pads[i];
				if (pd.Class == "CodeMetrics.MetricsPad")
				{
					padIndex = i;
					break;
				}
			}
			
			if (padIndex >= 0)
			{
				PadDescriptor padDescriptor = (PadDescriptor)pads[padIndex];
				WorkbenchSingleton.Workbench.WorkbenchLayout.ActivatePad(padDescriptor);
			}
		}
		
		/// <summary>
		/// Обновить данные на вкладке "Code Metrics".
		/// </summary>
		private static void UpdatePadContent()
		{
			IList<PadDescriptor> pads = WorkbenchSingleton.Workbench.PadContentCollection;
			for (int i = 0; i < pads.Count; i++)
			{
				PadDescriptor pd = (PadDescriptor)pads[i];
				if (pd.Class == "CodeMetrics.MetricsPad" && pd.PadContent != null)
				{
					IPadContent padContent = pd.PadContent;
					if (padContent is MetricsPad)
					{
						MetricsPad metricsPad = (MetricsPad)padContent;
						metricsPad.RefreshView();
						break;
					}
				}
			}
		}
	}
}