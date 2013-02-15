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
    public class ChartBuilderService : IChartBuilderService
    {
        private const Int32 POINTS_COUNT = 1000;

        private Func<double, double> CreateExpressionEvaluator(string expression)
        {
            ExpressionContext context = new ExpressionContext();
            context.Options.EmitToAssembly = false;
            context.Imports.AddType(typeof(Math));

            context.Variables["x"] = 0.0d;

            IDynamicExpression fx = null;

            try
            {
                fx = context.CompileDynamic(expression);
            }
            catch (ExpressionCompileException)
            {
                return null;
            }

            Func<Double, Double> expressionEvaluator = (Double i) =>
            {
                context.Variables["x"] = i;
                return (Double)fx.Evaluate();
            };

            return expressionEvaluator;
        }

        private List<List<Double>> SplitData(IEnumerable<Double> data, Func<Double, Double> expressionEvaluator, Double minY, Double maxY)
        {
            List<List<Double>> dataRanges = new List<List<Double>>();
            List<Double> dataRange = null;

            foreach (Double x in data)
            {
                Double y = expressionEvaluator(x);

                if (Double.IsNaN(y) || Double.IsInfinity(y) || y < minY || y > maxY)
                {
                    if (dataRange != null)
                    {
                        dataRanges.Add(dataRange);
                        dataRange = null;
                    }

                    continue;
                }

                if (dataRange == null)
                {
                    dataRange = new List<Double>();
                }

                dataRange.Add(x);
            }

            if (dataRange != null)
            {
                dataRanges.Add(dataRange);
            }

            return dataRanges;
        }

        private List<EnumerableDataSource<Double>> CreatePointDataSources(Chart chart, Func<Double, Double> expressionEvaluator)
        {
            Double coefficient = (chart.MaxX - chart.MinX) / POINTS_COUNT;
            IEnumerable<Double> data = Enumerable.Range(0, POINTS_COUNT).Select(i => chart.MinX + (Double)i * coefficient);

            List<List<Double>> dataRanges = this.SplitData(data, expressionEvaluator, chart.MinY, chart.MaxY);
            List<EnumerableDataSource<Double>> pointDataSources = new List<EnumerableDataSource<Double>>();

            foreach (List<Double> dataRange in dataRanges)
            {
                EnumerableDataSource<Double> pointDataSource = new EnumerableDataSource<Double>(dataRange);
                pointDataSource.SetXMapping(i => i);
                pointDataSource.SetYMapping(expressionEvaluator);
                pointDataSources.Add(pointDataSource);
            }

            return pointDataSources;
        }

        public Chart GetChart(Chart chart)
        {
            if (String.IsNullOrEmpty(chart.Expression))
            {
                return chart;
            }

            if (chart.MaxX - chart.MinX <= 0)
            {
                return chart;
            }

            if (chart.MaxY - chart.MinY <= 0)
            {
                return chart;
            }

            if (chart.Width <= 0 || chart.Height <= 0)
            {
                return chart;
            }

            Func<Double, Double> expressionEvaluator = this.CreateExpressionEvaluator(chart.Expression);

            if (expressionEvaluator == null)
            {
                return chart;
            }

            OperationContext operationContext = OperationContext.Current;

            Thread thread = new Thread(new ThreadStart(delegate
            {
                using (new OperationContextScope(operationContext))
                {
                    ChartPlotter chartProtter = new ChartPlotter();
                    chartProtter.Width = chart.Width;
                    chartProtter.Height = chart.Height;

                    List<EnumerableDataSource<Double>> pointDataSources = this.CreatePointDataSources(chart, expressionEvaluator);

                    foreach (EnumerableDataSource<Double> pointDataSource in pointDataSources)
                    {
                        LineGraph lineGraph = new LineGraph(pointDataSource);
                        lineGraph.LinePen = new System.Windows.Media.Pen(lineGraph.LinePen.Brush, 2.0);
                        chartProtter.Children.Add(lineGraph);
                    }

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