using System;
using HdrHistogram;

namespace Abc.Zerio.Client
{
    public class BenchmarkResult
    {
        private readonly int _clients;

        public BenchmarkResult(int clients)
        {
            _clients = clients;
        }

        // prop name => csv headers, therefore short, do not refactor

        public string Transport { get; set; }
        public DateTime Started { get; set; }
        public DateTime Ended { get; set; }

        public int TgtMsgCount { get; set; }
        public int ActMsgCount { get; set; }

        public int MsgSize { get; set; }
        public int Delay { get; set; }
        public int Burst { get; set; }

        public double TgtMsgSec => _clients * Math.Round((1_000_000.0 / Delay) * Burst, MidpointRounding.ToZero);

        public double TgtBwMb => Math.Round(TgtMsgSec * MsgSize * 8 / 1_000_000.0, MidpointRounding.ToZero);

        public double ActMsgSec => Math.Round(ActMsgCount / (Ended - Started).TotalSeconds, MidpointRounding.ToZero);

        public double ActBwMb => Math.Round(ActMsgSec * MsgSize * 8 / 1_000_000.0, MidpointRounding.ToZero);

        public double Average { get; set; }
        public double StDev { get; set; }

        public double Min { get; set; }
        public double P10 { get; set; }
        public double P20 { get; set; }
        public double P30 { get; set; }
        public double P40 { get; set; }
        public double P50 { get; set; }
        public double P60 { get; set; }
        public double P70 { get; set; }
        public double P80 { get; set; }
        public double P90 { get; set; }
        public double P925 { get; set; }
        public double P95 { get; set; }
        public double P975 { get; set; }
        public double P99 { get; set; }
        public double P999 { get; set; }
        public double P9999 { get; set; }
        public double P99999 { get; set; }
        public double Max { get; set; }

        public void SetStats(LongHistogram h)
        {
            Average = Math.Round(h.GetMean(), MidpointRounding.ToZero);
            StDev = Math.Round(h.GetStdDeviation(), MidpointRounding.ToZero);
            Max = h.GetMaxValue();
            Min = h.GetValueAtPercentile(0.0);
            P10 = h.GetValueAtPercentile(10);
            P20 = h.GetValueAtPercentile(20);
            P30 = h.GetValueAtPercentile(30);
            P40 = h.GetValueAtPercentile(40);
            P50 = h.GetValueAtPercentile(50);
            P60 = h.GetValueAtPercentile(60);
            P70 = h.GetValueAtPercentile(70);
            P80 = h.GetValueAtPercentile(80);
            P90 = h.GetValueAtPercentile(90);
            P925 = h.GetValueAtPercentile(92.5);
            P95 = h.GetValueAtPercentile(95);
            P975 = h.GetValueAtPercentile(97.5);
            P99 = h.GetValueAtPercentile(99);
            P999 = h.GetValueAtPercentile(99.9);
            P9999 = h.GetValueAtPercentile(99.99);
            P99999 = h.GetValueAtPercentile(99.999);
        }
    }
}
