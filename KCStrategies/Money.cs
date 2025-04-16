#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.NinjaScript;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class Money : KCAlgoBase
    {		
		
        private LEOMOMENTUM1 leoMomentum;
		
		private EMA EMA60;
        private EMA EMA200;
		
		private TrendMagic TrendMagic1;
		private int cciPeriod;
		private int atrPeriod;
		private bool longSignal = false;
        private bool shortSignal = false;
        private bool activeOrder = false;
		private bool uptrend = false;
		private bool downtrend = false;
		

		public override string DisplayName { get { return Name; } }

        protected override void OnStateChange()
        {
            base.OnStateChange();
            
            if (State == State.SetDefaults)
            {
                Description = "Strategy based on the TrendMagic and Leomomentum indicators";
                Name = "Money v3.6";
                StrategyName = "Money";
                Version = "3.6 Jan. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType = "TDU Renko Backtest 70-2";	
				
				cciPeriod		= 20;
				atrPeriod		= 9;
				atrMult			= 1.2;
				TrendMagicThreshold = 20.0; // 5 Points (20 Ticks) default threshold
				
				FastEMA			= 60;
                SlowEMA			= 200;
				
				InitialStop		= 20;
				ProfitTarget	= 40;	
            }
            else if (State == State.DataLoaded)
            {
                InitializeIndicators();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequiredToTrade)
                return;
            
			uptrend = Close[0] > EMA60[0] && Close[0] > EMA200[0] && EMA60[0] > EMA200[0];
			downtrend = Close[0] < EMA60[0] && Close[0] < EMA200[0] && EMA60[0] < EMA200[0];
			longSignal = (TrendMagic1.Trend[0] > TrendMagic1.Trend[1] && TrendMagic1.Trend[1] == TrendMagic1.Trend[2] && uptrend);
            shortSignal = (TrendMagic1.Trend[0] < TrendMagic1.Trend[1] && TrendMagic1.Trend[1] == TrendMagic1.Trend[2] && downtrend);	
//			longSignal = (TrendMagic1.Trend[0] > TrendMagic1.Trend[1] && leoMomentum.Analyzer[0] == 1 && Math.Abs(Close[0] - TrendMagic1.Trend[0]) <= TrendMagicThreshold * TickSize && uptrend);
//            shortSignal = (TrendMagic1.Trend[0] < TrendMagic1.Trend[1] && leoMomentum.Analyzer[0] == -1 && Math.Abs(Close[0] - TrendMagic1.Trend[0]) <= TrendMagicThreshold * TickSize && downtrend);	
			
            base.OnBarUpdate();
        }

        protected override bool ValidateEntryLong()
        {
            // Logic for validating long entries
			if (longSignal) return true;
			else return false;
        }

        protected override bool ValidateEntryShort()
        {
            // Logic for validating short entries
			if (shortSignal) return true;
            else return false;
        }

        protected override bool ValidateExitLong()
        {
            // Logic for validating long exits
            return enableExit? true : false;
        }

        protected override bool ValidateExitShort()
        {
			// Logic for validating short exits
			return enableExit? true : false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
				
			TrendMagic1		 	= TrendMagic(cciPeriod, atrPeriod, atrMult, false);
            AddChartIndicator(TrendMagic1);
			
			leoMomentum = LEOMOMENTUM1(16, 6, 18, 0.01, -0.01, true, Brushes.Lime, Brushes.Red, Brushes.Gray);
            AddChartIndicator(leoMomentum);
			
			EMA60 = EMA(Close, FastEMA);
            EMA200 = EMA(Close, SlowEMA);
			EMA60.Plots[0].Brush = Brushes.Cyan;
			EMA200.Plots[0].Brush = Brushes.Yellow;
			EMA60.Plots[0].Width = 2;
			EMA200.Plots[0].Width = 2;
			AddChartIndicator(EMA60);
			AddChartIndicator(EMA200);
        }
        #endregion

        #region Properties
        [NinjaScriptProperty]
		[Display(Name="TrendMagic ATR Multiplier", Order = 1, GroupName="07. Strategy Settings")]
		public double atrMult
		{ get; set; }
		
        [NinjaScriptProperty]
        [Range(0, double.MaxValue)]
        [Display(Name = "TrendMagic Threshold (Ticks)", Order = 2, GroupName = "07. Strategy Settings")]
        public double TrendMagicThreshold { get; set; }
		
		[NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA 60", Order = 3, GroupName = "07. Strategy Settings")]
        public int FastEMA { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA 200", Order = 4, GroupName = "07. Strategy Settings")]
        public int SlowEMA { get; set; }
        #endregion
    }
}
