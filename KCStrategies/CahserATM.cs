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
using NinjaTrader.NinjaScript.Strategies;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class CasherATM : ATMAlgoBase
    {
        // Parameters
		private Momentum Momentum1;
		
		private HiLoBands HiLoBands1; 
        private Series<double> highestHigh;
        private Series<double> lowestLow;
		private Series<double> midline;
		
		private bool longSignal = false;
        private bool shortSignal = false;

		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "This strategy is based on the HiLoBands indicator.";
                Name = "Casher v6.0.0";
                StrategyName = "Casher";
                Version = "6.0.0 May 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType =  "Tbars 25, 50";				
				
				LookbackPeriod		= 4;
				Width				= 2;
				showHighLow			= true;				
				
		        enableHmaHooks 		= false;
		        showHmaHooks 		= false;
				
		        enableVMA 			= false;
		        showVMA 			= false;

		        enableRegChan1 		= false;
		        enableRegChan2 		= false;
		        showRegChan1 		= false;
		        showRegChan2 		= false;
		        showRegChanHiLo 	= false;
            }
            else if (State == State.DataLoaded)
            {
				highestHigh = new Series<double> (this);
				lowestLow = new Series<double> (this);
				midline = new Series<double> (this);
				
                InitializeIndicators();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < BarsRequiredToTrade)
                return;
            
			// Ensure there are enough bars to calculate the average range
		    if (CurrentBar < LookbackPeriod)
		        return;
		
			highestHigh[0] = HiLoBands1.Values[0][0];
			lowestLow[0] = HiLoBands1.Values[1][0];
			midline[0] = HiLoBands1.Values[2][0];
			
		    // --- Long Signal Components ---
		
		    // 1. Midline Breakout: Close crosses decisively above the midline on the current bar.
		    bool longMidlineBreakout = CrossAbove(Close, midline, 1);
		
		    // 2. Midline Turning Up: Midline shows a clear upward turn,
		    //    meaning current midline is above previous, and previous was flat or declining.
		    bool longMidlineTurn = (midline[0] > midline[1] && midline[1] <= midline[2]);

		    // 3. Bounce off Lower Band: Price tests the lower band and shows signs of reversal.
		    //    - Previous bar's low touched or went below the lower band.
		    //    - Current bar's low is higher than the previous bar's low (not making new lows).
		    bool longBandBounce = (Low[1] <= lowestLow[1] && Low[0] > Low[1]);
		    
		    // 4. (Optional) Highest High Band Turning Up: The upper band itself starts to expand upwards.
		    //    This indicates a potential increase in upward volatility or a breakout from the recent range.
		    bool longHHBandTurn = (highestHigh[0] > highestHigh[1] && highestHigh[1] <= highestHigh[2]);
			
			bool lowCrossAboveMidline = (Low[0] > midline[0] && Low[1] <= midline[1]);
			
			bool midlineUpTurn = (midline[0] > midline[1]);
		
		    // --- Mandatory Confirmations for All Long Entries ---
		    // Current bar is bullish: Closes higher than its open AND closes higher than the previous bar's close.
		    bool bullishBarConfirm = (Close[0] > Open[0] && Close[0] > Close[1]);
		    // Momentum is rising AND Momentum value is positive.
		    bool momentumConfirmLong = (Momentum1[0] > Momentum1[1] && Momentum1[0] > 0);
		
		    // Combine: Any of the primary signals AND all confirmations.
		    // You can choose to include longHHBandTurn by uncommenting it if desired.
		    longSignal = (longMidlineBreakout || longMidlineTurn || longBandBounce || longHHBandTurn || lowCrossAboveMidline || midlineUpTurn)
		                 && bullishBarConfirm
		                 && momentumConfirmLong;
		
		    // --- Short Signal Components ---
		
		    // 1. Midline Breakdown: Close crosses decisively below the midline on the current bar.
		    bool shortMidlineBreakdown = CrossBelow(Close, midline, 1);
		
		    // 2. Midline Turning Down: Midline shows a clear downward turn,
		    //    meaning current midline is below previous, and previous was flat or rising.
		    bool shortMidlineTurn = (midline[0] < midline[1] && midline[1] >= midline[2]);
		
		    // 3. Rejection from Upper Band: Price tests the upper band and shows signs of reversal.
		    //    - Previous bar's high touched or went above the upper band.
		    //    - Current bar's high is lower than the previous bar's high (not making new highs).
		    bool shortBandRejection = (High[1] >= highestHigh[1] && High[0] < High[1]);
		
		    // 4. (Optional) Lowest Low Band Turning Down: The lower band itself starts to expand downwards.
		    //    This indicates a potential increase in downward volatility or a breakdown from the recent range.
		    bool shortLLBandTurn = (lowestLow[0] < lowestLow[1] && lowestLow[1] >= lowestLow[2]);
			
			bool highCrossBelowMidline = (High[0] < midline[0] && High[1] >= midline[1]);
			
			bool midlineDownTurn = (midline[0] < midline[1]);
		
		    // --- Mandatory Confirmations for All Short Entries ---
		    // Current bar is bearish: Closes lower than its open AND closes lower than the previous bar's close.
		    bool bearishBarConfirm = (Close[0] < Open[0] && Close[0] < Close[1]);
		    // Momentum is falling AND Momentum value is negative.
		    bool momentumConfirmShort = (Momentum1[0] < Momentum1[1] && Momentum1[0] < 0);
		
		    // Combine: Any of the primary signals AND all confirmations.
		    // You can choose to include shortLLBandTurn by uncommenting it if desired.
		    shortSignal = (shortMidlineBreakdown || shortMidlineTurn || shortBandRejection || shortLLBandTurn || highCrossBelowMidline || midlineDownTurn)
		                  && bearishBarConfirm
		                  && momentumConfirmShort;
			
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
            return false;
        }

        protected override bool ValidateExitShort()
        {
			return false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
			HiLoBands1				= HiLoBands(LookbackPeriod, Width);
			HiLoBands1.Plots[0].Brush = Brushes.Cyan;
			HiLoBands1.Plots[1].Brush = Brushes.Magenta;
			if (showHighLow) AddChartIndicator(HiLoBands1);
			
			Momentum1			= Momentum(Close, 14);	
			Momentum1.Plots[0].Brush = Brushes.Yellow;
			Momentum1.Plots[0].Width = 2;
			if (showMomo) AddChartIndicator(Momentum1);
			
        }
        #endregion

        #region Properties

//		[NinjaScriptProperty]
//        [Display(Name = "Risk to Reward Ratio", Order = 1, GroupName="02. Order Settings")]
//        public double RiskToReward { get; set; }		
		
		[NinjaScriptProperty]
        [Display(Name = "HiLo Period", Order = 1, GroupName="08a. Strategy Settings")]
        public int LookbackPeriod { get; set; }		
		
		[NinjaScriptProperty]
        [Display(Name = "Line Width", Order = 2, GroupName="08a. Strategy Settings")]
        public int Width { get; set; }	
		
		[NinjaScriptProperty]
        [Display(Name = "Show High Low Bands", Order = 3, GroupName = "08a. Strategy Settings")]
        public bool showHighLow { get; set; }
		
		[NinjaScriptProperty]
        [Display(Name = "Show Momentum", Order = 4, GroupName = "08a. Strategy Settings")]
        public bool showMomo { get; set; }
		
//		[NinjaScriptProperty]
//		[Display(Name="Trail Stop Tick Offset", Order = 5, GroupName="08a. Strategy Settings")]
//		public int TrailOffset
//		{ get; set; }

        #endregion
    }
}
