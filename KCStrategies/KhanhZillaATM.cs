#region Using declarations
using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.Core;
using BlueZ = NinjaTrader.NinjaScript.Indicators.BlueZ; // Alias for better readability
using RegressionChannel = NinjaTrader.NinjaScript.Indicators.RegressionChannel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies.KCStrategies
{
    public class KhanhZillaATM : ATMAlgoBase2
    {
//		private RegressionChannelHighLow RegressionChannelHighLow1;	
//		private BlueZ.BlueZHMAHooks hullMAHooks;	
		
//        private VMA VMA1;
//        private bool volMaUp;
//        private bool volMaDown;

		private double highestHigh;
		private double lowestLow;
		
		public override string DisplayName { get { return Name; } }
		
        protected override void OnStateChange()
        {
            base.OnStateChange();

            if (State == State.SetDefaults)
            {
                Description = "Strategy based on the Linear Regression Channel indicator.";
                Name = "KhanhZilla ATM v5.2";
                StrategyName = "KhanhZilla ATM";
                Version = "5.2 Apr. 2025";
                Credits = "Strategy by Khanh Nguyen";
                ChartType = "Tbars 20";		

//				HmaPeriod		= 16;
//				RegChanPeriod	= 20;
//				RegChanWidth	= 5;
				showMomo		= true;
				showVMA			= false;

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
			
			longSignal = (Low[0] > RegressionChannelHighLow1.Lower[1]) 
				&& (Low[1] == RegressionChannelHighLow1.Lower[1]);

            shortSignal =  (High[0] < RegressionChannelHighLow1.Upper[1])
				&& (High[1] == RegressionChannelHighLow1.Upper[1]);
			
			if (longSignal)				
				lowestLow = RegressionChannelHighLow1.Lower[1];
			
			if (shortSignal)
				highestHigh = RegressionChannelHighLow1.Upper[1];

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
            if (exitLong) return true;
            else return false;
        }

        protected override bool ValidateExitShort()
        {
			if (exitShort) return true;
			else return false;
        }

        #region Indicators
        protected override void InitializeIndicators()
        {
//			RegressionChannelHighLow1 = RegressionChannelHighLow(Close, Convert.ToInt32(RegChanPeriod), RegChanWidth);		
//			RegressionChannelHighLow1.Plots[0].Width = 2;
//			RegressionChannelHighLow1.Plots[1].Width = 2;
//            RegressionChannelHighLow1.Plots[1].Brush = Brushes.Yellow;
//			RegressionChannelHighLow1.Plots[2].Width = 2;
//            RegressionChannelHighLow1.Plots[2].Brush = Brushes.Yellow;
//			AddChartIndicator(RegressionChannelHighLow1);
			
//			hullMAHooks	= BlueZHMAHooks(Close, HmaPeriod, 0, false, false, true, Brushes.Lime, Brushes.Red);
//			hullMAHooks.Plots[0].Brush = Brushes.White;
//			hullMAHooks.Plots[0].Width = 2;
//			if (showHmaHooks) AddChartIndicator(hullMAHooks);
			
//			VMA1				= VMA(Close, 9, 9);
//			VMA1.Plots[0].Brush = Brushes.SkyBlue;
//			VMA1.Plots[0].Width = 3;
//			if (showVMA) AddChartIndicator(VMA1);
        }
        #endregion


		#region Properties - Strategy Settings
	
//		[NinjaScriptProperty]
//		[Display(Name="Regression Channel Period", Order=1, GroupName="08a. Strategy Settings")]
//		public int RegChanPeriod
//		{ get; set; }

//		[NinjaScriptProperty]
//		[Display(Name="Regression Channel Width", Order=2, GroupName="08a. Strategy Settings")]
//		public double RegChanWidth
//		{ get; set; }

//		[NinjaScriptProperty]
//		[Display(Name = "Show VMA", Order = 4, GroupName = "08b. Default Settings")]
//		public bool showVMA { get; set; }

//		[NinjaScriptProperty]
//		[Display(Name = "Show HMA Hooks", Order = 6, GroupName = "08b. Default Settings")]
//		public bool showHmaHooks { get; set; }

//		[NinjaScriptProperty]
//		[Display(Name = "HMA Period", Order = 7, GroupName = "08b. Default Settings")]
//		public int HmaPeriod { get; set; }

		#endregion
	}
}
