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

            IDynamicExpression f = null;

            try
            {
                f = context.CompileDynamic(chart.Expression);
            }
            catch (ExpressionCompileException)
            {
                return chart;
            }
            
            IEnumerable<Double> data = Enumerable.Range(0, 10 * 10).Select(i => (Double)i);

            EnumerableDataSource<Double> pointDataSource = new EnumerableDataSource<Double>(data);
            pointDataSource.SetXMapping(i => i / 10);

            Func<Double, Double> expressionEvaluator = (Double i) =>
            {
                context.Variables["x"] = i / 10;
                return (Double)f.Evaluate();
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
                    chartProtter.Children.Add(lineGraph);

                    MemoryStream memoryStream = new MemoryStream();
                    chartProtter.SaveScreenshotToStream(memoryStream, "png");
                    chart.ImageBytes = memoryStream.ToArray();
                }
            }));

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            return chart;
        }
    }
}