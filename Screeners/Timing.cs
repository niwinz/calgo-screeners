using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Timing : Indicator {

        [Output("Result", Color = Colors.Orange, PlotType = PlotType.Line)]
        public IndicatorDataSeries Result { get; set; }

        [Output("Result2", Color = Colors.Green, PlotType = PlotType.Line)]
        public IndicatorDataSeries Result2 { get; set; }

        private MacdCrossOver lmacd;
        private MacdCrossOver rmacd;

        private WeightedMovingAverage lwma150;
        private WeightedMovingAverage rwma150;

        private MarketSeries rseries;
        private TimeFrame reftf;
        private int minimumIndex = 20;

        //private string upArrow = "↑";
        //private string downArrow = "↓";
        //private double arrowOffset;

        protected override void Initialize() {
            reftf = GetReferenceTimeframe(MarketSeries.TimeFrame);
            rseries = MarketData.GetSeries(reftf);

            lmacd = Indicators.MacdCrossOver(MarketSeries.Close, 26, 12, 9);
            rmacd = Indicators.MacdCrossOver(rseries.Close, 26, 12, 9);

            lwma150 = Indicators.WeightedMovingAverage(MarketSeries.Close, 150);
            rwma150 = Indicators.WeightedMovingAverage(rseries.Close, 150);

            Print("Load reference time frame: {0}", reftf);

        }

        public override void Calculate(int index) {
            if (index < minimumIndex) return;
            if (!IsLastBar) {
                Result[index] = CalculateLocalTiming(index);
                Result2[index] = CalculateReferenceTiming(index);
            }

            //var rtiming = CalculateReferenceTiming(index);
            //ChartObjects.DrawText("test", String.Format("Timing: {0} {1}", rtiming, Result[index]), StaticPosition.TopLeft, Colors.Black);
        }

        public int CalculateLocalTiming(int index) {
            //Print("calculating for index: {0} and is real time?: {1}", index, IsRealTime ? "yes" : "no");
            if (lwma150.Result.IsRising()) {
                if (lmacd.Histogram[index] > 0 && lmacd.Signal[index] > 0) {
                    return 1;
                } else if (lmacd.Histogram[index] > 0 && lmacd.Signal[index] < 0) {
                    return 4;
                } else if (lmacd.Histogram[index] <= 0 && lmacd.Signal[index] >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (lmacd.Histogram[index] < 0 && lmacd.Signal[index] < 0) {
                    return -1;
                } else if (lmacd.Histogram[index] < 0 && lmacd.Signal[index] > 0) {
                    return -4;
                } else if (lmacd.Histogram[index] >= 0 && lmacd.Signal[index] <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        public int CalculateReferenceTiming(int lindex) {
            var index = GetIndexByDate(rseries, MarketSeries.OpenTime[lindex]);

            if (IsRising(rwma150.Result, index)) {
                if (rmacd.Histogram[index] > 0 && rmacd.Signal[index] > 0) {
                    return 1;
                } else if (rmacd.Histogram[index] > 0 && rmacd.Signal[index] < 0) {
                    return 4;
                } else if (rmacd.Histogram[index] <= 0 && rmacd.Signal[index] >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (rmacd.Histogram[index] < 0 && rmacd.Signal[index] < 0) {
                    return -1;
                } else if (rmacd.Histogram[index] < 0 && rmacd.Signal[index] > 0) {
                    return -4;
                } else if (rmacd.Histogram[index] >= 0 && rmacd.Signal[index] <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        private int GetIndexByDate(MarketSeries series, DateTime time) {
            for (int i = series.Close.Count - 1; i > 0; i--) {
                DateTime closeTime;
                if (reftf == TimeFrame.Hour) {
                    closeTime = series.OpenTime[i].AddHours(1);
                } else {
                    closeTime = series.OpenTime[i].AddDays(1);
                }

                if (time >= series.OpenTime[i] && time < closeTime) {
                    return i;
                }
            }
            return -1;
        }

        public TimeFrame GetReferenceTimeframe(TimeFrame tf) {
            if (tf == TimeFrame.Hour) {
                return TimeFrame.Daily;
            } else if (tf == TimeFrame.Minute5) {
                return TimeFrame.Hour;
            } else {
                return TimeFrame.Hour;
            }
        }

        private bool IsRising(DataSeries data, int index) {
            double sum = 0;
            int periods = minimumIndex;
            int counter = 0;

            for (int i = index - periods; i <= index; i++) {
                var val = data[i];
                if (!double.IsNaN(val)) {
                    sum += data[i];
                    counter++;
                }
            }

            return (sum / counter) > data[index-periods];
        }
    }
}
