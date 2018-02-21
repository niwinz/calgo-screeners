using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo.Indicators {
    [Indicator(IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class Vcn : Indicator {
        private MacdCrossOver macd;
        private WeightedMovingAverage wma150;
        private ExponentialMovingAverage ema8;

        [Output("Result Potentian", Color = Colors.Orange, PlotType = PlotType.Histogram)]
        public IndicatorDataSeries Result1 { get; set; }

        [Output("Result Entry Point", Color = Colors.Red, PlotType = PlotType.Histogram)]
        public IndicatorDataSeries Result2 { get; set; }

        protected override void Initialize() {
            macd = Indicators.MacdCrossOver(MarketSeries.Close, 26, 12, 9);
            wma150 = Indicators.WeightedMovingAverage(MarketSeries.Close, 150);
            ema8 = Indicators.ExponentialMovingAverage(MarketSeries.Close, 8);
        }

        public override void Calculate(int index) {
            // Do nothing at start
            if (index < 4) return;

            var isTrendUp = MarketSeries.Close[index] > wma150.Result[index];

            if (isTrendUp) {
                if ((macd.Histogram[index] > 0 && macd.Signal[index] > 0) ||
                    (macd.Histogram[index] > 0 && macd.Signal[index] < 0)) {
                    if (MarketSeries.Low[index-1] > ema8.Result[index-1]
                        && MarketSeries.Low[index-2] > ema8.Result[index-2]
                        && MarketSeries.Low[index-3] > ema8.Result[index-3]
                        && MarketSeries.Low[index] <= ema8.Result[index]) {
                        Result2[index] = 1;
                    } else if (MarketSeries.Low[index] > ema8.Result[index]) {
                        Result1[index] = 1;
                    }
                }
            } else {
                if ((macd.Histogram[index] < 0 && macd.Signal[index] < 0) ||
                    (macd.Histogram[index] < 0 && macd.Signal[index] > 0)) {
                    if (MarketSeries.High[index - 1] < ema8.Result[index - 1]
                        && MarketSeries.High[index - 2] < ema8.Result[index - 2]
                        && MarketSeries.High[index - 3] < ema8.Result[index - 3]
                        && MarketSeries.High[index] >= ema8.Result[index]) {
                        Result2[index] = -1;
                    } else if (MarketSeries.High[index] < ema8.Result[index]) {
                        Result1[index] = -1;
                    }
                }
            }
        }
    }
}
