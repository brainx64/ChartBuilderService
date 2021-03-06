﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace ChartBuilderService
{
    [ServiceContract]
    public interface IChartBuilderService
    {
        [OperationContract]
        Chart GetChart(Chart chart);
    }

    [DataContract]
    public class Chart
    {
        [DataMember]
        public String Expression { get; set; }

        [DataMember]
        public Double MinX { get; set; }

        [DataMember]
        public Double MaxX { get; set; }

        [DataMember]
        public Double MinY { get; set; }

        [DataMember]
        public Double MaxY { get; set; }

        [DataMember]
        public Int32 Width { get; set; }

        [DataMember]
        public Int32 Height { get; set; }

        [DataMember]
        public Byte[] ImageBytes { get; set; }

        [DataMember]
        public String ErrorMessage { get; set; }
    }
}