using Microsoft.Research.DynamicDataDisplay;
using Microsoft.Research.DynamicDataDisplay.DataSources;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading;
using Ciloci.Flee;
using Ciloci.Flee.CalcEngine;

namespace ChartBuilderService
{
    // ПРИМЕЧАНИЕ. Команду "Переименовать" в меню "Рефакторинг" можно использовать для одновременного изменения имени класса "Service1" в коде, SVC-файле и файле конфигурации.
    // ПРИМЕЧАНИЕ. Чтобы запустить клиент проверки WCF для тестирования службы, выберите элементы Service1.svc или Service1.svc.cs в обозревателе решений и начните отладку.
    public class ChartBuilderService : IChartBuilderService
    {
        private Func<double, double> CreateExpressionForX(string expression)
        {
            ExpressionContext context = new ExpressionContext();

            // Define some variables
            context.Variables["x"] = 0.0d;

            // Use the variables in the expression
            IDynamicExpression e = context.CompileDynamic(expression);

            Func<double, double> expressionEvaluator = (double input) =>
            {
                context.Variables["x"] = input;
                var result = (double)e.Evaluate();
                return result;
            };

            return expressionEvaluator;
        }

        public Chart GetChart(Chart chart)
        {
            ExpressionContext context = new ExpressionContext();
            context.Options.EmitToAssembly = false;
            context.Imports.AddType(typeof(Math));

            context.Variables["x"] = 0.0d;

            IDynamicExpression fx = null;

            try
            {
                fx = context.CompileDynamic(chart.Expression);
            }
            catch (ExpressionCompileException)
            {
                return chart;
            }

            Int32 pointsCount = 1000;
            Double coefficient = (chart.EndX - chart.StartX) / pointsCount;

            IEnumerable<Double> data = Enumerable.Range(0, pointsCount).Select(i => chart.StartX + (Double)i * coefficient);

            EnumerableDataSource<Double> pointDataSource = new EnumerableDataSource<Double>(data);
            pointDataSource.SetXMapping(i => i);

            Func<Double, Double> expressionEvaluator = (Double i) =>
            {
                context.Variables["x"] = i;
                return (Double)fx.Evaluate();
            };

            pointDataSource.SetYMapping(expressionEvaluator);
            
            OperationContext operationContext = OperationContext.Current;

            Thread thread = new Thread(new ThreadStart(delegate
            {
                using (new OperationContextScope(operationContext))
                {
                    ChartPlotter chartProtter = new ChartPlotter();
                    chartProtter.Width = chart.Width;
                    chartProtter.Height = chart.Height;
                    
                    LineGraph lineGraph = new LineGraph(pointDataSource);
                    lineGraph.LinePen = new System.Windows.Media.Pen(lineGraph.LinePen.Brush, 2.0);
                    chartProtter.Children.Add(lineGraph);
                    chartProtter.LegendVisible = false;

                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        chartProtter.SaveScreenshotToStream(memoryStream, "png");
                        chart.ImageBytes = memoryStream.ToArray();
                    }
                }
            }));

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            return chart;
        }
    }
}