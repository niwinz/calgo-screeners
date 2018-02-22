using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;

namespace cAlgo {
    [Robot(TimeZone = TimeZones.WEuropeStandardTime, AccessRights = AccessRights.FullAccess)]
    public class Screener : Robot {

        [Parameter("Source")]
        public DataSeries Source { get; set; }

        [Parameter("Time Frame")]
        public TimeFrame loctf { get; set; }

        private Dictionary<String, MarketSeries> lseries;
        private Dictionary<String, MarketSeries> rseries;

        private Dictionary<String, Symbol> symbols;

        private String[] pairs = new String[] {
            "EURUSD",
            "GBPUSD",
            //"EURJPY",
            //"USDJPY",
            //"AUDUSD",
            //"USDCHF",
            //"GBPJPY",
            //"USDCAD",
            //"EURGBP",
            //"EURCHF",
            //"AUDJPY",
            //"NZDUSD",
            //"CHFJPY",
            //"EURAUD",
            //"CADJPY",
            //"GBPAUD",
            //"EURCAD",
            //"AUDCAD",
            //"GBPCHF",
            //"AUDCHF",
            //"GBPCAD",
            //"GBPNZD",
            //"NZDCAD",
            //"NZDCHF",
            //"NZDJPY",
            //"AUDNZD",
            //"CADCHF",
            //"EURNZD"
        };
        //private String[] pairs = new String[] { "EURUSD", "USDJPY" };

        //private Dictionary<String, ExponentialMovingAverage> mm8;
        //private Dictionary<String, WeightedMovingAverage> mm50;
        private Dictionary<String, WeightedMovingAverage> lmm150;
        private Dictionary<String, WeightedMovingAverage> lmm300;
        private Dictionary<String, WeightedMovingAverage> rmm150;
        private Dictionary<String, WeightedMovingAverage> rmm300;

        private Dictionary<String, MacdCrossOver> lmacd;
        private Dictionary<String, MacdCrossOver> rmacd;

        private long barCounter;
        private long ticks = 0;
        private TimeFrame reftf;
        private bool fistRun = true;


        protected override void OnStart() {
            barCounter = Source.Count;

            lseries = new Dictionary<string, MarketSeries>();
            rseries = new Dictionary<string, MarketSeries>();

            symbols = new Dictionary<string, Symbol>();

            //mm8 = new Dictionary<string, ExponentialMovingAverage>();
            //mm50 = new Dictionary<string, WeightedMovingAverage>();
            lmm150 = new Dictionary<string, WeightedMovingAverage>();
            lmm300 = new Dictionary<string, WeightedMovingAverage>();
            rmm150 = new Dictionary<string, WeightedMovingAverage>();
            rmm300 = new Dictionary<string, WeightedMovingAverage>();

            lmacd = new Dictionary<string, MacdCrossOver>();
            rmacd = new Dictionary<string, MacdCrossOver>();

            Print("Initializing screener local state.");

            foreach (var sym in pairs) {
                symbols[sym] = MarketData.GetSymbol(sym);
            }

            reftf = GetReferenceTimeframe(loctf);

            foreach (var sym in pairs) {
                var lmks = MarketData.GetSeries(sym, loctf);
                var rmks = MarketData.GetSeries(sym, reftf);

                Print("Initializing data for: {0}/{1}.", sym, loctf);

                //mm8[sym] = Indicators.ExponentialMovingAverage(mks.Close, 50);
                //mm50[sym] = Indicators.WeightedMovingAverage(mks.Close, 50);
                lmm150[sym] = Indicators.WeightedMovingAverage(lmks.Close, 150);
                lmm300[sym] = Indicators.WeightedMovingAverage(lmks.Close, 300);
                rmm150[sym] = Indicators.WeightedMovingAverage(rmks.Close, 150);
                rmm300[sym] = Indicators.WeightedMovingAverage(rmks.Close, 300);

                lmacd[sym] = Indicators.MacdCrossOver(lmks.Close, 26, 12, 9);
                rmacd[sym] = Indicators.MacdCrossOver(rmks.Close, 26, 12, 9);

                lseries[sym] = lmks;
                rseries[sym] = rmks;
            }

            Print("Initialization finished.");
        }

        protected override void OnStop() {
            Print("Bot Stopped with total ticks: {0}", ticks);
        }

        private bool IsNewBar() {
            if (barCounter < Source.Count) {
                barCounter = Source.Count;
                return true;
            } else {
                return false;
            }
        }

        protected override void OnTick() {
            ticks++;

            if (IsNewBar() || fistRun) {
                HandleUpdate();
            }

            if (fistRun) fistRun = false;
        }

        // -------------------------------------------
        // ----  Market Timing
        // -------------------------------------------

        private Tuple<int, int> CalculateMarketTiming(String sym) {
            var local = CalculateLocalTiming(sym);
            var reference = CalculateReferenceTiming(sym);

            return new Tuple<int, int>(reference, local);
        }

        private bool IsTrendUp(MarketSeries series, WeightedMovingAverage wma) {
            var close = series.Close.LastValue;
            var value = wma.Result.LastValue;

            if (value < close) {
                return true;
            } else {
                return false;
            }
        }

        private int CalculateLocalTiming(String sym) {
            if (IsTrendUp(lseries[sym], lmm150[sym])) {
                if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue > 0) {
                    return 1;
                } else if (lmacd[sym].Histogram.LastValue > 0 && lmacd[sym].Signal.LastValue < 0) {
                    return 4;
                } else if (lmacd[sym].Histogram.LastValue <= 0 && lmacd[sym].Signal.LastValue >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue < 0) {
                    return -1;
                } else if (lmacd[sym].Histogram.LastValue < 0 && lmacd[sym].Signal.LastValue > 0) {
                    return -4;
                } else if (lmacd[sym].Histogram.LastValue >= 0 && lmacd[sym].Signal.LastValue <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        private int CalculateReferenceTiming(String sym) {
            if (IsTrendUp(rseries[sym], rmm150[sym])) {
                if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue > 0) {
                    return 1;
                } else if (rmacd[sym].Histogram.LastValue > 0 && rmacd[sym].Signal.LastValue < 0) {
                    return 4;
                } else if (rmacd[sym].Histogram.LastValue <= 0 && rmacd[sym].Signal.LastValue >= 0) {
                    return 2;
                } else {
                    return 3;
                }
            } else {
                if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue < 0) {
                    return -1;
                } else if (rmacd[sym].Histogram.LastValue < 0 && rmacd[sym].Signal.LastValue > 0) {
                    return -4;
                } else if (rmacd[sym].Histogram.LastValue >= 0 && rmacd[sym].Signal.LastValue <= 0) {
                    return -2;
                } else {
                    return -3;
                }
            }
        }

        private TimeFrame GetReferenceTimeframe(TimeFrame tf) {
            if (tf == TimeFrame.Hour) {
                return TimeFrame.Daily;
            } else if (tf == TimeFrame.Minute5) {
                return TimeFrame.Hour;
            } else if (tf == TimeFrame.Daily) {
                return TimeFrame.Weekly;
            } else if (tf == TimeFrame.Weekly) {
                return TimeFrame.Weekly;
            } else {
                return TimeFrame.Hour;
            }
        }

        private void HandleUpdate() {
            var output = new List<String>(symbols.Count);
            var time = Server.Time.ToString("yyyy-MM-dd HH:mm:ss");

            output.Add(string.Format("Timeframe: {0}\r\nUpdated at: {1}\r\n\r\n\r\n", loctf.ToString(), time));
            output.Add(string.Format("{0,12}\t{1,8}\r\n", "Symbol", "Timing"));
            output.Add("---------------------------------------------------------------------------------------------\n\r");

            foreach (var sym in pairs) {
                HandleOnBar(sym, output);
            }

            var result = "";
            foreach (var item in output) {
                result += item;
            }

            ChartObjects.RemoveObject("screener");
            ChartObjects.DrawText("screener", result, StaticPosition.TopLeft, Colors.Black);
        }

        private void HandleOnBar(String sym, List<String> output) {
            var timing = CalculateMarketTiming(sym);

            var timingStr = string.Format("{0,2},{1,2}", timing.Item1, timing.Item2);
            var msg = string.Format("{0, 12}\t{1,8}\r\n", sym, timingStr);

            output.Add(msg);
        }
    }
}
